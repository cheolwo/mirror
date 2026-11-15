"""임시 DB/가상 공개응답만 사용하는 NLIC 주기 수집 회귀 검사."""
import copy
from datetime import datetime, timezone
import json
from pathlib import Path
import sqlite3
import tempfile
import unittest
from unittest.mock import Mock, patch

import logistics_facility_refresh as m

UTC=timezone.utc
HEADERS={
 'RegisteredWarehouse':['상호명','사업자번호','소재지','일반창고면적(m²)','냉동냉장창고면적(m²)','보관장소면적(m²)','타법률창고면적(m²)','관련법률','취급품목'],
 'LogisticsComplex':['물류시설명','시행자','주소','총면적(㎡)','총사업비(억)','단지시설면적(㎡)','지원시설면적(㎡)','공공시설면적 (㎡)','분양율 (%)','입주율 (%)','지정고시일','사업시작월','사업종료월','주요입점업체','구분','기타사항'],
 'LogisticsTerminal':['물류터미널명','지역','규모','공사시행인가일']}
VALUES={
 'RegisteredWarehouse':['창고 예시','0012345678','경기도 예시주소','0','','5','0','물류시설법','식품'],
 'LogisticsComplex':['단지 예시','사업 시행회사','경기도 단지주소',100.0,1.5,60.0,20.0,20.0,0.0,30.0,'2000/01/01','1999','2001','입점기업','운영중',''],
 'LogisticsTerminal':['터미널 예시','경기도 터미널주소',10.0,'2000/01/01']}
PAGE=b'''function fn_down(){ $("S_PAGE_LEN").value="1"; } function next(){}'''

class Cell:
    def __init__(self,value):
        self.value=value
        self.ctype=2 if isinstance(value,(int,float)) else 0 if value=='' else 1
class Sheet:
    name='공개원본_0'
    def __init__(self,rows): self.rows=rows;self.nrows=len(rows);self.ncols=len(rows[0])
    def cell(self,r,c): return Cell(self.rows[r][c])
class Book:
    nsheets=1
    def __init__(self,rows):self.sheet=Sheet(rows)
    def sheet_by_index(self,index):return self.sheet
class Parser:
    def __init__(self):self.fixtures={}
    def add(self,body,kind,*,headers=None,rows=None):
        self.fixtures[body]=[headers or HEADERS[kind],['']*len(HEADERS[kind]),*(rows if rows is not None else [VALUES[kind]])]
    def open_workbook(self,*,file_contents,logfile):return Book(self.fixtures[file_contents])

