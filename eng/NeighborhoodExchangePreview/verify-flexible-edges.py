"""Explicit synthetic failures and cross-server persistence checks, loopback only."""
import importlib.util, pathlib, concurrent.futures, json
spec=importlib.util.spec_from_file_location("flex_verify",pathlib.Path(__file__).with_name("verify-flexible.py"))
f=importlib.util.module_from_spec(spec); spec.loader.exec_module(f)
api,cmd,agreement,shipping,prepare,uid,C,D=f.api,f.cmd,f.agreement,f.shipping,f.prepare_request,f.uid,f.C,f.D
checks=[]
def passed(name,**details): checks.append({"check":name,"passed":True,**details}); print(name, flush=True)
def ev(rid): return api('/preview/flex/'+rid+'/evidence')
def choice(item,mode,client=None):
    payload={'clientRequestId':client or uid(),'expectedDispatchRevision':item['dispatchRevision'],'dispatchMode':mode}
    return api(D+'/'+item['requestId']+'/dispatch-choice',data=payload),payload
try:
    w=agreement('driver-delivery','선택 변경'); item,r=shipping(w,'automatic'); rid=item['requestId']; before=ev(rid)
    item,payload=choice(item,'public-call'); after=ev(rid)
    assert before['queueId']==after['queueId'] and after['round']>before['round']
    replay=api(D+'/'+rid+'/dispatch-choice',data=payload,port=5543); assert replay['dispatchRevision']==item['dispatchRevision']
    api('/preview/flex/'+rid+'/accept','driver-a',{'round':before['round']},expected=(409,))
    api(D+'/'+rid+'/dispatch-choice','owner',{'clientRequestId':uid(),'expectedDispatchRevision':item['dispatchRevision'],'dispatchMode':'hybrid'},expected=(404,))
    item,_=choice(item,'hybrid'); assert ev(rid)['queueId']==before['queueId']
    passed('preaccept-choice-revision-idempotency-stale-accept',requestId=rid)
    terms=dict(api(C+'/'+w['stableId'])['terms']); terms['transferMethod']='recipient-pickup'
    w=cmd(w,'requester','update-terms',terms=terms)
    assert w['statusCode']=='requested' and not w['ownerAgreed'] and not w['requesterAgreed']
    old=ev(rid); assert not old['ready']; api('/preview/flex/'+rid+'/accept','driver-b',{'round':old['round']},expected=(409,))
    for actor in ['owner','requester']: w=cmd(w,actor,'handover-info-consent',consented=True,privacyNoticeVersion=f.notice)
    for actor in ['owner','requester']: w=cmd(w,actor,'agree',privacyNoticeVersion=f.notice)
    for actor,action in [('owner','start'),('owner','propose-completion'),('requester','confirm-completion')]: w=cmd(w,actor,action)
    assert w['statusCode']=='completed' and not w.get('linkedDeliveryRequestId')
    passed('waiting-driver-to-direct-cancels-old-queue',workId=w['stableId'],requestId=rid)

    w=agreement('driver-delivery','인계 동의 철회'); item,r=shipping(w,'public-call'); rid=item['requestId']
    w=cmd(w,'requester','withdraw-handover-consent')
    assert 'start' not in w['allowedActions'] and api(C+'/'+w['stableId'],'owner')['terms'].get('handoverPlace') is None
    assert not ev(rid)['ready']; api(D,data=r,expected=(409,))
    passed('handover-withdrawal-cancels-waiting-queue-and-hides-place',workId=w['stableId'])

    w=agreement('driver-delivery','기사 확정 보호'); item,r=shipping(w,'public-call'); rid=item['requestId']; old=ev(rid)
    api('/preview/flex/'+rid+'/accept','driver-a',{'round':old['round']}); before=api(C+'/'+w['stableId'])
    terms=dict(before['terms']); terms['transferMethod']='provider-delivery'
    error=api(C+'/'+w['stableId']+'/commands','requester',{'clientRequestId':uid(),'expectedRevision':before['revision'],'action':'update-terms','terms':terms},expected=(409,))
    assert error['errorCode']=='DeliveryRecoveryRequired' and api(C+'/'+w['stableId'])['revision']==before['revision']
    released=api('/preview/flex/'+rid+'/release','driver-a',{}); reopened=ev(rid)
    assert reopened['driver'] is None and reopened['candidate'] is None and reopened['mode']=='public-call'
    assert rid in {x['의뢰Id'] for x in api('/preview/flex/public','driver-b')}
    api('/preview/flex/'+rid+'/accept','driver-b',{'round':reopened['round']})
    passed('assigned-change-blocked-public-release-reopens-same-queue',requestId=rid,queueId=reopened['queueId'])

    w=agreement('driver-delivery','동의 증적 철회'); r=prepare(w,'hybrid'); consent=r['privacyConsentEvidenceId']
    api('/preview/flex/privacy/'+consent+'/withdraw',data={}); api(D,data=r,expected=(400,409,))
    assert not api(C+'/'+w['stableId']).get('linkedDeliveryRequestId')
    passed('application-consent-withdrawal-blocks-registration',workId=w['stableId'])

    w=agreement('driver-delivery','부분 실패 복구'); r=prepare(w,'hybrid'); r['notes']='fixture-fail-link'
    api(D,data=r,expected=(409,)); pending=api(C+'/'+w['stableId']); assert pending['deliveryRegistrationPending']
    assert pending['myPendingDeliveryRequest']['clientRequestId']==r['clientRequestId']
    assert api(C+'/'+w['stableId'],'owner').get('myPendingDeliveryRequest') is None
    mine=api(D+'/mine'); item=next(x for x in mine if x.get('collaborationId')==w['stableId']); rid=item['requestId']; held=ev(rid)
    assert not held['ready'] and rid not in {x['의뢰Id'] for x in api('/preview/flex/public','driver-a')}
    api('/preview/flex/'+rid+'/accept','driver-a',{'round':held['round']},expected=(409,))
    resumed=api(D,data=r); assert resumed['requestId']==rid and resumed['idempotentReplay'] and ev(rid)['ready']
    passed('sql-saved-mongo-link-failure-private-retry-and-acceptance-fence',workId=w['stableId'],requestId=rid)

    w=agreement('driver-delivery','동시 재송신'); r=prepare(w,'hybrid')
    with concurrent.futures.ThreadPoolExecutor() as pool:
        replies=list(pool.map(lambda port: api(D,data=r,port=port,expected=(200,409,)),[5542,5543]))
    item=api(D,data=r); rid=item['requestId']
    assert sum(x.get('collaborationId')==w['stableId'] for x in api(D+'/mine'))==1
    assert all(x.get('requestId',rid)==rid for x in replies)
    passed('two-server-concurrent-same-request-one-source-one-queue',workId=w['stableId'],requestId=rid,queueId=ev(rid)['queueId'])

    w=agreement('driver-delivery','자동 후보 부재'); item,r=shipping(w,'automatic'); rid=item['requestId']
    api('/preview/flex/'+rid+'/fixture-no-candidate',data={}); result=api('/preview/flex/'+rid+'/recommend',data={})
    assert ev(rid)['candidate'] is None and rid not in {x['의뢰Id'] for x in api('/preview/flex/public','driver-a')}
    api('/preview/flex/'+rid+'/retry',data={}); assert ev(rid)['candidate'] is None
    passed('automatic-no-candidate-stays-private-and-retries',requestId=rid)

    w=agreement('driver-delivery','병행 후보 부재'); item,r=shipping(w,'hybrid'); rid=item['requestId']
    api('/preview/flex/'+rid+'/fixture-no-candidate',data={}); api('/preview/flex/'+rid+'/recommend',data={})
    assert ev(rid)['candidate'] is None and rid in {x['의뢰Id'] for x in api('/preview/flex/public','driver-a')}
    api('/preview/flex/'+rid+'/retry',data={}); assert ev(rid)['candidate'] is None
    passed('hybrid-no-candidate-keeps-public-open-and-automatic-retry',requestId=rid)

    w=agreement('driver-delivery','추천 거절'); item,r=shipping(w,'automatic'); rid=item['requestId']
    api('/preview/flex/'+rid+'/recommend',data={}); old=ev(rid)
    api('/preview/flex/'+rid+'/decline','driver-a',{}); now=ev(rid)
    assert now['candidate']=='preview-driver-b' and now['round']>old['round']
    api('/preview/flex/'+rid+'/accept','driver-a',{'round':old['round']},expected=(409,))
    passed('recommendation-decline-invalidates-round-and-recommends-next',requestId=rid)
    result={'runId':f.run,'passed':True,'checks':checks,'requests':f.events,'fixtures':'Explicit local synthetic accounts, route, fare, eligibility, external notification and injected partial failure'}
    (f.evidence/'edge-evidence.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
except Exception:
    (f.evidence/'edge-failure.json').write_text(json.dumps({'runId':f.run,'checks':checks,'requests':f.events},ensure_ascii=False,indent=2),encoding='utf-8')
    raise
