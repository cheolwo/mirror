// 기존 주문 작성/상세 Razor와 역할 adapter가 실제 격리 서버를 변경하고 재조회합니다.
// 로그인 세션/운행 준비는 합성 fixture이며 실제 로그인 화면·지도·휴대폰 증거가 아닙니다.
const {chromium}=require('playwright');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const root=path.resolve(process.env.FOUR_ROLE_R27_ARTIFACTS||'artifacts/local/food-mobile-four-role-r27');
const out=path.join(root,'real-ui');fs.mkdirSync(out,{recursive:true});
const prepared=JSON.parse(fs.readFileSync(path.join(root,'food-ui-preparation.json'),'utf8').replace(/^\uFEFF/,''));
const host='http://127.0.0.1:5397',results=[],errors=[],checks=[];
const resumeOrder=process.env.FOUR_ROLE_R27_RESUME_ORDER;
assert.equal(prepared.baseUrl,'http://127.0.0.1:5363/');assert.equal(prepared.orderCreated,false);
let browser,page,orderNo;
async function eventually(predicate,message,ms=45000){const until=Date.now()+ms;while(Date.now()<until){if(await predicate())return;await new Promise(r=>setTimeout(r,200));}throw new Error(message);}
async function httpRows(){return(await(await page.request.get(host+'/verification/http-evidence')).json()).rows;}
async function capture(name){
 for(const width of [390,320]){
  await page.setViewportSize({width,height:844});await page.waitForTimeout(100);
  const bounds=await page.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth}));
  assert.ok(bounds.scroll<=bounds.width+1,name+' horizontal overflow at '+width);
  await page.screenshot({path:path.join(out,name+'-'+width+'.png'),fullPage:true});
  results.push({name,width,url:page.url(),atUtc:new Date().toISOString(),...bounds});
 }
 await page.setViewportSize({width:390,height:844});
}
async function workspace(role,title){
 await page.goto(host+'/workspace/'+role+'?selected='+encodeURIComponent(orderNo));
 await page.getByRole('heading',{name:title,exact:true}).waitFor();
 await page.waitForFunction(()=>!document.body.innerText.includes('현재 업무를 불러오는 중입니다.'));
 assert.equal(await page.locator('.role-map-workspace__error').count(),0,'workspace API failure');
 const list=page.getByRole('button',{name:'목록',exact:true});if(await list.count())await list.click();
}
async function command(label,suffix){
 let button=page.getByRole('button',{name:label,exact:true});
 if(!await button.count() && await page.getByRole('button',{name:'상세',exact:true}).count())await page.getByRole('button',{name:'상세',exact:true}).click();
 await button.waitFor();await eventually(()=>button.isEnabled(),label+' disabled');
 const start=(await httpRows()).length;await button.click();
 const confirm=page.locator('.role-map-workspace__confirmation').getByRole('button',{name:'확인',exact:true});
 if(await confirm.waitFor({timeout:1800}).then(()=>true).catch(()=>false))await confirm.click();
 await eventually(async()=>(await httpRows()).slice(start).some(x=>x.method==='POST'&&x.path.endsWith(suffix)&&x.status===200),label+' successful command missing');
 await page.waitForFunction(()=>!document.body.innerText.includes('처리 결과를 확인하고 있습니다.'));
 assert.equal(await page.locator('.role-map-workspace__error').count(),0,label+' workspace error');
 checks.push({name:label,status:'Passed',postSuffix:suffix});
}
async function ordererDetail(){
 await page.goto(host+'/verification/four-role/orderer/orders/'+encodeURIComponent(orderNo));
 await page.locator('.food-order-detail-heading').waitFor();
 await page.waitForFunction(()=>!document.body.innerText.includes('주문 상세를 불러오고 있습니다.'));
}
async function run(){
 browser=await chromium.launch({headless:true,executablePath:process.env.COMMERCE_BROWSER_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
 const context=await browser.newContext({viewport:{width:390,height:844},reducedMotion:'reduce'});
 await context.route('**/*',route=>new URL(route.request().url()).hostname==='127.0.0.1'?route.continue():route.abort());
 page=await context.newPage();page.setDefaultTimeout(20000);page.on('pageerror',e=>errors.push(e.message));
 if(resumeOrder){
  assert.match(resumeOrder,/^FOOD-\d+$/);
  const prior=JSON.parse(fs.readFileSync(path.join(root,'real-ui-before-dispatch-fix','verification.json'),'utf8'));
  assert.equal(prior.orderNo,resumeOrder);
  assert.ok(prior.checks.some(x=>x.name==='Actual composer order creation and fast repeated click'&&x.status==='Passed'));
  orderNo=resumeOrder;
  results.push(...prior.results.filter(x=>/^0[123]-/.test(x.name)));
  checks.push(...prior.checks);
  checks.push({name:'Resume the same UI-created order after scoped dispatch fix',status:'Passed',priorEvidence:'real-ui-before-dispatch-fix/verification.json'});
 }else{
 await page.goto(host+'/workspace/orderer/new');
 await page.getByRole('button',{name:prepared.menuName+' 수량 늘리기',exact:true}).waitFor();
 await page.getByRole('button',{name:prepared.menuName+' 수량 늘리기',exact:true}).click();
 await page.getByLabel('수령인 이름',{exact:true}).fill('시험 수령인');
 await page.getByLabel('수령인 연락처',{exact:true}).fill('000-0000-0000');
 await page.getByLabel('배달 주소',{exact:true}).fill(prepared.recipientAddress);
 await page.getByLabel('상세 주소',{exact:true}).fill('시험 상세 주소');
 await page.getByLabel('전달 요청사항',{exact:true}).fill('모바일 화면 검증용 요청');
 await page.getByRole('checkbox',{name:'거래 당사자와 거래·개인정보 안내를 확인했습니다.',exact:true}).check();
 await capture('01-order-composer');
 const beforeCreate=(await httpRows()).length;
 await page.getByRole('button',{name:'주문 제출',exact:true}).click({clickCount:2});
 await page.getByRole('button',{name:'주문 상세 보기',exact:true}).click();
 await page.waitForURL('**/verification/four-role/orderer/orders/*');
 orderNo=decodeURIComponent(new URL(page.url()).pathname.split('/').pop());
 assert.ok(orderNo.length>0);await page.locator('.food-order-detail-heading').waitFor();
 const createRows=(await httpRows()).slice(beforeCreate).filter(x=>x.method==='POST'&&x.path==='/api/v1/food-orders');
 assert.equal(createRows.length,1,'fast repeated click created multiple HTTP submissions');assert.equal(createRows[0].status,200);
 checks.push({name:'Actual composer order creation and fast repeated click',status:'Passed',orderNo});
 await capture('02-orderer-pending');
 await workspace('restaurant','음식점');
 await page.getByRole('link',{name:'주문 수락',exact:true}).click();
 await page.getByLabel('음식점 이름',{exact:true}).fill('사가정 한상 샘플');
 await page.getByLabel('픽업 주소',{exact:true}).fill(prepared.pickupAddress);
 await page.getByLabel('이번 주문 조리 예상 시간(분)',{exact:true}).fill('1');
 await page.getByRole('button',{name:'요청 내용 확인',exact:true}).click();
 await page.getByRole('checkbox',{name:'대상과 요청 내용을 확인했습니다.'}).check();
 await page.getByRole('button',{name:'확인하고 요청',exact:true}).click();
 await page.getByText('요청이 반영된 업무 상태를 확인했습니다.',{exact:true}).waitFor();
 await capture('03-restaurant-accepted');
 }
 await workspace('restaurant','음식점');
 const earlyCook=page.getByRole('button',{name:'조리 시작',exact:true});
 assert.equal(await earlyCook.count()?await earlyCook.isEnabled():false,false,'Cooking allowed before driver acceptance');
 await capture('04-cooking-gate-before-assignment');
 await workspace('food-driver','음식기사');
 await eventually(async()=>{
  if(await page.getByRole('button',{name:'배달 수락',exact:true}).count())return true;
  const refresh=page.getByRole('button',{name:'새로고침',exact:true});if(await refresh.count()&&await refresh.isEnabled())await refresh.click();
  return false;
 },'Scoped server worker did not offer delivery',90000);
 await capture('05-server-offer');await command('배달 수락','/accept');await capture('06-driver-assigned');
 await workspace('restaurant','음식점');await command('조리 시작','/restaurant-progress');await capture('07-cooking');
 await command('픽업 준비 완료','/restaurant-progress');await capture('08-ready');
 await workspace('food-driver','음식기사');
 if(await page.getByRole('button',{name:'음식점 도착',exact:true}).count())await command('음식점 도착','/restaurant-arrival');
 await capture('09-driver-arrived');await command('음식 픽업 확인','/pickup-complete');await capture('10-picked-up');
 const beforeReentry=(await httpRows()).length;
 await page.reload();await page.getByRole('button',{name:'고객 전달 완료',exact:true}).waitFor();
 assert.equal((await httpRows()).slice(beforeReentry).filter(x=>x.method==='POST'&&/\/(accept|restaurant-arrival|pickup-complete|delivery-complete)$/.test(x.path)).length,0,'Reentry repeated a business command');
 checks.push({name:'Picked-up page reentry has no automatic business POST',status:'Passed'});
 await command('고객 전달 완료','/delivery-complete');await capture('11-delivered');
 await ordererDetail();const receipt=page.getByRole('button',{name:'음식 수령 확인',exact:true});
 assert.equal(await receipt.count(),1,'Receipt action must appear once');await capture('12-receipt-waiting');
 await command('음식 수령 확인','/receipt-confirmation');await capture('13-receipt-confirmed');
 await page.goto(host+'/food-operations/orders/'+encodeURIComponent(orderNo));
 await page.getByRole('heading',{name:'음식 주문 확인',exact:true}).waitFor();
 await page.getByText('다음 담당:',{exact:false}).waitFor();await capture('14-operator-completed');
 const inject=await page.request.post(host+'/verification/fail-next-admin-trace');assert.equal(inject.status(),204);
 await page.getByRole('button',{name:'최신 상태 확인',exact:true}).click();
 await page.getByText('최신 음식 주문을 확인하지 못했습니다. 다시 조회해 주세요.',{exact:true}).waitFor();
 await page.locator('[data-food-order-stale-trace]').waitFor();
 await page.getByText('이전 확인 시 담당:',{exact:false}).waitFor();
 assert.ok(await page.getByText(orderNo,{exact:true}).count(),'Failed refresh discarded retained trace');
 await capture('15-operator-stale-after-failure');
 await page.getByRole('button',{name:'최신 상태 확인',exact:true}).click();
 await eventually(async()=>await page.getByText('최신 음식 주문을 확인하지 못했습니다. 다시 조회해 주세요.',{exact:true}).count()===0,'Admin refresh did not recover');
 await capture('16-operator-refreshed');checks.push({name:'Actual admin Razor retains and labels stale trace after injected transport GET failure, then refreshes',status:'Passed',failureInjection:'Preview HTTP handler returned a single 503; product server unmodified'});
 await page.goto(host+'/workspace/food-driver/history');await page.getByRole('heading',{name:'완료 배달 내역',exact:true}).waitFor();
 await page.locator('.rw-row').first().waitFor();await capture('17-driver-history');
 await page.locator('.rw-row').first().click();await page.getByRole('heading',{name:'내역 상세',exact:true}).waitFor();await capture('18-driver-history-detail');
 const evidence=await(await page.request.get(host+'/verification/http-evidence')).json();
 fs.writeFileSync(path.join(out,'http-evidence.json'),JSON.stringify(evidence,null,2));
 assert.equal(errors.length,0,errors.join('\n'));
}
run().then(()=>console.log(JSON.stringify({status:'Completed',orderNo,screenshots:results.length,checks:checks.length})))
 .catch(e=>{errors.push(e.message);console.error(e.message);process.exitCode=1;})
 .finally(async()=>{
  if(page)try{await page.screenshot({path:path.join(out,'last-state.png'),fullPage:true});fs.writeFileSync(path.join(out,'last-state.txt'),await page.locator('body').innerText());fs.writeFileSync(path.join(out,'http-evidence.json'),JSON.stringify(await(await page.request.get(host+'/verification/http-evidence')).json(),null,2));}catch{}
  if(browser)await browser.close();
  fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({schemaVersion:'actual-shared-food-ui.r27',status:errors.length?'Failed':'Completed',orderNo,restaurantId:101,results,checks,errors,orderCreatedByActualRoleClient:true,orderCreateUiVerified:!!orderNo,sharedRazorApiVerified:!errors.length,serverAutomatedOfferVerified:checks.some(x=>x.postSuffix==='/accept'),adminRazorLinkedFromProductSource:true,authenticationAdapterSynthetic:true,driverPreparedByApi:true,physicalDeviceVerified:false,actualGpsVerified:false,externalMapVerified:false,realPaymentVerified:false},null,2));
 });
