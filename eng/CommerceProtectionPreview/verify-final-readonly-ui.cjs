const {chromium}=require('playwright');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const root=path.join(process.cwd(),'artifacts/local/apk-completion-r26'),out=path.join(root,'final-styled-readonly-ui');fs.mkdirSync(out,{recursive:true});
const {orderNo,caseId}=JSON.parse(fs.readFileSync(path.join(root,'real-support-ui/case-summary.json'),'utf8'));
const targets=[
 ['orderer','/workspace/orderer?selected='+orderNo,'주문자'],
 ['restaurant','/workspace/restaurant?selected='+orderNo,'음식점'],
 ['food-driver','/workspace/food-driver','음식기사'],
 ['operator','/workspace/operator?selected='+orderNo,'운영자'],
 ['history','/workspace/food-driver/history','완료 배달 내역'],
 ['support-user','/commerce/disputes/'+caseId,'거래 문제 신고'],
 ['support-admin','/privacy-support/'+caseId,'사건 처리'],
 ['support-admin-404','/privacy-support/support%3Ar26-missing','사건 처리'],
 ['support-other-party-404','/commerce/disputes/'+caseId+'?role=food-driver','거래 문제 신고']
 ];
const rows=[],errors=[];let browser;
async function run(){
 browser=await chromium.launch({headless:true,executablePath:process.env.COMMERCE_BROWSER_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
 const context=await browser.newContext({viewport:{width:390,height:844},reducedMotion:'reduce'});
 await context.route('**/*',r=>new URL(r.request().url()).hostname==='127.0.0.1'?r.continue():r.abort());
 const page=await context.newPage();page.setDefaultTimeout(15000);page.on('pageerror',e=>errors.push(e.message));
 for(const width of [320,390]){
  await page.setViewportSize({width,height:844});
  for(const [name,url,heading] of targets){
   await page.goto('http://127.0.0.1:5396'+url);await page.getByRole('heading',{name:heading,exact:true}).waitFor();
   await page.waitForFunction(()=>[...document.styleSheets].some(s=>s.href?.endsWith('/verification/shared-ui.css')&&s.cssRules.length>1000));
   if(name==='support-admin-404'){await page.getByRole('alert').waitFor();await page.getByRole('link',{name:'요청 목록으로',exact:true}).waitFor();}
   else if(name==='support-other-party-404'){await page.getByRole('alert').waitFor();assert.equal(await page.getByLabel('추가로 알릴 내용',{exact:true}).count(),0);await page.getByRole('link',{name:'내 요청 목록으로',exact:true}).waitFor();}
   else if(name==='support-admin') await page.locator('#support-action').waitFor();
   else if(name==='support-user') await page.getByRole('heading',{name:'이의제기 접수',exact:true}).waitFor();
   else if(name==='history') await page.locator('.rw-row').first().waitFor();
   else await page.waitForFunction(()=>!document.body.innerText.includes('현재 업무를 불러오는 중입니다.'));
   const bounds=await page.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth,sharedCssApplied:!document.querySelector('.support-workspace')||parseFloat(getComputedStyle(document.querySelector('.support-workspace')).paddingLeft)>0,controls:[...document.querySelectorAll('.support-workspace button,.support-workspace select,.support-workspace nav a,.commerce-page button,.commerce-page select')].filter(e=>e.getBoundingClientRect().height>0).map(e=>e.getBoundingClientRect().height)}));assert.ok(bounds.scroll<=bounds.width+1,name+' overflow');assert.ok(bounds.sharedCssApplied,name+' missing shared CSS');assert.ok(bounds.controls.every(x=>x>=47.5),name+' small touch targets');
   await page.screenshot({path:path.join(out,name+'-'+width+'.png'),fullPage:true});rows.push({name,width,url:page.url(),atUtc:new Date().toISOString(),...bounds});
  }
 }
 assert.equal(errors.length,0,errors.join('\n'));
 const http=await(await page.request.get('http://127.0.0.1:5396/verification/http-evidence')).json();
 assert.equal(http.rows.filter(x=>x.method!=='GET').length,0,'Readonly verification sent mutations');
 fs.writeFileSync(path.join(out,'http-evidence.json'),JSON.stringify(http,null,2));
}
run().then(()=>console.log(JSON.stringify({status:'Passed',screenshots:rows.length,errors:errors.length})))
 .catch(e=>{errors.push(e.message);console.error(e.message);process.exitCode=1;})
 .finally(async()=>{if(browser)await browser.close();fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({schemaVersion:'final-readonly-shared-ui.r26',status:errors.length?'Failed':'Passed',orderNo,caseId,rows,errors,readOnly:true,externalNotificationsSent:false,syntheticNoticeProofsOnly:true,physicalDeviceVerified:false,productionVerified:false},null,2));});
