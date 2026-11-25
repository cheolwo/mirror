"""인허가 갱신의 기관 범위·전체 건수·요청 예산·실패 후 주기 검증."""
import copy
from datetime import timedelta
import json
from pathlib import Path
import tempfile
import unittest
from urllib.parse import parse_qs

import collect_public_snapshots as collector
from test_collect_public_snapshots import Client, Response, NOW
from test_seoul_license_csv import SOURCE, ROW, body

COUNT_URL = 'https://data.seoul.go.kr/dataList/dataView.do?onepagerow=1&infId=OA-23056&filterCol=OPNSFTEAMCODE&txtFilter=3060000'
DOWNLOAD_URL = 'https://datafile.seoul.go.kr/bigfile/iot/sheet/csv/download.do'

def source():
    return {**copy.deepcopy(SOURCE), 'id':'test-license', 'sourceId':'seoul-open-data',
        'datasetId':'OA-23056', 'kind':'dataset', 'status':'ready', 'url':DOWNLOAD_URL,
        'method':'POST', 'validator':'seoul_business_license_csv', 'intervalHours':24,
        'attemptCooldownHours':24, 'maxRecords':100, 'maxBytes':10000,
        'countDiscovery':{'url':COUNT_URL,'maxBytes':10000},
        'form':{'infId':'OA-23056','filterCol':'OPNSFTEAMCODE','txtFilter':'3060000',
                'gridTotalCnt':'__DISCOVERED_COUNT__','ssUserId':'SAMPLE_VIEW'}}

class SeoulLicenseDiscoveryTests(unittest.TestCase):
    def collect(self, folder, client, *, item=None, now=NOW, requests=2, dry_run=False):
        item = source() if item is None else item
        budget={'requests':0,'bytes':0}
        result=collector.collect_source(item,Path(folder)/'license',[DOWNLOAD_URL,COUNT_URL],
            {'maxRequests':requests,'maxTotalBytes':20000,'timeoutSeconds':1},now,budget,
            open_client=client,resolve_dns=False,dry_run=dry_run)
        return result,budget

    def test_discovered_count_used_in_post_and_bound_to_saved_body(self):
        raw=body([ROW,{**ROW,'관리번호':'second'}])
        client=Client(Response(b'{totalCount:2}',url=COUNT_URL),Response(raw,url=DOWNLOAD_URL))
        with tempfile.TemporaryDirectory() as folder:
            result,budget=self.collect(folder,client)
            self.assertEqual(('saved',2,2),(result['status'],result['recordCount'],budget['requests']))
            form=parse_qs(client.calls[1][0].data.decode())
            self.assertEqual(['2'],form['gridTotalCnt'])
            saved=json.loads((Path(folder)/'license/latest.json').read_text(encoding='utf-8'))
            self.assertEqual(2,saved['countDiscovery']['recordCount'])
            self.assertEqual(raw,(Path(folder)/'license'/saved['body']).read_bytes())

    def test_count_mismatch_never_updates_latest(self):
        client=Client(Response(b'{totalCount:2}',url=COUNT_URL),Response(body(),url=DOWNLOAD_URL))
        with tempfile.TemporaryDirectory() as folder:
            result,budget=self.collect(folder,client)
            self.assertEqual('license_discovery_record_count_mismatch',result['reason'])
            self.assertFalse((Path(folder)/'license/latest.json').exists())

    def test_invalid_count_prevents_download_and_attempt_is_cooled_down(self):
        for raw in [b'{totalCount:101}',b'{totalCount:0}',b'{totalCount:1.5}',b'{totalCount:1x}',b'{totalCount:-1}',b'{totalCount:1,totalCount:1}',b'<html>no count</html>']:
            with self.subTest(raw=raw), tempfile.TemporaryDirectory() as folder:
                client=Client(Response(raw,url=COUNT_URL))
                result,budget=self.collect(folder,client)
                self.assertEqual('license_count_invalid_or_exceeds_limit',result['reason'])
                self.assertEqual(1,len(client.calls))
                second,_=self.collect(folder,Client(),now=NOW+timedelta(minutes=30))
                self.assertEqual(('not_due','license_attempt_cooldown'),(second['status'],second['reason']))
                after,_=self.collect(folder,Client(Response(b'{totalCount:1}',url=COUNT_URL),Response(body(),url=DOWNLOAD_URL)),now=NOW+timedelta(hours=24))
                self.assertEqual('saved',after['status'])

    def test_mismatched_agency_missing_discovery_and_short_interval_reject_before_http(self):
        cases=[({'expectedAgencyCode':'3040000'},'license_count_scope_mismatch'),
               ({'countDiscovery':None},'license_count_discovery_required'),
               ({'intervalHours':0.5},'license_interval_too_short'),
               ({'attemptCooldownHours':0.5},'invalid_license_attempt_cooldown')]
        for changes,code in cases:
            with self.subTest(changes=changes),tempfile.TemporaryDirectory() as folder:
                client=Client(); result,budget=self.collect(folder,client,item={**source(),**changes})
                self.assertEqual(code,result['reason']); self.assertFalse(client.calls)

    def test_two_request_budget_required_before_any_http(self):
        with tempfile.TemporaryDirectory() as folder:
            client=Client(); result,budget=self.collect(folder,client,requests=1)
            self.assertEqual('job_request_or_byte_budget_reached',result['reason'])
            self.assertFalse(client.calls); self.assertEqual(0,budget['requests'])

    def test_success_interval_and_dry_run_do_not_fetch(self):
        with tempfile.TemporaryDirectory() as folder:
            result,budget=self.collect(folder,Client(),dry_run=True)
            self.assertEqual('dry_run',result['status']); self.assertFalse((Path(folder)/'license').exists())
            self.collect(folder,Client(Response(b'{totalCount:1}',url=COUNT_URL),Response(body(),url=DOWNLOAD_URL)))
            result,budget=self.collect(folder,Client(),now=NOW+timedelta(hours=23))
            self.assertEqual('not_due',result['status']); self.assertEqual(0,budget['requests'])

    def test_policy_fingerprint_covers_exact_license_contract(self):
        original=source(); fingerprint=collector.policy_fingerprint(original,NOW)
        for key,value in [('expectedAgencyCode','3040000'),('encoding','cp949'),('maxRecords',99),
                          ('maxBytes',9999),('attemptCooldownHours',48),('intervalHours',48),
                          ('exactHeader',['changed']),('countDiscovery',{'url':COUNT_URL+'&extra=1','maxBytes':10000})]:
            self.assertNotEqual(fingerprint,collector.policy_fingerprint({**original,key:value},NOW),key)

if __name__=='__main__': unittest.main()
