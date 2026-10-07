// 로컬 제품 컴포넌트의 자동 렌더 검증. 실제 저장/API 업무/단말 실행의 증거가 아닙니다.
const { chromium } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = process.cwd();
const out = path.join(root, 'artifacts/local/role-handoff-fixes-r21/ui');
const assets = path.join(root, 'docs/assets/changes/2026-10-06-role-handoff-fixes-r21');
fs.mkdirSync(out, {recursive:true}); fs.mkdirSync(assets, {recursive:true});
const results = []; const errors = []; const checks = [];
const base = 'http://127.0.0.1:5392';
const knownMapFailure = /google|지도|Maps|ERR_ABORTED/i;
async function run() {
  const browser = await chromium.launch({headless:true, ...(process.env.HANDOFF_BROWSER_PATH ? {executablePath:process.env.HANDOFF_BROWSER_PATH} : {})});
  try {
    for (const width of [320,390]) {
      const context = await browser.newContext({viewport:{width,height:844}, reducedMotion:'reduce'});
      const page = await context.newPage();
      page.on('pageerror', e => errors.push({width,type:'pageerror',message:e.message}));
      page.on('console', m => { if(m.type()==='error') errors.push({width,type:'console',message:m.text()}); });
      async function open(url, text) {
        await page.goto(base+url);
        await page.getByText(text,{exact:false}).first().waitFor();
        await page.locator('html').evaluate(async () => { await document.fonts.ready; });
      }
      async function capture(name, publish=false) {
        const info = await page.evaluate(() => ({
          viewport:innerWidth, scroll:document.documentElement.scrollWidth,
        controls:[...document.querySelectorAll('.role-secondary button,.role-secondary input:not([type=checkbox]),.role-secondary select,.role-secondary textarea,.role-secondary header a,.role-map-workspace__related a')]
            .filter(e=>e.getBoundingClientRect().width>0).map(e=>({tag:e.tagName,text:(e.innerText||e.getAttribute('type')||'').slice(0,80),height:e.getBoundingClientRect().height})),
          text:document.body.innerText
        }));
        assert.ok(info.scroll <= info.viewport+1, `${name}: horizontal overflow ${info.scroll}/${info.viewport}`);
        assert.ok(info.controls.every(x=>x.height>=47.5), `${name}: short touch target`);
        const file = `${name}-${width}.png`;
        await page.screenshot({path:path.join(out,file),fullPage:true});
        if(publish && width===390) fs.copyFileSync(path.join(out,file),path.join(assets,file));
        results.push({name,width,url:page.url(),viewport:info.viewport,scroll:info.scroll,controls:info.controls});
      }
      await open('/workspace/operator?selected=preview-order-1','화물 예외 검토');
      await page.getByRole('link',{name:'화물 예외 검토',exact:true}).click();
      await page.getByText('수량 불일치',{exact:true}).waitFor();
      assert.ok(new URL(page.url()).searchParams.get('returnUrl').includes('selected='));
      await capture('operator-list');
      await page.getByRole('link',{name:/상세·검토/}).click();
      await page.getByRole('heading',{name:'검토 결정',exact:true}).waitFor();
      await page.getByLabel('조치').selectOption('NormalAcceptedAffectedHeld');
      await page.getByLabel('정상 수량',{exact:true}).fill('10');
      await page.getByLabel('영향 수량',{exact:true}).fill('2');
      await page.getByLabel('검토 사유',{exact:true}).fill('수량 확인 후 정상분을 인수하고 영향분을 검토합니다.');
      await page.getByRole('button',{name:'결정 내용 확인',exact:true}).click();
      await page.getByText('정상 10 개 · 영향 2 개',{exact:true}).waitFor();
      assert.equal(await page.getByRole('button',{name:'결정 저장',exact:true}).isDisabled(),true);
      await capture('operator-review',true);
      await page.getByRole('button',{name:'입력으로 돌아가기',exact:true}).click();
      await page.getByRole('button',{name:'결정 내용 확인',exact:true}).waitFor();
      await page.getByRole('link',{name:/사건 목록/}).click();
      await page.getByText('수량 불일치',{exact:true}).waitFor();
      await page.getByRole('link',{name:/운영 업무/}).click();
      await page.getByRole('link',{name:'화물 예외 검토',exact:true}).waitFor();
      assert.equal(new URL(page.url()).searchParams.get('selected'),'preview-order-1');
      checks.push({width,flow:'operator-current-list-review-confirm-cancel-list-current'});

      await open('/workspace/shipper?scenario=held','영향 수량만 보류 중입니다.');
      await capture('shipper-hold',true);
      await open('/handoff-preview/neighborhood','영향 수량만 보류 중입니다.');
      await capture('neighborhood-hold',true);

      for(const role of ['food-driver','cargo-driver']) {
        const label = role==='food-driver'?'완료 배달 내역':'운송 내역';
        await open(`/workspace/${role}?selected=${role==='food-driver'?'preview-order-1':'101'}`,label);
        await page.getByRole('link',{name:label,exact:true}).click();
        await page.getByRole('link',{name:/상세 보기/}).waitFor();
        if(role==='food-driver') {
          await page.getByLabel('전달 완료일(한국)',{exact:true}).fill('2026-10-05');
          await page.getByRole('button',{name:'날짜 조회',exact:true}).click();
          await page.getByRole('link',{name:/상세 보기/}).waitFor();
        }
        await capture(role+'-history');
        await page.getByRole('link',{name:/상세 보기/}).click();
        await page.getByRole('heading',{name:'내역 상세',exact:true}).waitFor();
        await page.getByText(role==='food-driver'?'수락 당시 요금 구성':'종료 상태', {exact:true}).waitFor();
        if(role==='food-driver') await page.getByText('10/05 12:00 (한국)',{exact:false}).first().waitFor();
        await capture(role+'-detail',true);
        await page.getByRole('link',{name:/내역 목록/}).click();
        await page.getByRole('link',{name:/상세 보기/}).waitFor();
        if(role==='food-driver') assert.equal(await page.getByLabel('전달 완료일(한국)',{exact:true}).inputValue(),'2026-10-05');
        await page.getByRole('link',{name:/현재 업무/}).click();
        await page.getByRole('link',{name:label,exact:true}).waitFor();
        checks.push({width,flow:role+'-current-list-detail-list-current'});
      }
      await open('/workspace/food-driver/history/preview-settlement?scenario=expired','정산 정보만 표시합니다.');
      assert.equal(await page.getByRole('heading',{name:'고객 정보',exact:true}).count(),0);
      assert.equal(await page.getByRole('heading',{name:'주문 정보',exact:true}).count(),0);
      await capture('food-expired');
      for (const [scenario,text] of [['empty','검토할 화물 사건이 없습니다'],['permission','현재 계정으로 이 업무를 처리할 수 없습니다.'],['feature-off','기능 활성화']]) {
        await open('/workspace/operator/cargo-incidents?scenario='+scenario,text);
        if(scenario!=='empty') assert.equal(await page.getByRole('link',{name:/상세·검토/}).count(),0);
        await capture('operator-'+scenario);
      }
      await context.close();
    }
    assert.equal(errors.filter(x=>x.type==='pageerror'||!knownMapFailure.test(x.message)).length,0,JSON.stringify(errors));
    fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({passed:true,results,checks,errors,evidenceScope:'Local hosted product components with example DTOs; no external writes or phone/Azure verification'},null,2));
    console.log(JSON.stringify({passed:true,captures:results.length,flows:checks.length,errors},null,2));
  } finally { await browser.close(); }
}
run().catch(error=>{fs.writeFileSync(path.join(out,'failure.json'),JSON.stringify({message:error.message,results,checks,errors},null,2));console.error(error);process.exitCode=1;});
