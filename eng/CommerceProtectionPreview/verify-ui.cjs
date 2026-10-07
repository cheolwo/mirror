// 실제 공유 Razor를 개발 전용 루프백 호스트에서 검토합니다. 운영 인증·DB 저장 증거가 아닙니다.
const {chromium} = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const out=path.join(process.cwd(),'artifacts/local/commerce-privacy-r25/ui');
fs.mkdirSync(out,{recursive:true});
const results=[],flows=[],errors=[];
async function run(){
 const browser=await chromium.launch({headless:true,...(process.env.COMMERCE_BROWSER_PATH?{executablePath:process.env.COMMERCE_BROWSER_PATH}:{})});
 try{
  for(const width of [320,390]){
   const context=await browser.newContext({viewport:{width,height:844},reducedMotion:'reduce'});
   await context.route('**/*',route=>{const host=new URL(route.request().url()).hostname; return host==='127.0.0.1'?route.continue():route.abort();});
   const page=await context.newPage();
   page.on('pageerror',e=>errors.push({width,message:e.message}));
   page.on('console',m=>{if(m.type()==='error') errors.push({width,message:m.text()});});
   async function open(url,text){await page.goto('http://127.0.0.1:5396'+url); await page.getByRole('heading',{name:text,exact:true}).waitFor(); await page.waitForFunction(()=>!document.querySelector('.commerce-page [role=status]')?.textContent?.includes('불러오고'));}
   async function capture(name){const info=await page.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth,controls:[...document.querySelectorAll('.commerce-page button,.commerce-page select,.commerce-page input:not([type=checkbox]),.commerce-page summary')].filter(e=>e.getBoundingClientRect().height>0).map(e=>({text:e.textContent,height:e.getBoundingClientRect().height}))}));assert.ok(info.scroll<=info.width+1,name+' overflow');assert.ok(info.controls.every(e=>e.height>=47.5),name+' touch target'); await page.screenshot({path:path.join(out,name+'-'+width+'.png'),fullPage:true});results.push({name,url:page.url(),...info});}
   await open('/commerce/notices','거래·개인정보 안내');assert.ok(await page.getByText('거래 당사자를 연결하는 통신판매중개 플랫폼',{exact:false}).isVisible());assert.equal(await page.locator('[data-document=privacy]').getAttribute('open'),null);await capture('notices');await page.locator('[data-document=privacy] summary').click();assert.ok(await page.getByText('기사의 완료 상세정보 열람은 3일간 가능합니다.',{exact:false}).isVisible());await capture('privacy-policy-open');flows.push({width,flow:'public-notice-policy-open'});
   await open('/commerce/seller','판매자 확인');await page.getByRole('heading',{name:'판매자 확인 필요'}).waitFor();assert.equal(await page.getByRole('button',{name:'판매자 정보 저장'}).isEnabled(),false);await capture('seller-unverified');await page.getByLabel('판매자 유형',{exact:true}).selectOption('Business');await page.getByLabel('사업장 주소',{exact:true}).waitFor();await capture('seller-business');flows.push({width,flow:'business-private-type-switch-not-verification'});
   await open('/commerce/seller?state=anonymous','판매자 확인');await page.getByRole('heading',{name:'로그인이 필요해요'}).waitFor();assert.equal(await page.locator('#seller-phone').count(),0);await capture('seller-anonymous');
   await open('/commerce/privacy?state=empty','내 정보 관리');await page.getByText('접수한 요청이 없습니다.').waitFor();await capture('rights-empty');await page.getByLabel('요청 종류',{exact:true}).selectOption('deletion');await page.getByLabel('요청할 내용',{exact:true}).fill('내 정보 삭제와 보존 근거 확인을 요청합니다.');await page.getByRole('button',{name:'요청 접수',exact:true}).click();await page.getByRole('heading',{name:'접수됨',exact:true}).waitFor();assert.equal(await page.getByText('주말 기준 잠정 기한입니다.',{exact:false}).count(),0);await page.getByText('처리 결과 안내 목표:',{exact:false}).waitFor();await capture('rights-submitted');flows.push({width,flow:'rights-request-receipt'});
   await open('/commerce/privacy/example-case?state=retained','내 정보 관리');await page.getByRole('heading',{name:'처리 제한 안내',exact:true}).waitFor();await page.getByText('처리 제한 사유: 법정 보존 의무').waitFor();await page.getByText('보존 기한:',{exact:false}).waitFor();await capture('rights-retained');flows.push({width,flow:'rights-retention-reason-and-appeal'});
   await open('/commerce/privacy/example-case?state=retention-pending','내 정보 관리');await page.getByText('보존 상태를 확인하고 있습니다.',{exact:false}).waitFor();assert.equal(await page.getByRole('button',{name:'내용 추가',exact:true}).count(),0);await capture('rights-retention-pending');flows.push({width,flow:'retention-pending-blocks-actions'});
   await open('/commerce/disputes?sourceKind=food-order&sourceId=example-order','거래 문제 신고');await capture('dispute-form');await page.getByLabel('문제 내용',{exact:true}).fill('주문 전달 상태를 확인해 주세요.');await page.getByRole('button',{name:'요청 접수',exact:true}).click();await page.getByRole('heading',{name:'접수됨',exact:true}).waitFor();await capture('dispute-submitted');flows.push({width,flow:'transaction-linked-dispute-receipt'});
   await open('/commerce/disputes/example-case','거래 문제 신고');await page.getByRole('heading',{name:'확인 중',exact:true}).waitFor();await page.getByLabel('추가로 알릴 내용',{exact:true}).fill('처리 경과를 추가로 확인합니다.');await page.getByRole('button',{name:'내용 추가',exact:true}).click();await page.getByText('요청을 접수했습니다. 처리 진행을 확인할 수 있습니다.').waitFor();await capture('dispute-detail');flows.push({width,flow:'allowed-action-add-content'});
   await open('/commerce/privacy?state=error','내 정보 관리');await page.getByRole('alert').waitFor();assert.equal(await page.getByText('접수한 요청이 없습니다.').count(),0);await capture('rights-error');
   await open('/commerce/confirmation?state=disclosure','거래 확인');const acknowledgment=page.getByRole('checkbox',{name:'거래 당사자와 거래·개인정보 안내를 확인했습니다.'});await acknowledgment.check();await page.getByText('확인한 판매자 판본: 1').waitFor();await capture('seller-disclosure-accepted');await page.getByRole('button',{name:'다른 판매자 확인'}).click();await page.getByText('확인한 판매자 판본: 없음').waitFor();assert.equal(await acknowledgment.isChecked(),false);await acknowledgment.check();await page.getByText('확인한 판매자 판본: 2').waitFor();await capture('seller-disclosure-changed');flows.push({width,flow:'seller-disclosure-current-revision-reacknowledgment'});await context.close();
  }
  assert.equal(errors.length,0,JSON.stringify(errors));
 }finally{await browser.close();fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({basis:'actual-shared-razor-development-preview',results,flows,errors,physicalDeviceVerified:false,productionVerified:false},null,2));}
 console.log(JSON.stringify({screenshots:results.length,flows:flows.length,errors:errors.length}));
}
run().catch(e=>{console.error(e);process.exitCode=1;});
