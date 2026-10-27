const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { chromium } = require('playwright');

(async () => {
  const startedAtUtc = new Date().toISOString();
  const nestedScroll = process.env.SSALDDEL_MOBILE_CONTEXT_NESTED_SCROLL === '1';
  const base = process.env.SSALDDEL_MOBILE_CONTEXT_PREVIEW_URL || 'http://127.0.0.1:5595';
  assert.equal(new URL(base).hostname, '127.0.0.1');
  const output = path.resolve(process.env.SSALDDEL_MOBILE_CONTEXT_PREVIEW_OUTPUT || `artifacts/local/mobile-context-r72-${Date.now()}`);
  assert.ok(output.startsWith(path.resolve('artifacts/local') + path.sep)); fs.mkdirSync(output, { recursive: false });
  const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM
    ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : { channel: 'msedge' }) });
  const checks = [], errors = [], blocked = [], captures = [], scrollTransitions = [];
  const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
  const scriptSha256 = hash(__filename);
  const inputHashes = Object.fromEntries(['RoleWorkspacePreview.dll', 'Ssalddel.Ui.Common.dll'].map(file => {
    const target = path.resolve('eng/RoleWorkspacePreview/bin/Debug/net10.0', file);
    return [file, hash(target)];
  }));
  let currentPage;
  try {
    for (const width of [320, 390, 1280]) {
      const context = await browser.newContext({ viewport: { width, height: nestedScroll ? 640 : 900 }, locale: 'ko-KR' });
      const page = await context.newPage(); currentPage = page; page.setDefaultTimeout(10000);
      page.on('pageerror', error => errors.push(String(error)));
      await context.route('**/*', route => {
        const url = new URL(route.request().url());
        if ((url.origin !== base && !['data:', 'blob:'].includes(url.protocol)) || url.pathname.startsWith('/api/')) {
          blocked.push({ method: route.request().method(), pathname: url.pathname }); return route.abort();
        }
        return route.continue();
      });
      const stepArea = step => page.locator(`[data-mobile-context-preview="${step}"]`);
      const next = () => page.locator('.shipper-mobile-navigation__next');
      async function ready(step) {
        await stepArea(step).waitFor();
        await page.waitForFunction(current => {
          const heading = document.querySelector(`[data-mobile-context-preview="${current}"] h2`);
          const shell = document.querySelector('.shipper-mobile-shell__content');
          if (!heading || document.activeElement !== heading) return false;
          const title = heading.getBoundingClientRect();
          const viewport = shell ? shell.getBoundingClientRect() : { top: 0, bottom: innerHeight };
          return title.top >= viewport.top - 1 && title.bottom <= viewport.bottom + 1;
        }, step);
      }
      async function nextEnabled() { await page.waitForFunction(() => { const button = document.querySelector('.shipper-mobile-navigation__next'); return button && !button.disabled; }); }
      async function onward(step) {
        await nextEnabled();
        const before = nestedScroll ? await page.evaluate(() => {
          const shell = document.querySelector('.shipper-mobile-shell__content');
          shell.scrollTop = shell.scrollHeight;
          return { scrollTop: shell.scrollTop, headingTop: shell.querySelector('h2').getBoundingClientRect().top };
        }) : null;
        await next().click(); await ready(step);
        if (nestedScroll) scrollTransitions.push({ width, step, before, after: await page.evaluate(() => {
          const shell = document.querySelector('.shipper-mobile-shell__content');
          const heading = shell.querySelector('h2'); const title = heading.getBoundingClientRect(); const viewport = shell.getBoundingClientRect();
          return { scrollTop: shell.scrollTop, headingTop: title.top, headingBottom: title.bottom,
            viewportTop: viewport.top, viewportBottom: viewport.bottom, focused: document.activeElement === heading };
        }) });
      }
      async function capture(step) {
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `${width}/${step}: horizontal overflow`);
        // 입력 중 다음 버튼 활성화 검사는 위에서 별도로 끝낸다. 캡처는 포커스와 전환 효과가 끝난 화면으로 남긴다.
        await page.evaluate(() => document.activeElement?.blur());
        if (step === 'schedule') {
          await page.waitForFunction(() => [...document.querySelectorAll('input[type="datetime-local"]')].slice(0, 2)
            .every(input => input.value && input.getAttribute('aria-invalid') !== 'true'));
          assert.equal(await page.getByText('Not a valid date time', { exact: true }).count(), 0);
        }
        const file = `${step}-${width}.png`; const target = path.join(output, file);
        await page.screenshot({ path: target, fullPage: true, animations: 'disabled' }); const png = fs.readFileSync(target);
        captures.push({ width, step, file, imageWidth: png.readUInt32BE(16), imageHeight: png.readUInt32BE(20), sha256: hash(target) });
      }
      await page.goto(`${base}/shipper/request/cargo?sourcePostId=12&returnPath=%2Fmobile-context-preview%2Fdesktop${nestedScroll ? '&nestedScroll=1' : ''}`, { waitUntil: 'domcontentloaded' });
      await ready('cargo'); const draftId = await stepArea('cargo').getAttribute('data-draft-id');
      if (nestedScroll) assert.ok(await page.locator('[data-nested-scroll-preview="true"] .shipper-mobile-shell__content').isVisible());
      assert.ok(await next().isDisabled()); assert.equal(await page.getByLabel('전체 중량 kg (포장·깔판 포함)', { exact: true }).count(), 0);
      await page.getByLabel('화물 종류', { exact: true }).fill('필기구'); await nextEnabled();
      assert.ok(await page.getByLabel('화물 종류', { exact: true }).evaluate(element => element === document.activeElement));
      await capture('cargo'); await onward('package');
      assert.ok(await next().isDisabled()); assert.equal(await page.getByLabel('화물 종류', { exact: true }).count(), 0);
      await page.getByLabel('전체 중량 kg (포장·깔판 포함)', { exact: true }).fill('3.5');
      await page.getByLabel('포장한 화물 길이 mm', { exact: true }).fill('300');
      await page.getByLabel('포장한 화물 너비 mm', { exact: true }).fill('200');
      assert.ok(await next().isDisabled()); const height = page.getByLabel('포장한 화물 높이 mm', { exact: true });
      await height.fill('100'); await nextEnabled(); assert.ok(await height.evaluate(element => element === document.activeElement));
      await page.getByRole('button', { name: /^이전 ·/ }).click(); await ready('cargo');
      assert.equal(await page.getByLabel('화물 종류', { exact: true }).inputValue(), '필기구');
      await onward('package'); assert.equal(await page.getByLabel('포장한 화물 높이 mm', { exact: true }).inputValue(), '100');
      await capture('package'); await onward('pickup');
      assert.ok(await next().isDisabled()); assert.equal(await page.getByLabel('하차 도로명 주소', { exact: true }).count(), 0);
      assert.equal(await page.getByLabel('상차 담당자 연락처', { exact: true }).getAttribute('type'), 'tel');
      await page.getByLabel('상차 도로명 주소', { exact: true }).fill('상차 예시지역');
      await page.getByLabel('상차 담당자', { exact: true }).fill('담당자 1');
      await capture('pickup'); await onward('dropoff');
      assert.equal(await page.getByLabel('상차 도로명 주소', { exact: true }).count(), 0);
      await page.getByLabel('하차 도로명 주소', { exact: true }).fill('전달 예시지역');
      await page.getByLabel('하차 담당자', { exact: true }).fill('담당자 2');
      await capture('dropoff'); await onward('schedule');
      assert.ok(await next().isDisabled()); assert.equal(await page.getByLabel('상차 도로명 주소', { exact: true }).count(), 0);
      await page.getByLabel('상차 시작 (한국시간)', { exact: true }).fill('2026-10-20T10:00');
      await page.getByLabel('상차 종료 (한국시간)', { exact: true }).fill('2026-10-20T11:00');
      await nextEnabled(); await capture('schedule'); await onward('vehicle');
      assert.equal(await page.getByLabel('결제 예정 금액', { exact: true }).count(), 0);
      await page.getByRole('radio', { name: /오토바이 퀵/ }).click();
      await capture('vehicle'); await onward('payment');
      assert.equal(await page.getByRole('radiogroup', { name: '차량 종류', exact: true }).count(), 0);
      await page.getByRole('button', { name: /^계좌이체/ }).click();
      await page.getByLabel('결제 예정 금액', { exact: true }).fill('6000');
      await capture('payment'); await onward('review');
      assert.equal(await stepArea('review').getAttribute('data-draft-id'), draftId);
      assert.equal(await next().count(), 0); assert.ok(await page.getByRole('button', { name: '운송 의뢰 등록', exact: true }).isDisabled());
      for (const [message, target] of [['상차 담당자 연락처', '/pickup'], ['하차 담당자 연락처', '/dropoff'], ['기사 지급 예정 운임', '/procedure']]) {
        const warningLink = page.locator('.shipper-mobile-review-check').filter({ hasText: message }).getByRole('link', { name: '확인', exact: true });
        assert.ok((await warningLink.getAttribute('href')).includes(target), `${width}: warning link for ${message}`);
      }
      await page.locator('.shipper-mobile-review-section a[href*="/package"]').click(); await ready('package');
      await page.getByLabel('포장한 화물 높이 mm', { exact: true }).fill('');
      await page.goBack(); await ready('review');
      const missingHeight = page.locator('.shipper-mobile-review-check').filter({ hasText: '높이' });
      const correction = missingHeight.getByRole('link', { name: '보완', exact: true });
      assert.ok((await correction.getAttribute('href')).includes('shipper/request/package'));
      assert.ok((await correction.getAttribute('href')).includes('sourcePostId=12'));
      await correction.click(); await ready('package');
      await page.getByLabel('포장한 화물 높이 mm', { exact: true }).fill('100');
      for (const step of ['pickup', 'dropoff', 'schedule', 'vehicle', 'payment', 'review']) await onward(step);
      await capture('review'); checks.push({ width, check: 'eight-contexts-live-next-validation-back-retention-correction-links-and-no-submission' });
      if (nestedScroll) {
        assert.ok(scrollTransitions.some(item => item.width === width && item.before.scrollTop > 100), `${width}: nested scroll was not exercised`);
        checks.push({ width, check: 'nested-scroll-new-step-heading-visible-and-focused-same-step-input-focus-retained' });
      }
      await page.goto(`${base}/mobile-context-preview/desktop`, { waitUntil: 'domcontentloaded' });
      await stepArea('desktop').waitFor();
      assert.equal(await page.locator('.shipper-request-authoring__layout').isVisible(), width > 720);
      assert.equal(await page.locator('.shipper-request-authoring__mobile-entry').isVisible(), width <= 720);
      if (width > 720) { assert.equal(await page.locator('.shipper-request-screen').count(), 4); await capture('desktop'); }
      else { assert.ok(await page.getByRole('link', { name: '단계별 입력 시작', exact: true }).isVisible()); }
      checks.push({ width, check: 'desktop-legacy-four-screen-or-narrow-mobile-entry' });
      console.log(JSON.stringify({ width, checksComplete: true, captures: captures.length }));
      await context.close();
    }
    assert.equal(errors.length, 0, errors.join('\n')); assert.equal(blocked.length, 0);
  } catch (error) {
    errors.push(String(error.stack || error));
    if (currentPage && !currentPage.isClosed()) {
      await currentPage.screenshot({ path: path.join(output, 'failure.png'), fullPage: true }).catch(() => {});
      fs.writeFileSync(path.join(output, 'failure-body.txt'), await currentPage.locator('body').innerText().catch(() => 'unavailable'));
    }
    throw error;
  } finally {
    fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ base, nestedScroll, startedAtUtc, completedAtUtc: new Date().toISOString(), inputHashes, scriptSha256, scope: 'Common Razor eight-context preview with scoped in-memory draft only; no API, database, real consent, submission or device.', checks, errors, blocked, scrollTransitions, captures }, null, 2));
    await browser.close();
  }
  console.log(JSON.stringify({ passed: checks.length, captures: captures.length, output }));
})().catch(error => { console.error(error); process.exitCode = 1; });
