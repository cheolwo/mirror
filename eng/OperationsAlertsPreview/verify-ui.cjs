const { chromium } = require('playwright');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const out=path.join(process.cwd(),'artifacts/operations-alerts-r45/ui');fs.mkdirSync(out,{recursive:true});
(async()=>{const browser=await chromium.launch({headless:true,...(process.env.COMMERCE_BROWSER_PATH?{executablePath:process.env.COMMERCE_BROWSER_PATH}:{})});const results=[],errors=[];
try{for(const width of [320,390]){const page=await browser.newPage({viewport:{width,height:844}});page.on('pageerror',e=>errors.push(e.message));
await page.route('**/*',r=>new URL(r.request().url()).hostname==='127.0.0.1'?r.continue():r.abort());
for(const scenario of ['list','empty','error','disabled','login','detail','stale','conflict']){
const detail=['detail','stale','conflict'].includes(scenario),id='a'.repeat(64);await page.goto('http://127.0.0.1:5417'+(detail?'/operations-alerts/'+id:'')+'?state='+scenario);
await page.getByRole('heading',{name:'운영 알림',exact:true}).waitFor();
if(scenario==='empty')await page.getByText('확인할 알림이 없습니다').waitFor();
if(scenario==='error')await page.getByRole('button',{name:'다시 시도'}).waitFor();
if(scenario==='login')await page.getByRole('link',{name:'관리자 로그인'}).waitFor();
if(scenario==='stale')assert(await page.getByRole('button',{name:'알림 확인',exact:true}).isDisabled());
if(scenario==='conflict'){await page.getByRole('button',{name:'알림 확인',exact:true}).click();await page.getByText('상태가 바뀌었거나 확인에 실패했습니다.',{exact:false}).waitFor();}
if(scenario==='detail'){await page.getByRole('button',{name:'알림 확인',exact:true}).click();await page.getByRole('button',{name:'확인 중',exact:true}).waitFor();}
assert(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth));
await page.screenshot({path:path.join(out,`${width}-${scenario}.png`),fullPage:true});results.push({width,scenario,overflow:false});}
await page.close();}assert.equal(errors.length,0);fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({results,errors,actualExternalSending:false,actualApi:false},null,2));
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exit(1)});
