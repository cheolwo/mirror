"""인허가 CSV의 지역 누출·헤더 변화·중복/결측 보존과 무네트워크 검증."""
import copy
import csv
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import collect_public_snapshots as collector

HEADER = ['개방자치단체코드','관리번호','영업상태명','인허가일자','X좌표','Y좌표','면적']
SOURCE = {'exactHeader':HEADER,'requiredColumns':HEADER,'expectedAgencyCode':'3060000',
          'dateColumns':['인허가일자'],'statusColumns':['영업상태명'],
          'coordinatePairs':[['X좌표','Y좌표']],'numericColumns':['면적'],'encoding':'auto'}
ROW = {'개방자치단체코드':'3060000','관리번호':'key1','영업상태명':'영업/정상',
       '인허가일자':'20261101','X좌표':'208000.5','Y좌표':'453000','면적':'10.5'}

def body(rows=None, header=None, encoding='utf-8'):
    output=io.StringIO(newline='')
    writer=csv.DictWriter(output,fieldnames=HEADER if header is None else header)
    writer.writeheader(); writer.writerows([ROW] if rows is None else rows)
    return output.getvalue().encode(encoding)

class SeoulLicenseCsvTests(unittest.TestCase):
    def validate(self, raw=None, source=None, limit=100):
        return collector.validate_business_license_csv(body() if raw is None else raw,
                    SOURCE if source is None else source,limit)

    def code(self, expected, **kwargs):
        with self.assertRaises(collector.CollectionError) as result:
            self.validate(**kwargs)
        self.assertEqual(expected,result.exception.code)

    def test_exact_header_cp949_utf8_bom_and_bytes_unchanged(self):
        for encoding in ['cp949','utf-8','utf-8-sig']:
            raw=body(encoding=encoding); before=bytes(raw)
            result=self.validate(raw)
            self.assertEqual(1,result['recordCount'])
            self.assertEqual(encoding,result['encoding'])
            self.assertEqual(before,raw)

    def test_header_hash_contract_and_order_drift(self):
        source={**SOURCE,'exactHeader':[],'headerSha256':hashlib.sha256(json.dumps(HEADER,ensure_ascii=False,separators=(',',':')).encode()).hexdigest()}
        self.assertEqual(1,self.validate(source=source)['recordCount'])
        for header in [list(reversed(HEADER)),HEADER+['EXTRA']]:
            self.code('license_csv_headers_mismatch',raw=body(header=header))
        self.code('license_csv_headers_mismatch',source={**source,'headerSha256':'0'*64})
        self.code('license_csv_header_contract_required',source={**SOURCE,'exactHeader':[]})

    def test_wrong_agency_in_later_row_and_empty_management_rejected(self):
        self.code('license_csv_agency_mismatch',raw=body([ROW,{**ROW,'관리번호':'two','개방자치단체코드':'3040000'}]))
        self.code('license_csv_agency_mismatch',raw=body([{**ROW,'개방자치단체코드':''}]))
        self.code('license_csv_management_id_empty',raw=body([{**ROW,'관리번호':' '}]))

    def test_duplicate_rows_and_changed_same_key_are_preserved(self):
        result=self.validate(body([ROW,ROW,{**ROW,'영업상태명':'폐업'}]))
        self.assertEqual((3,1,2,1),(result['recordCount'],result['uniqueManagementIds'],result['duplicateManagementRows'],result['duplicateManagementGroups']))
        self.assertEqual({'영업/정상':2,'폐업':1},result['statusSummary']['영업상태명']['values'])
        self.assertTrue(result['duplicatesPreserved'])

    def test_missing_zero_invalid_negative_are_distinct_without_filling(self):
        result=self.validate(body([ROW,{**ROW,'관리번호':'2','면적':'','X좌표':'','Y좌표':'','인허가일자':'','영업상태명':''},
                                  {**ROW,'관리번호':'3','면적':'0','X좌표':'0','Y좌표':'0','인허가일자':'20260230'},
                                  {**ROW,'관리번호':'4','면적':'NaN','X좌표':'inf','Y좌표':'1'},
                                  {**ROW,'관리번호':'5','면적':'-2','X좌표':'','Y좌표':'1'}]))
        self.assertEqual((1,1,1,1),tuple(result['numericSummary']['면적'][k] for k in ['missing','invalid','zero','negative']))
        self.assertEqual((1,1),tuple(result['dateSummary']['인허가일자'][k] for k in ['missing','invalid']))
        self.assertEqual((1,1,1,1),tuple(result['coordinateSummary'][0][k] for k in ['missingBoth','missingOne','zeroBoth','invalid']))
        self.assertFalse(result['missingValuesFilled']); self.assertIsNone(result['evidenceAsOf'])

    def test_empty_provider_errors_encoding_and_csv_shape(self):
        for raw,code in [(b'','license_csv_empty'),(b'<html>error</html>','license_csv_not_csv'),(b'{"error":1}','license_csv_not_csv'),(b'\xff\xfe\x00','license_csv_encoding_invalid'),(body([]),'license_csv_no_records')]:
            self.code(code,raw=raw)
        self.code('license_csv_row_shape_invalid',raw=body()+b'3060000,short\n')
        self.code('license_csv_syntax_invalid',raw=body()+b'"unterminated\n')
        self.code('record_limit_exceeded',raw=body([ROW,ROW]),limit=1)

    def test_invalid_configuration_and_required_fields_fail_closed(self):
        for changes in [{'expectedAgencyCode':'306'}, {'encoding':'latin1'}, {'coordinatePairs':[['X좌표']]}, {'numericColumns':['unknown']}]:
            expected='license_csv_required_columns_missing' if 'numericColumns' in changes else 'license_csv_config_invalid'
            self.code(expected,source={**SOURCE,**changes})
        for limit in [0,True,100001]: self.code('invalid_record_limit',limit=limit)

    def test_dispatch_and_dry_run_make_no_network_or_files(self):
        self.assertIs(collector.validate_business_license_csv,collector.VALIDATORS['seoul_business_license_csv'])
        count_url='https://data.seoul.go.kr/dataList/dataView.do?onepagerow=1&infId=OA-23056&filterCol=OPNSFTEAMCODE&txtFilter=3060000'
        source={**SOURCE,'id':'license-test','sourceId':'seoul-open-data','datasetId':'OA-23056','kind':'dataset','status':'ready','url':'https://example.org/public.csv','method':'POST','form':{'ssUserId':'SAMPLE_VIEW','gridTotalCnt':'__DISCOVERED_COUNT__','infId':'OA-23056','filterCol':'OPNSFTEAMCODE','txtFilter':'3060000'},'validator':'seoul_business_license_csv','intervalHours':24,'maxRecords':100,'maxBytes':10000,'countDiscovery':{'url':count_url,'maxBytes':10000}}
        with tempfile.TemporaryDirectory() as folder, patch.object(collector,'fetch',side_effect=AssertionError('network forbidden')):
            target=Path(folder)/'not-created'
            result=collector.collect_source(source,target,[source['url'],count_url],{'maxRequests':2,'maxTotalBytes':10000,'timeoutSeconds':1},collector.datetime.now(collector.UTC),{'requests':0,'bytes':0},dry_run=True,resolve_dns=False)
            self.assertEqual('dry_run',result['status']); self.assertFalse(target.exists())

if __name__=='__main__': unittest.main()
