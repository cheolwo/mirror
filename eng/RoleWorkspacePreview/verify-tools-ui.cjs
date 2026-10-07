// 실제 공통 컴포넌트 + 로컬 예시 DTO. 업무 저장·외부 지급·휴대폰 실행 근거가 아닙니다.
const { chromium } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const out = path.join(process.cwd(), 'artifacts/local/role-tools-r22/ui');
const assets = path.join(process.cwd(), 'docs/assets/changes/2026-10-06-role-tools-r22');
fs.mkdirSync(out, {recursive:true}); fs.mkdirSync(assets, {recursive:true});
const results = [], checks = [], errors = [];
const base = 'http://127.0.0.1:5392';
async function run() {
  const browser = await chromium.launch({headless:true, ...(process.env.ROLE_TOOLS_BROWSER_PATH ? {executablePath:process.env.ROLE_TOOLS_BROWSER_PATH} : {})});
  try {
    for (const width of [320,390]) {
      const context = await browser.newContext({viewport:{width,height:844},reducedMotion:'reduce'});
      const page = await context.newPage();
      page.on('pageerror', e=>errors.push({width,type:'pageerror',message:e.message}));
      page.on('console', m=>{if(m.type()==='error') errors.push({width,type:'console',message:m.text()});});
      async function open(url) {
        await page.goto(base+url);
        await page.locator('.role-map-workspace__card').waitFor();
        await page.locator('html').evaluate(async()=>await document.fonts.ready);
      }
      async function capture(name, publish=false) {
        const info = await page.evaluate(()=>({viewport:innerWidth,scroll:document.documentElement.scrollWidth,
          controls:[...document.querySelectorAll('.role-map-workspace__actions a,.role-map-workspace__actions button,.role-map-workspace__tools summary,.neighborhood-map-work-tools summary,.neighborhood-map-work-tools a')]
            .filter(e=>e.getBoundingClientRect().width>0 && e.getClientRects().length>0)
            .map(e=>({text:e.innerText,height:e.getBoundingClientRect().height}))}));
        assert.ok(info.scroll<=info.viewport+1,`${name}: horizontal overflow`);
        assert.ok(info.controls.every(c=>c.height>=47.5),`${name}: touch target too short`);
        const file = `${name}-${width}.png`;
        await page.screenshot({path:path.join(out,file),fullPage:true});
        if(publish && width===390) fs.copyFileSync(path.join(out,file),path.join(assets,file));
        results.push({name,width,url:page.url(),...info});
      }
      for(const role of ['orderer','restaurant','food-driver','shipper','cargo-driver','warehouse','operator']) {
        await open('/workspace/'+role);
        const cardBefore = await page.locator('.role-map-workspace__card').innerText();
        const urlBefore = page.url();
        const tools = page.locator('.role-map-workspace__tools');
        if(await tools.count()) {
          assert.equal(await tools.getAttribute('open'),null,role+': tools must start closed');
          await capture(role+'-current',role==='cargo-driver');
          await tools.locator('summary').click();
          assert.equal(page.url(),urlBefore,role+': opening tools changed route');
          assert.equal(await page.locator('.role-map-workspace__card').innerText(),cardBefore,role+': opening tools changed work');
          assert.ok(await tools.locator('a,button').count()>0);
          await capture(role+'-tools',role==='shipper'||role==='food-driver');
          checks.push({role,width,flow:'current-tools-open-no-work-change'});
        } else { await capture(role+'-current'); }
        if(role==='cargo-driver') assert.equal(await page.getByRole('link',{name:'문제 신고',exact:true}).isVisible(),true);
        if(role==='shipper') {
          for(const name of ['운송 내역','결제·정산 확인','인수증·증빙']) {
            const link = tools.getByRole('link',{name,exact:true});
            assert.ok((await link.getAttribute('href')).includes('returnUrl='));
          }
        }
      }
      for(const role of ['food-driver','cargo-driver','operator']) {
        const label = role==='food-driver'?'완료 배달 내역':role==='cargo-driver'?'운송 내역':'화물 예외 검토';
        const selected = role==='cargo-driver'?'101':role==='food-driver'?'preview-offer-1':'preview-order-1';
        await open(`/workspace/${role}?selected=${selected}`);
        await page.locator('.role-map-workspace__tools summary').click();
        await page.locator('.role-map-workspace__tools').getByRole('link',{name:label,exact:true}).click();
        await page.getByRole('link',{name:role==='operator'?/운영 업무/:/현재 업무/}).waitFor();
        assert.ok(new URL(page.url()).searchParams.get('returnUrl').includes('selected='+selected));
        await page.getByRole('link',{name:role==='operator'?/운영 업무/:/현재 업무/}).click();
        await page.locator('.role-map-workspace__card').waitFor();
        assert.equal(new URL(page.url()).searchParams.get('selected'),selected);
        assert.equal(await page.locator('.role-map-workspace__tools').getAttribute('open'),null);
        checks.push({role,width,flow:'tool-page-current-selected-return'});
      }
      await page.goto(base+'/community/map');
      await page.locator('.neighborhood-map-panel').waitFor();
      const communityTools = page.locator('[data-neighborhood-work-tools]');
      assert.equal(await communityTools.getAttribute('open'),null);
      await capture('community-current',true);
      const before = page.url();
      await communityTools.locator('summary').click();
      assert.equal(page.url(),before);
      await capture('community-tools',true);
      checks.push({role:'community',width,flow:'dedicated-map-tools-open'});
      await page.goto(base+'/community/map?layers=offer&view=list&region=seoul-a&panel=work&target=work-one');
      await page.getByText('완료 확인 요청',{exact:true}).waitFor();
      assert.equal(await page.getByText('협업 취소',{exact:true}).isVisible(),true);
      assert.equal(await page.locator('[data-neighborhood-start]').count(),0);
      assert.equal(await communityTools.getAttribute('open'),null);
      await capture('community-work',true);
      for(const name of ['내 배송 목록','내 보관공간']) {
        await communityTools.locator('summary').click();
        await communityTools.getByRole('link',{name,exact:true}).click();
        const returnUrl = new URL(page.url()).searchParams.get('returnUrl');
        assert.ok(returnUrl.includes('target=work-one'));
        assert.ok(returnUrl.includes('region=seoul-a'));
        await page.locator('.exchange-back').click();
        await page.getByText('완료 확인 요청',{exact:true}).waitFor();
        const query = new URL(page.url()).searchParams;
        assert.equal(query.get('target'),'work-one'); assert.equal(query.get('panel'),'work');
        assert.equal(query.get('region'),'seoul-a'); assert.equal(query.get('view'),'list');
        checks.push({role:'community',width,flow:name+'-selected-map-return'});
      }
      await context.close();
    }
    const unexpected = errors.filter(e=>e.type==='pageerror'||!/google|지도|Maps|ERR_ABORTED|favicon/i.test(e.message));
    assert.equal(unexpected.length,0,JSON.stringify(unexpected));
    fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({passed:true,results,checks,errors,evidenceScope:'Local hosted shared UI with example DTOs; map fallback; no phone/Azure/Figma/operational writes'},null,2));
    console.log(JSON.stringify({passed:true,captures:results.length,flows:checks.length,errors},null,2));
  } finally { await browser.close(); }
}
run().catch(e=>{fs.writeFileSync(path.join(out,'failure.json'),JSON.stringify({message:e.message,results,checks,errors},null,2));console.error(e);process.exitCode=1;});
