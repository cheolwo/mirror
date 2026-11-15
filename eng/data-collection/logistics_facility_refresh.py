"""NLIC의 명시한 세 공개 자료만 주기 확인하고 시설 참조 이력을 추가한다.

MySQL은 실행하지 않는다. 검증된 reference manifest를 pending으로 반환하며,
별도 verify 영수증으로만 pending을 해제한다. dry-run은 파일/네트워크/DB를 쓰지 않는다.
"""
from __future__ import annotations
import argparse
from contextlib import contextmanager
from datetime import datetime, timedelta, timezone
from decimal import Decimal
import hashlib
import io
import json
from pathlib import Path
import re
import sqlite3
import sys
from urllib.parse import urlencode
from urllib.request import Request, build_opener, HTTPRedirectHandler
import uuid

UTC = timezone.utc
KST = timezone(timedelta(hours=9))
MAGIC = bytes.fromhex('d0cf11e0a1b11ae1')
POLICY = 'eng/data-collection/logistics-facilities-refresh.jobs.json'
URLS = {'RegisteredWarehouse': 'https://www.nlic.go.kr/nlic/WhsInfoWarehouseSch.action',
        'LogisticsComplex': 'https://www.nlic.go.kr/nlic/fmComplex0010.action',
        'LogisticsTerminal': 'https://www.nlic.go.kr/nlic/fmTerminal0010.action'}
SPECS = {
 'RegisteredWarehouse': (0, 2, 1, None, 8, {3:('general_warehouse_area','m2'),4:('cold_warehouse_area','m2'),5:('storage_yard_area','m2'),6:('other_law_warehouse_area','m2')}),
 'LogisticsComplex': (0, 2, None, 14, None, {3:('total_area','m2'),4:('project_cost','KRW_100million'),5:('logistics_facility_area','m2'),6:('support_facility_area','m2'),7:('public_facility_area','m2'),8:('sale_rate','percent'),9:('occupancy_rate','percent')}),
 'LogisticsTerminal': (0, 1, None, None, None, {2:('total_area','m2')})}

class RefreshError(Exception):
    pass

def require(condition, code):
    if not condition: raise RefreshError(code)

def digest(data): return hashlib.sha256(data).hexdigest()
def compact(data): return json.dumps(data, ensure_ascii=False, separators=(',', ':'))
def read_json(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def stamp(value): return value.astimezone(UTC).isoformat(timespec='microseconds')
def time_value(value):
    result = datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(result.tzinfo is not None, 'timestamp_timezone_missing')
    return result.astimezone(UTC)

def beneath(root, value):
    require(isinstance(value, str) and value.startswith('artifacts/local/') and '\\' not in value, 'artifact_path_invalid')
    path = (root/value).resolve()
    require(path.is_relative_to((root/'artifacts/local').resolve()), 'artifact_path_escape')
    return path

def atomic_json(path, data):
    temporary = path.with_name(path.name+'.'+uuid.uuid4().hex+'.tmp')
    temporary.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    temporary.replace(path)

def period_key(now, group):
    local = now.astimezone(KST)
    return local.strftime('%Y-%m') if group == 'warehouses' else f'{local.year}-Q{(local.month-1)//3+1}'

def is_due(now, group, previous, minimum_hours):
    checked = time_value(previous['lastCheckedAtUtc'])
    return previous['periodKey'] != period_key(now, group) and now-checked >= timedelta(hours=minimum_hours)

@contextmanager
def exclusive_lock(path):
    handle = path.open('a+b')
    if handle.tell() == 0: handle.write(b'0'); handle.flush()
    handle.seek(0)
    try:
        try:
            if sys.platform == 'win32':
                import msvcrt
                msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except (OSError, BlockingIOError): raise RefreshError('collector_busy') from None
        yield
    finally:
        # Closing the descriptor releases the OS lock even after an exception.
        handle.close()

class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl): return None

