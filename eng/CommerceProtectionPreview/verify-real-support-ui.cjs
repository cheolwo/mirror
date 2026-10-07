const {chromium}=require('playwright');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const artifact=path.join(process.cwd(),'artifacts/local/apk-completion-r26');
const out=path.join(artifact,'real-support-ui');fs.mkdirSync(out,{recursive:true});
const orderNo=JSON.parse(fs.readFileSync(path.join(artifact,'food-ui-preparation.json'),'utf8').replace(/^\uFEFF/,'')).orderNo;
const privateSessions=JSON.parse(fs.readFileSync(path.join(artifact,'server/.private/ui-sessions.json'),'utf8'));
const rows=[],errors=[];let browser,caseId;
async function api(role,url,body){
 const token=privateSessions.accounts.find(x=>x.role===role).accessToken;
 const response=await fetch('http://127.0.0.1:5362/'+url,{method:body?'POST':'GET',headers:{Authorization:'Bearer '+token,...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined});
 const json=await response.json().catch(()=>({}));return {status:response.status,json};
}
async function run(){
 browser=await chromium.launch({headless:true,executablePath:process.env.COMMERCE_BROWSER_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
 const context=await browser.newContext({viewport:{width:390,height:844},reducedMotion:'reduce'});
 await context.route('**/*',r=>new URL(r.request().url()).hostname==='127.0.0.1'?r.continue():r.abort());
 const page=await context.newPage();page.setDefaultTimeout(15000);page.on('pageerror',e=>errors.push(e.message));
 async function capture(name){const bounds=await page.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth}));assert.ok(bounds.scroll<=bounds.width+1,name+' overflow');await page.screenshot({path:path.join(out,name+'.png'),fullPage:true});rows.push({name,url:page.url(),atUtc:new Date().toISOString(),...bounds});}
 async function open(url,heading){await page.goto('http://127.0.0.1:5396'+url);await page.getByRole('heading',{name:heading,exact:true}).waitFor();}
 async function adminAction(action,options={}){
  await page.locator('#support-action').selectOption(action);
  if(options.summary)await page.locator('#support-summary').fill(options.summary);
  if(options.evidence)await page.locator('#support-evidence').fill(options.evidence);
  const before=(await api('operator',`api/v1/admin/privacy-support/cases/${caseId}`)).json.revision;
  await page.locator('button.support-primary').click();
  if(options.blocked){await page.getByRole('alert').waitFor();const current=await api('operator',`api/v1/admin/privacy-support/cases/${caseId}`);assert.equal(current.json.revision,before);return;}
  await eventually(async()=>{const next=await api('operator',`api/v1/admin/privacy-support/cases/${caseId}`);return next.json.revision>before;},'Actual support command not persisted');
  await page.waitForTimeout(150);
 }
 if(!process.env.REAL_SUPPORT_RESUME){
 await open(`/commerce/disputes?sourceKind=food-order&sourceId=${encodeURIComponent(orderNo)}`,'거래 문제 신고');
 await page.getByLabel('문제 내용',{exact:true}).fill('r26 합성 거래 확인 요청: 공개 응답에는 비공개 상세가 포함되면 안 됩니다.');
 await page.getByRole('button',{name:'요청 접수',exact:true}).click();await page.getByRole('heading',{name:'접수됨',exact:true}).waitFor();
 let list=await api('orderer','api/v1/common/transaction-disputes');assert.equal(list.status,200);
 caseId=list.json.items.find(x=>x.sourceId===orderNo).caseId;await capture('01-user-created');
 await open(`/commerce/disputes/${encodeURIComponent(caseId)}`,'거래 문제 신고');
 await page.getByLabel('추가로 알릴 내용',{exact:true}).fill('r26 합성 추가 비공개 근거: 입력 누락 없이 같은 사건에 연결합니다.');
 await page.getByRole('button',{name:'내용 추가',exact:true}).click();await page.getByText('요청을 접수했습니다. 처리 진행을 확인할 수 있습니다.',{exact:true}).waitFor();await capture('02-user-additional');
 const general=await api('orderer',`api/v1/common/transaction-disputes/${caseId}`);assert.equal(general.status,200);assert.ok(!JSON.stringify(general.json).includes('입력 누락 없이'),'private text leaked in general response');
 await open(`/privacy-support/${encodeURIComponent(caseId)}`,'사건 처리');await page.locator('#support-action').waitFor();
 await adminAction('assign-self');await capture('03-admin-assigned');await adminAction('start-review');await capture('04-admin-reviewing');
 await adminAction('prepare-progress',{summary:'r26 합성 진행 안내문. 외부 발송 실행 없음.'});await capture('05-progress-prepared');
 await adminAction('prepare-result',{summary:'r26 합성 처리방안 안내문. 외부 발송 실행 없음.'});await capture('06-result-prepared');
 await adminAction('close',{blocked:true});await capture('07-close-blocked-without-notice');
 await page.getByRole('button',{name:'최신 상태 조회',exact:true}).click();await page.locator('#support-action').waitFor();
 }else{
  caseId=process.env.REAL_SUPPORT_RESUME;
  await open(`/privacy-support/${encodeURIComponent(caseId)}`,'사건 처리');await page.locator('#support-action').waitFor();
 }
 // 확인란·참조는 모두 명시적 합성 시험 표식입니다. 실제 이메일·메시지 발송 증거로 승격하지 않습니다.
 let recipients=[];
 if(!process.env.REAL_SUPPORT_RESUME_DONE){
 recipients=(await api('operator',`api/v1/admin/privacy-support/cases/${caseId}/evidence?reasonCode=case-review`)).json.authorizedRecipientIds;
 assert.ok(recipients.length>=2);
 for(let i=0;i<recipients.length;i++){
  await page.locator('#support-action').selectOption('record-result-notified');
  await page.getByRole('button',{name:'담당자 권한으로 통지 대상 확인',exact:true}).click();
  await eventually(async()=>await page.locator('#support-recipient option').count()>i+1,'recipient audit incomplete');
  await page.locator('#support-recipient').selectOption(String(i));
  await page.locator('#support-delivery-ref').fill(`r26-SYNTHETIC-NOTICE-PROOF-${i+1}-NO-EXTERNAL-SEND`);
  const current=(await api('operator',`api/v1/admin/privacy-support/cases/${caseId}`)).json;
  const time=new Date(current.serverNowUtc);time.setSeconds(time.getSeconds()-1);
  const local=new Date(time.getTime()+9*3600000).toISOString().slice(0,19);
  await page.locator('#support-occurred').fill(local);
  await page.getByRole('checkbox',{name:'실제 전달 또는 기관 접수를 증빙으로 확인했습니다.'}).check();
  await adminAction('record-result-notified');
 }
 await capture('08-synthetic-noticeproofs');await adminAction('close');await capture('09-admin-closed');
 await open(`/commerce/disputes/${caseId}`,'거래 문제 신고');await page.getByRole('heading',{name:'처리 종결',exact:true}).waitFor();
 await page.getByLabel('추가로 알릴 내용',{exact:true}).fill('r26 합성 이의제기: 처리 완료 사건의 재검토를 요청합니다.');
 await page.getByRole('button',{name:'이의제기 접수',exact:true}).click();await page.getByRole('heading',{name:'이의제기 접수',exact:true}).waitFor();await capture('10-user-appeal');
 }
 const final=await api('operator',`api/v1/admin/privacy-support/cases/${caseId}`);assert.equal(final.json.statusCode,'reopened');
 const history=final.json.history.map(x=>x.action);
 assert.ok(['create','add-evidence','assign-self','start-review','prepare-result','close','appeal'].every(x=>history.includes(x)));
 fs.writeFileSync(path.join(out,'case-summary.json'),JSON.stringify({orderNo,caseId,statusCode:final.json.statusCode,revision:final.json.revision,history,syntheticNoticeProofCount:final.json.notifications.length,externalNotificationsSent:false,generalResponsePrivateTextHidden:true},null,2));
 const http=await (await page.request.get('http://127.0.0.1:5396/verification/http-evidence')).json();fs.writeFileSync(path.join(out,'http-evidence.json'),JSON.stringify(http,null,2));
 for(const width of [320,390]){await page.setViewportSize({width,height:844});await capture('11-user-appeal-'+width);await open(`/privacy-support/${caseId}`,'사건 처리');await page.locator('#support-action').waitFor();await capture('12-admin-reopened-'+width);await open(`/commerce/disputes/${caseId}`,'거래 문제 신고');}
 assert.equal(errors.length,0,errors.join('\n'));
}
async function eventually(predicate,message){const end=Date.now()+15000;while(Date.now()<end){if(await predicate())return;await new Promise(r=>setTimeout(r,150));}throw new Error(message);}
run().then(()=>console.log(JSON.stringify({status:'Completed',orderNo,caseId,screenshots:rows.length,errors:errors.length})))
 .catch(e=>{errors.push(e.message);console.error(e.message);process.exitCode=1;})
 .finally(async()=>{if(browser)await browser.close();fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({schemaVersion:'actual-shared-support-ui.r26',orderNo,caseId,status:errors.length?'Failed':'Completed',rows,errors,sharedRazorClientHttpMongoEvidence:true,externalNotificationVerified:false,syntheticNoticeProofsOnly:true,physicalDeviceVerified:false,productionVerified:false},null,2));});
