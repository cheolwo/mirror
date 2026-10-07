// 기존 공유 Razor와 기존 역할 adapter/client가 실제 격리 서버를 변경·재조회하는 검증입니다.
const {chromium}=require('playwright');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const out=path.join(process.cwd(),'artifacts/local/apk-completion-r26/real-ui');
fs.mkdirSync(out,{recursive:true});
const prepared=JSON.parse(fs.readFileSync(path.join(out,'../food-ui-preparation.json'),'utf8').replace(/^\uFEFF/,''));
const orderNo=prepared.orderNo,results=[],errors=[];
const executable=process.env.COMMERCE_BROWSER_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
let browser;
async function run(){
 browser=await chromium.launch({headless:true,executablePath:executable});
 const context=await browser.newContext({viewport:{width:390,height:844},reducedMotion:'reduce'});
 await context.route('**/*',r=>new URL(r.request().url()).hostname==='127.0.0.1'?r.continue():r.abort());
 const page=await context.newPage();
 page.setDefaultTimeout(15000);
 page.on('pageerror',e=>errors.push(e.message));
 async function capture(name){
  const bounds=await page.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth}));
  assert.ok(bounds.scroll<=bounds.width+1,name+' horizontal overflow');
  await page.screenshot({path:path.join(out,name+'.png'),fullPage:true});
  results.push({name,url:page.url(),atUtc:new Date().toISOString(),...bounds});
 }
 async function workspace(role,text){
  await page.goto(`http://127.0.0.1:5396/workspace/${role}?selected=${encodeURIComponent(orderNo)}`);
  await page.getByRole('heading',{name:text,exact:true}).waitFor();
  await page.waitForFunction(()=>!document.body.innerText.includes('현재 업무를 불러오는 중입니다.'));
  assert.equal(await page.locator('.role-map-workspace__error').count(),0,'Workspace API error');
  if(await page.getByRole('button',{name:'목록',exact:true}).count()) await page.getByRole('button',{name:'목록',exact:true}).click();
 }
 async function command(label){
  const before=(await (await page.request.get('http://127.0.0.1:5396/verification/http-evidence')).json()).rows.length;
  const button=page.getByRole('button',{name:label,exact:true});
  if(!await button.count() && await page.getByRole('button',{name:'상세',exact:true}).count()) await page.getByRole('button',{name:'상세',exact:true}).click();
  await button.waitFor(); await assertEventually(()=>button.isEnabled(),label+' disabled');
  await button.click();
  const confirm=page.locator('.role-map-workspace__confirmation').getByRole('button',{name:'확인',exact:true});
  if(await confirm.waitFor({timeout:1500}).then(()=>true).catch(()=>false)) await confirm.click();
  await assertEventually(async()=>{const rows=(await (await page.request.get('http://127.0.0.1:5396/verification/http-evidence')).json()).rows.slice(before);return rows.some(x=>x.method==='POST'&&x.status===200);},label+' actual POST missing');
  await page.waitForTimeout(150);
  await page.waitForFunction(()=>!document.body.innerText.includes('처리 결과를 확인하고 있습니다.'));
  assert.equal(await page.locator('.role-map-workspace__error').count(),0,label+' API error');
 }
 if(!process.env.REAL_UI_RESUME){
 await workspace('orderer','주문자'); await capture('01-orderer-pending');
 await workspace('restaurant','음식점'); await page.getByRole('link',{name:'주문 수락',exact:true}).click();
 await page.getByLabel('음식점 이름',{exact:true}).fill('관찰 검증 음식점');
 await page.getByLabel('픽업 주소',{exact:true}).fill('검증 표본 음식점');
 await page.getByLabel('조리 예상 시간(분)',{exact:true}).fill('1');
 await page.getByRole('button',{name:'요청 내용 확인',exact:true}).click();
 await page.getByRole('checkbox',{name:'대상과 요청 내용을 확인했습니다.'}).check();
 await page.getByRole('button',{name:'확인하고 요청',exact:true}).click();
 await page.getByText('요청이 반영된 업무 상태를 확인했습니다.',{exact:true}).waitFor();
 await capture('02-restaurant-accepted');
 await workspace('restaurant','음식점');
 const earlyCook=page.getByRole('button',{name:'조리 시작',exact:true});
 assert.equal(await earlyCook.count()?await earlyCook.isEnabled():false,false,'Cooking before assigned');
 await capture('03-cooking-gate-before-assignment');
 }
 if(process.env.REAL_UI_RESUME!=='cooking'){
 await workspace('food-driver','음식기사');
 await command('배달 수락'); await capture('04-driver-assigned');
 }
 await workspace('restaurant','음식점'); await command('조리 시작'); await capture('05-cooking');
 await command('픽업 준비 완료'); await capture('06-ready');
 await workspace('food-driver','음식기사');
 if(await page.getByRole('button',{name:'음식점 도착',exact:true}).count()) await command('음식점 도착');
 await command('음식 픽업 확인'); await capture('07-picked-up');
 await command('고객 전달 완료'); await capture('08-delivered');
 await workspace('orderer','주문자'); await command('음식 수령 확인'); await capture('09-receipt-confirmed');
 await workspace('operator','운영자'); await capture('10-operator-completed');
 await page.goto('http://127.0.0.1:5396/workspace/food-driver/history');
 await page.getByRole('heading',{name:'완료 배달 내역',exact:true}).waitFor();
 await page.locator('.rw-row').first().waitFor(); await capture('11-driver-history');
 await page.locator('.rw-row').first().click(); await page.getByRole('heading',{name:'내역 상세',exact:true}).waitFor();
 await page.getByRole('heading',{name:'고객 정보',exact:true}).waitFor(); await capture('12-driver-detail');
 const http=await (await page.request.get('http://127.0.0.1:5396/verification/http-evidence')).json();
 fs.writeFileSync(path.join(out,'http-evidence.json'),JSON.stringify(http,null,2));
 assert.equal(errors.length,0,errors.join('\n'));
}
async function assertEventually(predicate,message){const until=Date.now()+30000;while(Date.now()<until){if(await predicate())return;await new Promise(r=>setTimeout(r,200));}throw new Error(message);}
run().then(()=>console.log(JSON.stringify({status:'Completed',orderNo,screenshots:results.length,errors:errors.length})))
 .catch(e=>{errors.push(e.message);console.error(e.message);process.exitCode=1;})
 .finally(async()=>{if(browser)await browser.close();fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({schemaVersion:'actual-shared-food-ui.r26',orderNo,workStableId:prepared.workStableId,status:errors.length?'Failed':'Completed',results,errors,orderCreatedByActualRoleClient:true,orderCreateUiVerified:false,sharedRazorApiVerified:true,physicalDeviceVerified:false,actualGpsVerified:false,externalMapVerified:false,realPaymentVerified:false},null,2));});