def fetch(url, form, maximum, timeout):
    request = Request(url, data=urlencode(form).encode('ascii') if form is not None else None,
                      headers={'User-Agent':'HongdalPublicDataResearch/1.0', 'Accept-Encoding':'identity',
                               **({'Content-Type':'application/x-www-form-urlencoded'} if form is not None else {})})
    with build_opener(NoRedirect).open(request, timeout=timeout) as response:
        require(response.status == 200, 'http_status_invalid')
        body = response.read(maximum+1)
    require(len(body) <= maximum, 'source_size_exceeded')
    return body

def warehouse_count(body):
    try: text = body.decode('utf-8-sig')
    except UnicodeError: raise RefreshError('warehouse_page_encoding_invalid') from None
    function = re.search(r'function\s+fn_down\s*\([^)]*\)\s*\{([\s\S]*?)function\s+', text)
    require(function is not None, 'warehouse_download_function_missing')
    values = re.findall(r'\$\(["\']S_PAGE_LEN["\']\)\.value\s*=\s*["\'](\d+)["\']', function.group(1))
    require(len(values) == 1 and 0 < int(values[0]) <= 100000, 'warehouse_declared_count_invalid')
    return int(values[0])

def load_parser(root, policy):
    directory = beneath(root, policy['xlrdLibraryPath'])
    require((directory/'xlrd/__init__.py').is_file(), 'xlrd_dependency_missing')
    sys.path.insert(0, str(directory))
    import xlrd
    require(xlrd.__version__ == '2.0.2', 'xlrd_version_mismatch')
    return xlrd

def cell_text(cell):
    if cell.ctype in (0, 6): return ''
    if cell.ctype == 2:
        return format(Decimal(str(cell.value)), 'f').rstrip('0').rstrip('.') if '.' in str(cell.value) else str(cell.value)
    return str(cell.value)

def parse_workbook(body, source, parser):
    require(body.startswith(MAGIC), 'workbook_signature_invalid')
    book = parser.open_workbook(file_contents=body, logfile=io.StringIO())
    require(book.nsheets == 1, 'workbook_sheet_count_changed')
    sheet = book.sheet_by_index(0)
    headers = [cell_text(sheet.cell(0,c)) for c in range(sheet.ncols)]
    require(headers == source['expectedHeaders'], 'workbook_headers_changed')
    rows = []
    for row in range(1, sheet.nrows):
        cells = [sheet.cell(row,c) for c in range(sheet.ncols)]
        values = [cell_text(c) for c in cells]
        if not any(v.strip() for v in values): continue
        require(values[0].strip() != '', 'facility_name_missing')
        require(not any('\ufffd' in v for v in values), 'workbook_text_invalid')
        require(all(c.ctype in (0,1,2,3,6) for c in cells), 'workbook_cell_type_unsupported')
        rows.append((row+1, values, [{'column':c+1,'header':headers[c],'cellType':cells[c].ctype,
                                    'value':cells[c].value,'text':values[c]} for c in range(sheet.ncols)]))
    require(0 < len(rows) <= 100000, 'workbook_row_count_invalid')
    return sheet.name, headers, rows

