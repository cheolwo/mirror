// Existing completed order only: admin GET -> one preview-only GET 503 -> GET recovery.
// No order creation, acceptance, cooking, dispatch, delivery or payment command is submitted.
const {chromium}=require('playwright');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const root=path.resolve(process.env.FOUR_ROLE_R27_ARTIFACTS||'artifacts/local/food-mobile-four-role-r27');
const out=path.resolve(process.env.ADMIN_STYLE_R27_OUTPUT||path.join(root,'admin-style-ui'));
assert.ok(out.startsWith(root+path.sep),'Admin style evidence must stay under the existing ignored r27 artifact root');
const prior=JSON.parse(fs.readFileSync(path.join(root,'real-ui','verification.json'),'utf8').replace(/^\uFEFF/,''));
assert.equal(prior.status,'Completed','The previous actual four-role workflow must already be complete');
const orderNo=process.env.ADMIN_STYLE_R27_ORDER||prior.orderNo;
assert.equal(orderNo,prior.orderNo,'Admin style validation must use the same completed order');
assert.match(orderNo,/^FOOD-\d+$/);
if(fs.existsSync(path.join(out,'verification.json')))throw Error('Existing style evidence is preserved; set ADMIN_STYLE_R27_OUTPUT to a new artifact subdirectory');
fs.mkdirSync(out,{recursive:true});
const host='http://127.0.0.1:5397',results=[],checks=[],assets=[],errors=[];
let browser,page,startCount=0,productHttp=[];
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const priorEvidenceHash=hash(path.join(root,'real-ui','verification.json'));
async function eventually(predicate,message,ms=20000){const until=Date.now()+ms;while(Date.now()<until){if(await predicate())return;await new Promise(r=>setTimeout(r,150));}throw Error(message)}
async function httpRows(){return(await(await page.request.get(host+'/verification/http-evidence')).json()).rows}
async function styleState(){return page.evaluate(()=>{
 const detail=document.querySelector('.food-mobile-detail'),heading=document.querySelector('.food-mobile-detail .mud-typography-h4');
 const paper=document.querySelector('.food-mobile-detail .mud-paper'),button=[...document.querySelectorAll('.food-mobile-detail button.mud-button-root')].find(x=>x.innerText==='최신 상태 확인');
 const row=document.querySelector('.food-mobile-detail .food-progress-row');
 const css=e=>e?getComputedStyle(e):null;
 // Chromium CSS nesting exposes an empty cssRules collection even on a plain CSSStyleRule.
 // Keep each rule's own selector before traversing children/imports, instead of discarding it.
 const readSheet=sheet=>{try{return collect(sheet.cssRules)}catch{return []}};
 const collect=rules=>[...rules].flatMap(r=>[
  ...(r.selectorText?[{selector:r.selectorText,minHeight:r.style?.minHeight||''}]:[]),
  ...(r.cssRules?collect(r.cssRules):[]),
  ...(r.styleSheet?readSheet(r.styleSheet):[])
 ]);
 const rules=[...document.styleSheets].flatMap(readSheet);
 const detailScope=detail?[...detail.attributes].map(a=>a.name).filter(a=>/^b-[a-z0-9]+$/.test(a)):[];
 const scopedRules=rules.filter(r=>detailScope.some(scope=>r.selector.includes('.food-mobile-detail['+scope+']')))
  .map(r=>({...r,matchesDetail:Boolean(detail?.matches(r.selector)),matchesButton:Boolean(button?.matches(r.selector))}));
 return {bodyClass:document.body.className,width:innerWidth,scrollWidth:document.documentElement.scrollWidth,
  primary:css(document.documentElement).getPropertyValue('--mud-palette-primary').trim(),surface:css(document.documentElement).getPropertyValue('--mud-palette-surface').trim(),
  heading:heading?{fontSize:css(heading).fontSize,color:css(heading).color}:null,
  paper:paper?{background:css(paper).backgroundColor,borderWidth:css(paper).borderTopWidth,borderStyle:css(paper).borderTopStyle,padding:css(paper).padding}:null,
  button:button?{color:css(button).color,borderWidth:css(button).borderTopWidth,minHeight:css(button).minHeight}:null,
  row:row?{borderWidth:css(row).borderBottomWidth,paddingTop:css(row).paddingTop}:null,
  detailScope,scopedRules,
  scopedDetailCssPresent:scopedRules.some(r=>r.matchesDetail),
  scopedButtonCssMatched:scopedRules.some(r=>r.matchesButton&&r.minHeight==='48px'),
  foundationHref:[...document.querySelectorAll('link[rel="stylesheet"]')].some(e=>e.href.endsWith('/_content/Ssalddel.Ui.Common/Areas/App/css/role-app-foundation.css')),
  adminAppHref:[...document.querySelectorAll('link[rel="stylesheet"]')].some(e=>e.href.endsWith('/verification/admin-app.css')),
  styleSheets:[...document.styleSheets].map(s=>({href:s.href,disabled:s.disabled}))};
 })}
