"""Loopback-only synthetic five-flow HTTP evidence; UI completion is verified separately."""
import json,uuid,urllib.request,urllib.error,datetime,pathlib,concurrent.futures,sys
root=pathlib.Path(__file__).resolve().parents[2]
evidence=root/'artifacts/local/neighborhood-flexible-delivery-r22'
C='/api/v1/common/neighborhood-collaborations'; D='/api/v1/common/neighborhood-deliveries'
run='r22-'+uuid.uuid4().hex[:12]; events=[]; flows=[]
def uid(): return str(uuid.uuid4())
def api(path, actor='requester', data=None, port=5542, expected=(200,201,204)):
    req=urllib.request.Request(f'http://127.0.0.1:{port}'+path, data=None if data is None else json.dumps(data,ensure_ascii=False).encode(),headers={'Cookie':'preview-account='+actor,'Content-Type':'application/json'},method='GET' if data is None else 'POST')
    try:
        with urllib.request.urlopen(req,timeout=30) as response: status=response.status; raw=response.read().decode()
    except urllib.error.HTTPError as error: status=error.code; raw=error.read().decode()
    try: value=json.loads(raw) if raw else None
    except json.JSONDecodeError: value=raw[:150]
    events.append({'path':path,'actor':actor,'port':port,'status':status})
    if status not in expected: raise AssertionError((path,status,value))
    return value
start=(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(hours=1)).isoformat().replace("+00:00","Z")
end=(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(hours=3)).isoformat().replace("+00:00","Z")
notice='neighborhood-goods-handover-20261006-v1'
def cmd(work,actor,action,**kwargs):
    work=api(C+'/'+work['stableId'],actor)
    return api(C+'/'+work['stableId']+'/commands',actor,{'clientRequestId':uid(),'expectedRevision':work['revision'],'action':action,**kwargs})
def agreement(method,label):
    p=api('/api/v1/community/posts','owner',{'appKey':'platform','category':'자유·생활','workflowTag':'생활 교류','roleTag':'제공해요','publicNeighborhoodRegionKey':'region:kr:hjd:1126057500','title':run+' '+label,'body':'가상 물품 1개 전달 검증','nickname':'가상 제공자','password':'fixture-only-r22'})
    p=api('/api/v1/community/posts/'+str(p['id']),'owner')
    terms={'summary':label+' 가상 상자','quantity':1,'unit':'개','fromUtc':start,'untilUtc':end,'agreedCostKrw':0,'transferMethod':method,'handoverPlace':'사가정 격리 검증 인계 지점'}
    w=api(C,'requester',{'clientRequestId':uid(),'sourcePostId':p['id'],'expectedSourceUpdatedAtUtc':p['updatedAtUtc'],'kind':'goods','terms':terms})
    owner=api(C+'/'+w['stableId'],'owner'); assert owner['terms'].get('handoverPlace') is None
    for actor in ['owner','requester']: w=cmd(w,actor,'handover-info-consent',consented=True,privacyNoticeVersion=notice)
    for actor in ['owner','requester']: w=cmd(w,actor,'agree',privacyNoticeVersion=notice)
    assert w['statusCode']=='agreed'
    assert api(C+'/'+w['stableId'],'helper',expected=(404,)) is None
    return w

def prepare_request(w,mode):
    consent=uid()
    api('/api/v1/common/application-privacy-consents','requester',{'증적Id':consent,'업무Code':'transport-proxy','출처Code':'neighborhood-exchange','동의문버전':'application-privacy-consent-draft-2026-08-04','수집이용동의':True,'연령요건확인':True})
    def loc(label,num): return {'주소':{'도로명주소':'사가정 가상 '+label,'상세주소':'격리 fixture'},'연락처':{'이름':'가상 담당','전화번호':num},'시간창':{'시작일시':start,'종료일시':end}}
    r={'clientRequestId':uid(),'dispatchMode':mode,'collaborationId':w['stableId'],'expectedTermsRevision':w['termsRevision'],'cargoName':w['terms']['summary'],'quantity':1,'weightKg':1,'vehicleType':'오토바이','pickup':loc('픽업','010-0000-0000'),'dropoff':loc('전달','010-0000-0001'),'sourcePostId':w['sourcePostId'],'privacyConsentEvidenceId':consent,'dispatchRequested':True,'directPaymentAgreed':True,'agreedFareKrw':8200}
    assert api(D+'/quote','requester',r)['automaticDispatchEnabled']
    return r

