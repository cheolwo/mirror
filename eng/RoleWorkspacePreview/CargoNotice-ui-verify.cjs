/* Run only against an isolated RoleWorkspacePreview host with CargoNoticePreviewFixture registrations. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
(async () => {
  const base = (process.env.CARGO_NOTICE_PREVIEW_URL || 'http://127.0.0.1:5392').replace(/\/$/, '');
  assert.ok(/^http:\/\/(127\.0\.0\.1|localhost):\d+$/.test(base));
  const output = path.resolve(process.env.CARGO_NOTICE_PREVIEW_OUTPUT || `artifacts/local/cargo-notice-ui-${Date.now()}`);
  assert.ok(output.startsWith(path.resolve('artifacts/local') + path.sep));
  fs.mkdirSync(output, { recursive: false });
  const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM
    ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : { channel: 'msedge' }) });
  const checks = [], errors = [], blocked = [];
  try {
    for (const width of [390, 1280]) {
      const context = await browser.newContext({ viewport: { width, height: 1000 }, locale: 'ko-KR' });
      const page = await context.newPage(); page.setDefaultTimeout(15000);
      page.on('pageerror', error => errors.push(String(error)));
      await context.route('**/*', route => {
        const url = new URL(route.request().url());
        if ((url.origin !== base && !['data:', 'blob:'].includes(url.protocol)) || url.pathname.startsWith('/api/')) {
          blocked.push({ method: route.request().method(), pathname: url.pathname }); return route.abort();
        }
        return route.continue();
      });
      const open = async scenario => {
        await page.goto(`${base}/cargo-notice-preview/${scenario}`, { waitUntil: 'domcontentloaded' });
        await page.locator('.cargo-notices').waitFor();
      };
      const capture = async name => {
        const geometry = await page.locator('.cargo-notices').evaluate(element => ({
          pageOverflow: document.documentElement.scrollWidth > innerWidth + 1,
          contentOverflow: element.scrollWidth > element.clientWidth + 1
        }));
        assert.equal(geometry.pageOverflow, false); assert.equal(geometry.contentOverflow, false);
        await page.screenshot({ path: path.join(output, `${width}-${name}.png`), fullPage: true });
        checks.push({ width, scenario: name, geometry });
      };
      await open('public'); await page.getByRole('heading', { name: '행사 물품 운송 정보' }).waitFor();
      assert.equal(await page.getByRole('button', { name: '수락', exact: true }).count(), 0); await capture('public');
      for (const [scenario, text] of [['empty', '현재 공개 중인 공고가 없습니다.'], ['disabled', '화물 정보 공고를 준비하고 있습니다.'], ['error', '처리 결과를 확인하지 못했습니다.'], ['anonymous', '로그인 후 내 공고를 관리할 수 있습니다']]) {
        await open(scenario); await page.getByText(text, { exact: false }).waitFor(); await capture(scenario);
      }
      await open('draft');
      await page.locator('.owner-card').click();
      await page.getByLabel('저장된 내용의 공개 미리보기').waitFor();
      const consent = page.getByRole('checkbox'); await consent.check();
      await page.getByRole('button', { name: '확인한 내용 게시', exact: true }).click();
      await page.getByRole('button', { name: '공개 철회', exact: true }).waitFor(); await capture('published');
      await page.getByLabel('제목', { exact: true }).fill('수정한 행사 물품');
      await page.getByLabel('제목', { exact: true }).press('Tab');
      await page.getByRole('button', { name: '수정 내용 저장', exact: true }).click();
      await page.getByRole('button', { name: '확인한 내용 게시', exact: true }).waitFor();
      assert.equal(await page.getByRole('checkbox').isChecked(), false);
      assert.equal(await page.getByRole('button', { name: '확인한 내용 게시', exact: true }).isDisabled(), true); await capture('edited-reconsent');
      await page.getByRole('checkbox').check(); await page.getByRole('button', { name: '확인한 내용 게시', exact: true }).click();
      await page.getByRole('button', { name: '공개 철회', exact: true }).click();
      await page.getByText('철회', { exact: true }).first().waitFor(); await capture('withdrawn');
      await open('unknown');
      await page.getByLabel('제목', { exact: true }).fill('응답 확인 연습');
      await page.getByLabel('화물명', { exact: true }).fill('포장 상자');
      await page.getByLabel('출발 지역', { exact: true }).selectOption('sample-jn');
      await page.getByLabel('도착 지역', { exact: true }).selectOption('sample-ddm');
      await page.getByRole('button', { name: '초안 저장', exact: true }).click();
      await page.getByRole('heading', { name: '이전 요청의 결과 확인이 필요합니다' }).waitFor(); await capture('unknown-result');
      await page.getByRole('button', { name: '이전 요청 결과 확인', exact: true }).click();
      await page.getByText('처리 결과와 최신 공고를 확인했습니다.', { exact: true }).waitFor(); await capture('result-recovered');
      await context.close();
    }
    assert.deepEqual(errors, []); assert.deepEqual(blocked, []);
    fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ checks, errors, blocked, success: true, evidence: 'isolated-component-render; not APK or operational publication' }, null, 2));
    console.log(JSON.stringify({ output, checks: checks.length, success: true }));
  } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