def append_snapshot(con, source, raw_path, body, parsed, acquired, manifest_path):
    checksum = digest(body)
    prior = con.execute('SELECT r.sha256 FROM raw_snapshots r JOIN sources s USING(source_key) WHERE s.source_id=? AND s.dataset_id=? AND r.sha256=?',
                        (source['sourceId'],source['datasetId'],checksum)).fetchall()
    require(not prior, 'historical_body_reappeared_requires_review')
    sk = digest(f"{source['sourceId']}|{source['datasetId']}|{checksum}".encode())
    snap = digest(f'{sk}|{checksum}'.encode())
    metadata = {'originalKind':'dataset','rowEvidenceAsOf':None,'redistributionApproved':False,
                'mysqlManifest':manifest_path,'refreshCollector':'logistics-facilities-refresh.v1'}
    con.execute('INSERT INTO sources VALUES(?,?,?,?,?,?,?,?,?)', (sk,source['sourceId'],source['datasetId'],source['id'],'reference',source['url'],acquired,None,compact(metadata)))
    con.execute('INSERT INTO raw_snapshots VALUES(?,?,?,?,?,?)',(snap,sk,raw_path,checksum,len(body),'application/vnd.ms-excel'))
    sheet, headers, observations = parsed
    name, address, business, status, items, measures = SPECS[source['facilityType']]
    for row, values, cells in observations:
        key = digest(f"{source['sourceId']}|{source['datasetId']}|{checksum}|{sheet}|{row}".encode())
        get = lambda index: values[index] if index is not None else None
        addr = get(address)
        con.execute('INSERT INTO facility_observations VALUES('+','.join('?' for _ in range(18))+')',
                    (key,snap,sheet,row,source['facilityType'],get(name),addr,None,get(business),get(status),get(items),
                     addr.split()[0] if addr.split() else None,None,None,'Unverified',None,digest(compact(values).encode()),compact(cells)))
        for column, (code,unit) in measures.items():
            original = values[column]; text = original.strip(); value = None
            state = 'Missing' if not text or text in ('-','해당없음') else 'Unparsed'
            if re.fullmatch(r'[+-]?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?',text):
                value = format(Decimal(text.replace(',','')), 'f'); state = 'Parsed'
            con.execute('INSERT INTO facility_measures VALUES(?,?,?,?,?,?)',(key,code,original,value,state,unit))
        con.executemany('INSERT INTO facility_attributes VALUES(?,?,?,?)',[(key,c+1,h,values[c]) for c,h in enumerate(headers)])
    require(con.execute('SELECT COUNT(*) FROM facility_observations WHERE snapshot_key=?',(snap,)).fetchone()[0] == len(observations), 'snapshot_readback_failed')
    return len(observations)

LATEST_VIEW = '''CREATE VIEW IF NOT EXISTS facility_latest_search AS
 SELECT f.* FROM facility_search f WHERE NOT EXISTS(
 SELECT 1 FROM raw_snapshots r2 JOIN sources s2 USING(source_key)
 WHERE s2.source_id=f.source_id AND s2.dataset_id=f.dataset_id
 AND s2.collected_at_utc>f.collected_at_utc)'''

def validate_policy(root, policy):
    require(policy['schemaVersion'] == 'logistics-facilities-refresh.v1', 'policy_schema_invalid')
    require(0 < policy['maxSourceBytes'] <= 12582912 and policy['maxRunBytes'] <= 25165824, 'policy_size_invalid')
    require(policy['minimumIntervalHours'] >= 48 and 0 < policy['timeoutSeconds'] <= 30, 'policy_interval_invalid')
    require(1 <= policy.get('maxRequests',3) <= 3 and 0 < policy.get('warehouseDiscoveryMaximumBytes',2097152) <= 2097152
            and 0 < policy.get('maximumWarehouseRows',100000) <= 100000, 'policy_request_limit_invalid')
    require(len(policy['sources']) == 3 and len({s['id'] for s in policy['sources']}) == 3
            and {s['facilityType'] for s in policy['sources']}==set(URLS), 'policy_sources_invalid')
    for source in policy['sources']:
        require(source['url'] == URLS.get(source['facilityType']), 'source_url_rejected')
        require(source['group'] == ('warehouses' if source['facilityType']=='RegisteredWarehouse' else 'hubs'), 'source_group_invalid')
        require(isinstance(source['form'],dict) and source['form'].get('command') == 'DWLOAD', 'source_form_invalid')
        require('S_PAGE_LEN' not in source['form'], 'fixed_warehouse_page_length_rejected')
        raw = beneath(root, source['baseline']['rawPath'])
        require(raw.is_file() and digest(raw.read_bytes()) == source['baseline']['sha256'], 'baseline_hash_mismatch')
    beneath(root, policy['outputRoot']); beneath(root, policy['facilityDatabasePath']); beneath(root, policy['xlrdLibraryPath'])

def pending_manifests(state):
    found = {}
    for value in state.values():
        for item in value.get('pendingMysql',[]): found[item['manifestPath']] = item
    return list(found.values())