def create_fixture_db(path,sources):
    con=sqlite3.connect(path)
    con.executescript('''
CREATE TABLE sources(source_key TEXT PRIMARY KEY,source_id TEXT,dataset_id TEXT,title TEXT,kind TEXT,official_url TEXT,collected_at_utc TEXT,evidence_as_of_utc TEXT,metadata_json TEXT);
CREATE TABLE raw_snapshots(snapshot_key TEXT PRIMARY KEY,source_key TEXT REFERENCES sources,repo_path TEXT,sha256 TEXT,bytes INTEGER,content_type TEXT);
CREATE TABLE facility_observations(observation_key TEXT PRIMARY KEY,snapshot_key TEXT REFERENCES raw_snapshots,source_sheet TEXT,source_row INTEGER,facility_type TEXT,facility_name TEXT,address_source TEXT,operator_source TEXT,business_number_source TEXT,status_source TEXT,items_source TEXT,address_prefix_candidate TEXT,latitude REAL,longitude REAL,coordinate_status TEXT,evidence_as_of_utc TEXT,row_content_sha256 TEXT,raw_cells_json TEXT);
CREATE TABLE facility_measures(observation_key TEXT REFERENCES facility_observations,measure_code TEXT,value_source TEXT,value_decimal_text TEXT,parse_status TEXT,unit_code TEXT,PRIMARY KEY(observation_key,measure_code));
CREATE TABLE facility_attributes(observation_key TEXT REFERENCES facility_observations,column_number INTEGER,source_header TEXT,value_source TEXT,PRIMARY KEY(observation_key,column_number));
CREATE VIEW facility_search AS SELECT f.observation_key,f.facility_type,f.facility_name,f.address_source,f.status_source,f.items_source,f.address_prefix_candidate,f.coordinate_status,f.evidence_as_of_utc,s.source_id,s.dataset_id,s.collected_at_utc,r.repo_path,r.sha256,f.source_sheet,f.source_row FROM facility_observations f JOIN raw_snapshots r USING(snapshot_key) JOIN sources s USING(source_key);
''')
    for s in sources:
        key='baseline-'+s['id'];snap='snapshot-'+s['id'];b=s['baseline']
        con.execute('INSERT INTO sources VALUES(?,?,?,?,?,?,?,?,?)',(key,s['sourceId'],s['datasetId'],'baseline','reference',s['url'],b['collectedAtUtc'],None,'{}'))
        con.execute('INSERT INTO raw_snapshots VALUES(?,?,?,?,?,?)',(snap,key,b['rawPath'],b['sha256'],20,'application/vnd.ms-excel'))
        con.execute('INSERT INTO facility_observations VALUES('+','.join('?' for _ in range(18))+')',
                    ('old-'+s['id'],snap,'원본',2,s['facilityType'],'기존 기록','기존주소',None,None,None,None,'기존주소',None,None,'Unverified',None,'old','[]'))
    con.commit();con.close()

class RefreshTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=Path(self.temp.name)
        home=self.root/'artifacts/local/test';home.mkdir(parents=True);self.db=home/'facilities.sqlite'
        self.policy={'schemaVersion':'logistics-facilities-refresh.v1','outputRoot':'artifacts/local/test/refresh',
                     'facilityDatabasePath':'artifacts/local/test/facilities.sqlite','xlrdLibraryPath':'artifacts/local/test/deps',
                     'minimumIntervalHours':48,'maxSourceBytes':1024,'maxRunBytes':4096,'timeoutSeconds':1,'sources':[]}
        self.parser=Parser();self.new={};self.old={}
        for index,kind in enumerate(HEADERS):
            sid='s'+str(index);body=m.MAGIC+('old-'+sid).encode();path=home/(sid+'.xls');path.write_bytes(body)
            self.old[kind]=body;self.parser.add(body,kind)
            new=m.MAGIC+('new-'+sid).encode();self.new[kind]=new;self.parser.add(new,kind)
            self.policy['sources'].append({'id':sid,'sourceId':'official-'+sid,'datasetId':'dataset-'+sid,
                 'group':'warehouses' if kind=='RegisteredWarehouse' else 'hubs','facilityType':kind,'url':m.URLS[kind],
                 'form':{'command':'DWLOAD'},'discoverPageLength':kind=='RegisteredWarehouse','expectedHeaders':HEADERS[kind],
                 'baseline':{'rawPath':path.relative_to(self.root).as_posix(),'sha256':m.digest(body),
                             'collectedAtUtc':'2026-11-14T00:00:00.000000+00:00','periodKey':'2026-11' if kind=='RegisteredWarehouse' else '2026-Q4'}})
        create_fixture_db(self.db,self.policy['sources']);self.state=home/'refresh/state.json'
    def run_refresh(self,group='warehouses',now=None,fetcher=None,**kwargs):
        return m.run(self.root,self.policy,group,now or datetime(2026,12,1,tzinfo=UTC),
                     fetcher=fetcher or Mock(side_effect=[PAGE,self.new['RegisteredWarehouse']]),parser=self.parser,**kwargs)
    def query(self,sql,params=()):
        con=sqlite3.connect(self.db)
        try:return con.execute(sql,params).fetchall()
        finally:con.close()
    def hashes(self):return m.digest(self.db.read_bytes()),self.state.read_bytes() if self.state.exists() else None
    def test_periods_use_kst_and_year_boundary(self):
        now=datetime(2026,12,31,15,tzinfo=UTC)
        self.assertEqual(m.period_key(now,'warehouses'),'2027-01');self.assertEqual(m.period_key(now,'hubs'),'2027-Q1')
    def test_minimum_interval_and_same_period(self):
        prev={'lastCheckedAtUtc':'2026-11-30T00:00:00+00:00','periodKey':'2026-11'}
        self.assertFalse(m.is_due(datetime(2026,12,1,tzinfo=UTC),'warehouses',prev,48))
        self.assertTrue(m.is_due(datetime(2026,12,2,tzinfo=UTC),'warehouses',prev,48))
        prev['periodKey']='2026-12';self.assertFalse(m.is_due(datetime(2026,12,20,tzinfo=UTC),'warehouses',prev,48))
    def test_dry_run_does_not_create_state_or_touch_database(self):
        before=self.hashes();fetcher=Mock(side_effect=AssertionError('network'))
        result=self.run_refresh(dry_run=True,fetcher=fetcher)
        self.assertEqual(result['httpRequests'],0);self.assertEqual(self.hashes(),before);self.assertFalse(self.state.parent.exists())
    def test_baseline_adoption_is_not_due_without_network(self):
        result=self.run_refresh(now=datetime(2026,11,20,tzinfo=UTC),fetcher=Mock(side_effect=AssertionError('network')))
        self.assertEqual(result['status'],'not_due');self.assertTrue(self.state.exists());self.assertEqual(self.query('select count(*) from facility_observations')[0][0],3)
    def test_changed_appends_and_preserves_old_source_and_missing_zero(self):
        result=self.run_refresh();self.assertEqual(result['status'],'updated');self.assertEqual(result['newSqliteObservations'],1)
        self.assertEqual(self.query('select count(*) from facility_observations')[0][0],4)
        self.assertEqual(self.query("select count(*) from facility_observations where observation_key like 'old-%'")[0][0],3)
        self.assertEqual(self.query("select value_decimal_text,parse_status from facility_measures where measure_code='general_warehouse_area'"),[('0','Parsed')])
        self.assertEqual(self.query("select value_decimal_text,parse_status from facility_measures where measure_code='cold_warehouse_area'"),[(None,'Missing')])
        self.assertEqual(self.query('select count(*) from facility_observations where evidence_as_of_utc is not null or latitude is not null')[0][0],0)
        self.assertEqual(self.query('select business_number_source from facility_observations where business_number_source is not null'),[('0012345678',)])
        self.assertEqual(self.query('select count(*) from facility_latest_search')[0][0],3)
    def test_unchanged_keeps_database_hash_and_pending_retry(self):
        first=self.run_refresh();before=m.digest(self.db.read_bytes())
        second=self.run_refresh(now=datetime(2027,1,1,tzinfo=UTC))
        self.assertEqual(second['status'],'unchanged');self.assertEqual(second['pendingMysql'],first['pendingMysql'])
        self.assertEqual(m.digest(self.db.read_bytes()),before)
        result=self.run_refresh(now=datetime(2027,1,2,tzinfo=UTC),fetcher=Mock(side_effect=AssertionError('network')))
        self.assertEqual(result['httpRequests'],0);self.assertTrue(result['pendingMysql'])
    def test_invalid_headers_do_not_modify_database_or_state(self):
        self.parser.add(self.new['RegisteredWarehouse'],'RegisteredWarehouse',headers=['wrong']+HEADERS['RegisteredWarehouse'][1:])
        before=self.hashes()
        with self.assertRaisesRegex(m.RefreshError,'headers_changed'):self.run_refresh()
        self.assertEqual(self.hashes(),before)
    def test_zero_rows_do_not_modify_database_or_state(self):
        self.parser.add(self.new['RegisteredWarehouse'],'RegisteredWarehouse',rows=[]);before=self.hashes()
        with self.assertRaisesRegex(m.RefreshError,'row_count_invalid'):self.run_refresh()
        self.assertEqual(self.hashes(),before)
    def test_oversized_response_fails_before_db_or_state(self):
        before=self.hashes()
        with self.assertRaisesRegex(m.RefreshError,'size_exceeded'):self.run_refresh(fetcher=Mock(side_effect=[PAGE,b'X'*1025]))
        self.assertEqual(self.hashes(),before)
    def test_declared_warehouse_count_must_match_workbook(self):
        before=self.hashes()
        with self.assertRaisesRegex(m.RefreshError,'declared_count_mismatch'):
            self.run_refresh(fetcher=Mock(side_effect=[PAGE.replace(b'"1"',b'"2"'),self.new['RegisteredWarehouse']]))
        self.assertEqual(self.hashes(),before)
    def test_hubs_keep_implementer_and_tenants_as_source_attributes(self):
        result=self.run_refresh('hubs',datetime(2027,1,1,tzinfo=UTC),Mock(side_effect=[self.new['LogisticsComplex'],self.new['LogisticsTerminal']]))
        self.assertEqual(result['newSqliteObservations'],2)
        self.assertEqual(self.query("select operator_source,items_source from facility_observations where facility_name='단지 예시'"),[(None,None)])
        self.assertEqual(self.query("select value_source from facility_attributes where source_header='시행자'"),[('사업 시행회사',)])
        self.assertEqual(self.query("select value_source from facility_attributes where source_header='주요입점업체'"),[('입점기업',)])
        manifest=m.read_json(self.root/result['pendingMysql'][0]['manifestPath']);self.assertEqual(len(manifest['entries']),2)
    def test_group_validation_finishes_before_any_database_write(self):
        self.parser.add(self.new['LogisticsTerminal'],'LogisticsTerminal',headers=['wrong']+HEADERS['LogisticsTerminal'][1:]);before=self.hashes()
        with self.assertRaisesRegex(m.RefreshError,'headers_changed'):
            self.run_refresh('hubs',datetime(2027,1,1,tzinfo=UTC),Mock(side_effect=[self.new['LogisticsComplex'],self.new['LogisticsTerminal']]))
        self.assertEqual(self.hashes(),before)
    def test_historical_body_reappearance_blocks_without_false_latest(self):
        self.run_refresh();before=self.hashes()
        with self.assertRaisesRegex(m.RefreshError,'historical_body_reappeared'):
            self.run_refresh(now=datetime(2027,1,1,tzinfo=UTC),fetcher=Mock(side_effect=[PAGE,self.old['RegisteredWarehouse']]))
        self.assertEqual(self.hashes(),before)
        self.assertFalse((self.state.parent/'pending-commit.json').exists())
    def test_os_lock_conflict_leaves_state_and_database_unchanged(self):
        self.state.parent.mkdir();before=self.hashes()
        with m.exclusive_lock(self.state.parent/'refresh.lock'):
            with self.assertRaisesRegex(m.RefreshError,'collector_busy'):self.run_refresh()
        self.assertEqual(self.hashes(),before)
    def test_commit_then_state_failure_recovers_without_http(self):
        original=m.atomic_json
        def fail_state(path,data):
            if path==self.state:raise OSError('simulated interruption')
            return original(path,data)
        with patch.object(m,'atomic_json',side_effect=fail_state):
            with self.assertRaises(OSError):self.run_refresh()
        self.assertFalse(self.state.exists());self.assertEqual(self.query('select count(*) from facility_observations')[0][0],4)
        before=m.digest(self.db.read_bytes());fetcher=Mock(side_effect=AssertionError('network'))
        dry=self.run_refresh(fetcher=fetcher,dry_run=True);self.assertIn('pendingRecoveryPath',dry);self.assertFalse(self.state.exists())
        result=self.run_refresh(fetcher=fetcher)
        self.assertEqual(result['status'],'recovered');self.assertEqual(result['httpRequests'],0);self.assertTrue(result['pendingMysql'])
        self.assertEqual(before,m.digest(self.db.read_bytes()));self.assertFalse((self.state.parent/'pending-commit.json').exists())
    def test_receipt_failure_after_commit_recovers_pending(self):
        original=m.atomic_json
        def fail_receipt(path,data):
            if path.name=='receipt.json':raise OSError('simulated interruption')
            return original(path,data)
        with patch.object(m,'atomic_json',side_effect=fail_receipt):
            with self.assertRaises(OSError):self.run_refresh()
        result=self.run_refresh(fetcher=Mock(side_effect=AssertionError('network')))
        self.assertEqual(result['status'],'recovered');self.assertTrue(result['pendingMysql'])
    def test_request_cap_prevents_extra_post(self):
        self.policy['maxRequests']=1;fetcher=Mock(return_value=PAGE);before=self.hashes()
        with self.assertRaisesRegex(m.RefreshError,'request_limit_exceeded'):self.run_refresh(fetcher=fetcher)
        self.assertEqual(fetcher.call_count,1);self.assertEqual(self.hashes(),before)
    def test_failed_transaction_rolls_back_first_snapshot_and_new_view(self):
        original=m.append_snapshot;calls=0
        def fail_second(*args):
            nonlocal calls
            calls+=1
            if calls==2:raise m.RefreshError('simulated_transaction_failure')
            return original(*args)
        before=self.hashes()
        with patch.object(m,'append_snapshot',side_effect=fail_second):
            with self.assertRaisesRegex(m.RefreshError,'simulated_transaction_failure'):
                self.run_refresh('hubs',datetime(2027,1,1,tzinfo=UTC),Mock(side_effect=[self.new['LogisticsComplex'],self.new['LogisticsTerminal']]))
        self.assertEqual(self.hashes(),before)
        self.assertEqual(self.query("select count(*) from sqlite_master where name='facility_latest_search'"),[(0,)])
        self.assertFalse((self.state.parent/'pending-commit.json').exists())
    def test_mysql_ack_requires_bound_verified_receipt(self):
        result=self.run_refresh();item=result['pendingMysql'][0];manifest=m.read_json(self.root/item['manifestPath'])
        receipt={'status':'completed','mode':'verify','independentReadbackVerified':True,
                 'databaseTarget':'hongdal-mysql-1 / 127.0.0.1:13306 / hongdal_dev','manifestSha256':item['manifestSha256'],
                 'entryCount':1,'entries':[{'sourceId':e['sourceId'],'datasetId':e['datasetId'],'verified':True,'existing':True,'normalizedCount':0} for e in manifest['entries']]}
        path=self.root/'artifacts/local/test/verify.json';path.write_text(json.dumps(receipt),encoding='utf-8')
        bad=copy.deepcopy(receipt);bad['entries'][0]['verified']=False;path.write_text(json.dumps(bad),encoding='utf-8');before=self.hashes()
        with self.assertRaisesRegex(m.RefreshError,'entries_unverified'):self.run_refresh(ack_receipt=path.as_posix())
        self.assertEqual(self.hashes(),before)
        path.write_text(json.dumps(receipt),encoding='utf-8')
        output=self.run_refresh(ack_receipt=path.as_posix(),fetcher=Mock(side_effect=AssertionError('network')))
        self.assertEqual(output['status'],'mysql_acknowledged');self.assertEqual(output['pendingMysql'],[])
    def test_redirect_and_wrong_source_url_cannot_be_enabled_by_policy(self):
        self.policy['sources'][0]['url']='https://example.com/warehouse';before=self.hashes()
        with self.assertRaisesRegex(m.RefreshError,'source_url_rejected'):self.run_refresh()
        self.assertEqual(self.hashes(),before)
        self.assertIsNone(m.NoRedirect().redirect_request(None,None,302,'',{},'https://example.com'))

if __name__=='__main__':unittest.main()