async function capture(name){
 for(const width of [390,320]){
  await page.setViewportSize({width,height:844});await page.waitForTimeout(150);
  const styles=await styleState();
  assert.ok(styles.primary&&styles.surface,name+' missing Mud theme variables');
  assert.ok(styles.bodyClass.split(/\s+/).includes('role-app--admin'),name+' missing product admin body class');
  assert.ok(styles.heading&&parseFloat(styles.heading.fontSize)>16,name+' heading typography is unstyled');
  assert.equal(styles.paper?.background,'rgb(255, 255, 255)',name+' paper surface is not the product theme surface');
  assert.ok(parseFloat(styles.paper.borderWidth)>=1&&styles.paper.borderStyle==='solid',name+' paper boundary is missing');
  assert.equal(styles.button?.color,'rgb(15, 118, 110)',name+' primary button does not use the product admin palette');
  assert.ok(parseFloat(styles.button.borderWidth)>=1,name+' primary button border is missing');
  assert.ok(parseFloat(styles.button.minHeight)>=48,name+' linked page button CSS is missing');
  assert.ok(styles.detailScope.length>0&&styles.scopedDetailCssPresent,name+' linked product scoped CSS is missing');
  assert.ok(styles.scopedButtonCssMatched,name+' scoped button selector does not match the actual page anchor/button');
  assert.ok(parseFloat(styles.row?.borderWidth)>=1&&styles.row.paddingTop==='12px',name+' product checkpoint row CSS is missing');
  assert.ok(styles.foundationHref&&styles.adminAppHref,name+' product stylesheet assets were not linked');
  assert.ok(styles.scrollWidth<=width+1,name+' horizontal overflow at '+width);
  const file=path.join(out,name+'-'+width+'.png');await page.screenshot({path:file,fullPage:true});
  results.push({name,width,url:page.url(),atUtc:new Date().toISOString(),styles,png:path.basename(file),pngSha256:hash(file)});
 }
 await page.setViewportSize({width:390,height:844});
}
async function run(){
 browser=await chromium.launch({headless:true,executablePath:process.env.COMMERCE_BROWSER_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
 const context=await browser.newContext({viewport:{width:390,height:844},reducedMotion:'reduce'});
 await context.route('**/*',r=>new URL(r.request().url()).hostname==='127.0.0.1'?r.continue():r.abort());
 page=await context.newPage();page.setDefaultTimeout(20000);
 page.on('pageerror',e=>errors.push(e.message));
 page.on('response',r=>{if(/\.css(?:\?|$)/.test(r.url()))assets.push({url:r.url(),status:r.status(),contentType:r.headers()['content-type']})});
 startCount=(await httpRows()).length;
 await page.goto(host+'/food-operations/orders/'+encodeURIComponent(orderNo));
 await page.getByRole('heading',{name:'음식 주문 확인',exact:true}).waitFor();
 await page.getByText('다음 담당:',{exact:false}).waitFor();
 await page.getByText(orderNo,{exact:true}).waitFor();
 await page.getByText('주문 수령확인 · 배차 배달완료',{exact:true}).waitFor();
 await eventually(async()=>Boolean((await styleState()).primary),'Product admin theme provider did not render');
 await capture('01-admin-completed');
 checks.push({name:'Product admin providers, palette, stylesheet assets and linked scoped CSS',status:'Passed'});
 assert.equal((await page.request.post(host+'/verification/fail-next-admin-trace')).status(),204);
 await page.getByRole('button',{name:'최신 상태 확인',exact:true}).click();
 await page.getByText('최신 음식 주문을 확인하지 못했습니다. 다시 조회해 주세요.',{exact:true}).waitFor();
 await page.locator('[data-food-order-stale-trace]').waitFor();
 await page.getByText('이전 확인 시 담당:',{exact:false}).waitFor();
 assert.equal(await page.getByText(orderNo,{exact:true}).count(),1,'Failed GET discarded the retained completed order');
 await capture('02-admin-stale');
 checks.push({name:'Same completed order retained and labelled stale after one preview GET 503',status:'Passed'});
 await page.getByRole('button',{name:'최신 상태 확인',exact:true}).click();
 await eventually(async()=>await page.locator('[data-food-order-stale-trace]').count()===0,'Stale trace did not recover');
 await page.getByText('다음 담당:',{exact:false}).waitFor();
 assert.equal(await page.getByText('최신 음식 주문을 확인하지 못했습니다. 다시 조회해 주세요.',{exact:true}).count(),0);
 await capture('03-admin-refreshed');
 productHttp=(await httpRows()).slice(startCount);
 assert.equal(productHttp.filter(r=>r.method!=='GET').length,0,'Admin style validation sent a product business command');
 const trace=productHttp.filter(r=>r.path==='/api/v1/admin/food-orders/'+orderNo+'/operations-trace');
 assert.equal(trace.filter(r=>r.status===503).length,1,'Expected exactly one preview-injected trace GET 503');
 assert.ok(trace.filter(r=>r.status===200).length>=2,'Initial and recovered trace GETs were not confirmed');
 for(const suffix of ['/_content/MudBlazor/MudBlazor.min.css','/verification/admin-app.css','/_content/Ssalddel.Ui.Common/Areas/App/css/role-app-foundation.css']){
  assert.ok(assets.some(a=>a.url.endsWith(suffix)&&a.status===200&&a.contentType?.startsWith('text/css')),suffix+' was not actually served as CSS');
 }
 assert.equal(hash(path.join(root,'real-ui','verification.json')),priorEvidenceHash,'Original workflow evidence was changed');
 checks.push({name:'Recovered trace GET, six styled mobile screenshots, no product business POST and original evidence preserved',status:'Passed'});
 assert.equal(errors.length,0,errors.join('\n'));
}
run().then(()=>console.log(JSON.stringify({status:'Completed',orderNo,screenshots:results.length,checks:checks.length})))
 .catch(e=>{errors.push(e.message);console.error(e.message);process.exitCode=1})
 .finally(async()=>{
  if(page)try{productHttp=(await httpRows()).slice(startCount)}catch{}
  if(browser)await browser.close();
  fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({schemaVersion:'product-admin-style-ui.r27',status:errors.length?'Failed':'Completed',
   orderNo,sourceWorkflowEvidence:'real-ui/verification.json',sourceWorkflowEvidenceSha256:priorEvidenceHash,results,checks,assets,httpRows:productHttp,errors,
   productPageDesignChanged:false,productPageScopedCssAnchorCorrected:true,linkedProductThemeAndCss:true,orderCreated:false,businessWorkflowReplayed:false,
   physicalDeviceVerified:false,actualGpsVerified:false,externalMapVerified:false,realPaymentVerified:false},null,2));
 });