def acknowledge(root, state, receipt_path):
    if Path(receipt_path).is_absolute():
        try: receipt_path=Path(receipt_path).resolve().relative_to(root).as_posix()
        except ValueError: raise RefreshError('artifact_path_escape') from None
    receipt = read_json(beneath(root,receipt_path))
    require(receipt.get('status')=='completed' and receipt.get('mode')=='verify' and receipt.get('independentReadbackVerified') is True
            and receipt.get('databaseTarget')=='hongdal-mysql-1 / 127.0.0.1:13306 / hongdal_dev', 'mysql_receipt_rejected')
    matches = [m for m in pending_manifests(state) if m['manifestSha256']==receipt.get('manifestSha256')]
    require(len(matches)==1,'mysql_pending_manifest_not_found')
    item = matches[0]; path = beneath(root,item['manifestPath'])
    require(digest(path.read_bytes())==item['manifestSha256'],'mysql_manifest_changed')
    expected = read_json(path)['entries']; actual = receipt.get('entries',[])
    require(receipt.get('entryCount')==len(expected)==len(actual) and
            sorted((e['sourceId'],e['datasetId']) for e in expected)==sorted((e['sourceId'],e['datasetId']) for e in actual)
            and all(e.get('verified') is True and e.get('existing') is True and e.get('normalizedCount')==0 for e in actual), 'mysql_entries_unverified')
    require(all('rawSha256' not in e or e['rawSha256']==next(x['rawSha256'] for x in expected
                if x['sourceId']==e['sourceId'] and x['datasetId']==e['datasetId']) for e in actual),'mysql_raw_hash_mismatch')
    for value in state.values(): value['pendingMysql']=[m for m in value.get('pendingMysql',[]) if m != item]

def recover_commit(root, policy, state, journal_path):
    """SQLite commit 뒤 영수증/state 저장 중 중단돼도 HTTP 없이 pending을 회수한다."""
    if not journal_path.exists(): return state, False
    journal=read_json(journal_path)
    require(journal.get('schemaVersion')=='logistics-facilities-pending-commit.v1','pending_commit_journal_invalid')
    item=journal['manifest'];manifest=beneath(root,item['manifestPath'])
    require(digest(manifest.read_bytes())==item['manifestSha256'],'pending_commit_manifest_changed')
    entries=read_json(manifest)['entries'];expected=journal['snapshots']
    require(len(entries)==len(expected)>0 and all(e['rawSha256']==x['rawSha256'] and e['rawPath']==x['rawPath']
            and e['sourceId']==x['sourceId'] and e['datasetId']==x['datasetId'] for e,x in zip(entries,expected)),
            'pending_commit_entries_changed')
    con=sqlite3.connect(beneath(root,policy['facilityDatabasePath']).as_uri()+'?mode=ro',uri=True)
    found=[]
    try:
        for entry in expected:
            raw=beneath(root,entry['rawPath'])
            require(digest(raw.read_bytes())==entry['rawSha256'],'pending_commit_raw_changed')
            rows=con.execute('''SELECT s.collected_at_utc,s.metadata_json,r.repo_path,
                (SELECT COUNT(*) FROM facility_observations f WHERE f.snapshot_key=r.snapshot_key)
                FROM sources s JOIN raw_snapshots r USING(source_key)
                WHERE s.source_id=? AND s.dataset_id=? AND r.sha256=?''',
                (entry['sourceId'],entry['datasetId'],entry['rawSha256'])).fetchall()
            require(len(rows)<=1,'pending_commit_database_ambiguous')
            if rows:
                acquired,metadata,path,count=rows[0]
                require(acquired==entry['collectedAtUtc'] and path==entry['rawPath'] and count==entry['rowCount']
                        and json.loads(metadata).get('mysqlManifest')==item['manifestPath'],'pending_commit_database_mismatch')
            found.append(bool(rows))
    finally: con.close()
    if not any(found): journal_path.unlink();return state,False  # 직전 transaction이 전부 rollback됨.
    require(all(found),'pending_commit_database_partial')
    require(digest(compact(state).encode()) in (journal['stateBeforeSha256'],digest(compact(journal['stateAfter']).encode())),
            'pending_commit_state_changed')
    atomic_json(beneath(root,journal['receiptPath']),journal['receipt'])
    atomic_json(journal_path.with_name('state.json'),journal['stateAfter'])
    journal_path.unlink()
    return journal['stateAfter'],True