def shipping(w,mode):
    r=prepare_request(w,mode)
    item=api(D,'requester',r)
    replay=api(D,'requester',r,port=5543); assert item['requestId']==replay['requestId'] and replay['idempotentReplay']
    different=dict(r); different['quantity']=2; api(D,'requester',different,expected=(409,))
    assert api(D+'/'+item['requestId'],'owner',expected=(404,)) is None
    return item,r

if __name__ == "__main__":
    try:
        initial_deliveries=len(api(D+"/mine"))
        for method,label in [('recipient-pickup','직접 수령'),('provider-delivery','직접 전달')]:
            w=agreement(method,label); flows.append({'label':label,'method':method,'workId':w['stableId'],'sourcePostId':w['sourcePostId']})
        assert len(api(D+'/mine'))==initial_deliveries
        for mode,label in [('automatic','자동 추천'),('public-call','공개 콜'),('hybrid','병행')]:
            w=agreement('driver-delivery',label); item,r=shipping(w,mode); rid=item['requestId']
            assert api(C+'/'+w['stableId'])['linkedDeliveryRequestId']==rid
            calls=api('/preview/flex/public','driver-b'); ids={x['의뢰Id'] for x in calls}; assert (rid in ids)==(mode!='automatic')
            if mode!='public-call':
                transition=api('/preview/flex/'+rid+'/recommend','requester',{})
                state=api('/preview/flex/'+rid+'/evidence'); assert state['candidate']=='preview-driver-a'
            else: state=api('/preview/flex/'+rid+'/evidence')
            assert state['ready'] and state['mongo']['mongo원장존재']
            if mode=='hybrid':
                with concurrent.futures.ThreadPoolExecutor() as pool:
                    replies=list(pool.map(lambda x: api('/preview/flex/'+rid+'/accept',x[0],{'round':state['round']},port=x[1],expected=(200,409)), [('driver-a',5542),('driver-b',5543)]))
                after=api('/preview/flex/'+rid+'/evidence'); winner=after['driver'].removeprefix('preview-')
                assert sum('requestId' in x for x in replies)==1, replies
            else:
                winner='driver-a' if mode=='automatic' else 'driver-b'
                api('/preview/flex/'+rid+'/accept',winner,{'round':state['round']})
            after=api('/preview/flex/'+rid+'/evidence'); assert after['driver']=='preview-'+winner
            api(D+'/'+rid+'/dispatch-choice','requester',{'clientRequestId':uid(),'expectedDispatchRevision':item['dispatchRevision'],'dispatchMode':'public-call'},expected=(409,))
            for step in ['pickup-arrive','pickup','dropoff-arrive','dropoff']:
                api('/preview/flex/'+str(after['queueId'])+'/transport/'+step,winner,{})
            delivery=api(D+'/'+rid); assert delivery['dispatchStatusCode']=='인수완료'
            assert delivery['paymentStatusCode']!='결제완료'
            flows.append({'label':label,'method':'driver-delivery','mode':mode,'workId':w['stableId'],'sourcePostId':w['sourcePostId'],'requestId':rid,'queueId':after['queueId'],'driver':winner,'fareFixtureKrw':8200})
        result={'runId':run,'status':'HTTPPreparedAwaitingUICompletion','databases':'isolated real MySQL/Mongo','fixtures':['accounts','geocoding','fare','candidate availability and suitability','route','notification','continuity','photo object evidence'], 'flows':flows,'requests':events}
        (evidence/'http-evidence.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps({'runId':run,'flows':flows},ensure_ascii=False,indent=2))
    except Exception:
        (evidence/'http-failure.json').write_text(json.dumps({'runId':run,'flows':flows,'events':events},ensure_ascii=False,indent=2),encoding='utf-8')
        raise