def run(root, policy, group, now, *, dry_run=False, fetcher=fetch, parser=None, ack_receipt=None):
    root = root.resolve(); validate_policy(root,policy)
    output = beneath(root,policy['outputRoot']); state_path = output/'state.json'
    state = read_json(state_path) if state_path.exists() else {s['id']:{'periodKey':s['baseline']['periodKey'],
          'lastCheckedAtUtc':s['baseline']['collectedAtUtc'],'rawPath':s['baseline']['rawPath'],
          'sha256':s['baseline']['sha256'],'collectedAtUtc':s['baseline']['collectedAtUtc'],'pendingMysql':[]} for s in policy['sources']}
    selected = [s for s in policy['sources'] if s['group']==group]
    due = [s for s in selected if is_due(now,group,state[s['id']],policy['minimumIntervalHours'])]
    result = {'status':'dry_run' if dry_run else 'not_due','httpRequests':0,'newSqliteObservations':0,
              'dueSources':[s['id'] for s in due],'pendingMysql':pending_manifests(state),'stateWritten':False}
    journal_path=output/'pending-commit.json'
    if dry_run:
        if journal_path.exists(): result['pendingRecoveryPath']=journal_path.relative_to(root).as_posix()
        require(ack_receipt is None,'dry_run_cannot_acknowledge'); return result
    output.mkdir(parents=True,exist_ok=True)
    with exclusive_lock(output/'refresh.lock'):
        # Re-read after locking; another group may have committed while waiting.
        if state_path.exists(): state=read_json(state_path)
        state,recovered=recover_commit(root,policy,state,journal_path)
        if ack_receipt:
            acknowledge(root,state,ack_receipt);atomic_json(state_path,state)
            return {**result,'status':'mysql_acknowledged','pendingMysql':pending_manifests(state),'stateWritten':True}
        if recovered:
            return {**result,'status':'recovered','dueSources':[],'pendingMysql':pending_manifests(state),'stateWritten':True}
        due = [s for s in selected if is_due(now,group,state[s['id']],policy['minimumIntervalHours'])]
        prepared=[];total=0
        if due: parser=parser or load_parser(root,policy)
        for source in due:
            previous=state[source['id']]; current=beneath(root,previous['rawPath'])
            require(current.is_file() and digest(current.read_bytes())==previous['sha256'],'current_raw_hash_mismatch')
            form=dict(source['form']); declared=None
            if source.get('discoverPageLength'):
                require(result['httpRequests'] < policy.get('maxRequests',3),'request_limit_exceeded')
                maximum=policy.get('warehouseDiscoveryMaximumBytes',2097152)
                page=fetcher(source['url'],None,maximum,policy['timeoutSeconds']);result['httpRequests']+=1;total+=len(page)
                require(len(page)<=maximum and total<=policy['maxRunBytes'],'discovery_size_exceeded')
                declared=warehouse_count(page);require(declared<=policy.get('maximumWarehouseRows',100000),'warehouse_count_limit_exceeded')
                form['S_PAGE_LEN']=str(declared)
            require(result['httpRequests'] < policy.get('maxRequests',3),'request_limit_exceeded')
            body=fetcher(source['url'],form,policy['maxSourceBytes'],policy['timeoutSeconds']);result['httpRequests']+=1;total+=len(body)
            require(len(body)<=policy['maxSourceBytes'] and total<=policy['maxRunBytes'],'response_size_exceeded')
            parsed=parse_workbook(body,source,parser)
            require(declared is None or declared==len(parsed[2]),'warehouse_declared_count_mismatch')
            prepared.append((source,body,parsed, digest(body)!=previous['sha256']))
        changed=[p for p in prepared if p[3]]; manifest_item=None
        if changed:
            folder=output/'runs'/(now.astimezone(UTC).strftime('%Y%m%dT%H%M%S%fZ')+'-'+uuid.uuid4().hex[:8]);folder.mkdir(parents=True)
            entries=[]
            for source,body,parsed,_ in changed:
                path=folder/(source['id']+'.xls');path.write_bytes(body);acquired=stamp(now)
                entries.append({'kind':'reference','sourceId':source['sourceId'],'datasetId':source['datasetId'],'sourceVersion':'public-export-'+acquired[:10],
                    'dataRevision':'raw-sha256:'+digest(body),'collectedAtUtc':acquired,'evidenceAsOfUtc':None,'rawPath':path.relative_to(root).as_posix(),
                    'rawSha256':digest(body),'contentType':'application/vnd.ms-excel','officialUrl':source['url'],'recordsPath':None,'recordsSha256':None})
            manifest=folder/'mysql-reference-manifest.json';atomic_json(manifest,{'schemaVersion':'observer-public-data-db.v1','entries':entries})
            manifest_item={'manifestPath':manifest.relative_to(root).as_posix(),'manifestSha256':digest(manifest.read_bytes())}
        state_after=json.loads(compact(state))
        for source,body,parsed,has_changed in prepared:
            previous=state_after[source['id']]; previous['lastCheckedAtUtc']=stamp(now);previous['periodKey']=period_key(now,group)
            if has_changed:
                entry=next(e for e in entries if e['sourceId']==source['sourceId'] and e['datasetId']==source['datasetId'])
                previous.update({k:entry[k] for k in ('rawPath','collectedAtUtc')});previous['sha256']=entry['rawSha256']
                previous.setdefault('pendingMysql',[]).append(manifest_item)
        if changed:
            receipt={'status':'sqlite_committed_mysql_pending','checkedAtUtc':stamp(now),'group':group,'entries':entries,
                     'newSqliteObservations':sum(len(p[2][2]) for p in changed),'pendingMysql':manifest_item}
            atomic_json(journal_path,{'schemaVersion':'logistics-facilities-pending-commit.v1','manifest':manifest_item,
                        'stateBeforeSha256':digest(compact(state).encode()),'stateAfter':state_after,
                        'snapshots':[{**e,'rowCount':len(p[2][2])} for e,p in zip(entries,changed)],
                        'receiptPath':(folder/'receipt.json').relative_to(root).as_posix(),'receipt':receipt})
            db=beneath(root,policy['facilityDatabasePath']);require(db.is_file(),'facility_database_missing')
            con=sqlite3.connect(db.as_uri()+'?mode=rw',uri=True);con.execute('PRAGMA foreign_keys=ON')
            try:
                with con:
                    con.execute('BEGIN IMMEDIATE')
                    con.execute(LATEST_VIEW)
                    for (source,body,parsed,_),entry in zip(changed,entries):
                        result['newSqliteObservations']+=append_snapshot(con,source,entry['rawPath'],body,parsed,entry['collectedAtUtc'],manifest_item['manifestPath'])
                    require(not con.execute('PRAGMA foreign_key_check').fetchall(),'database_foreign_key_failed')
                    require(con.execute('PRAGMA integrity_check').fetchone()[0]=='ok','database_integrity_failed')
            except Exception:
                journal_path.unlink()  # context manager가 rollback한 실패는 복구 대상 commit이 아니다.
                raise
            finally: con.close()
            atomic_json(folder/'receipt.json',receipt)
        atomic_json(state_path,state_after)
        if changed: journal_path.unlink()
        result.update(status='updated' if changed else 'unchanged' if prepared else 'not_due',
                      dueSources=[s['id'] for s in due],pendingMysql=pending_manifests(state_after),stateWritten=True)
        return result

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--group',choices=['warehouses','hubs'],required=True)
    ap.add_argument('--dry-run',action='store_true');ap.add_argument('--ack-mysql-receipt')
    args=ap.parse_args()
    try:
        root=Path.cwd();policy=read_json(root/POLICY)
        result=run(root,policy,args.group,datetime.now(UTC),dry_run=args.dry_run,ack_receipt=args.ack_mysql_receipt)
        print(json.dumps(result,ensure_ascii=False));return 0
    except RefreshError as error:
        code=str(error);blocked=code in ('collector_busy','xlrd_dependency_missing','historical_body_reappeared_requires_review') or code.startswith('pending_commit_')
        print(json.dumps({'status':'blocked' if blocked else 'failed','errorCode':code},ensure_ascii=False));return 2 if blocked else 1
    except Exception as error:
        print(json.dumps({'status':'failed','errorCode':type(error).__name__},ensure_ascii=False));return 1

if __name__=='__main__':raise SystemExit(main())
