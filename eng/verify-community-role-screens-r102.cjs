// Local, isolated UI verification of the production shared Razor components.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SSALDDEL_PLAYWRIGHT_MODULE || 'playwright');

const root = path.resolve(__dirname, '..');
const evidence = path.join(root, 'artifacts/local/community-role-screens-r102');
const assets = path.join(root, 'docs/assets/changes/2026-10-15-community-role-screens-r102');
fs.mkdirSync(evidence, { recursive: true });
fs.mkdirSync(assets, { recursive: true });
const exchangeOrigin = 'http://127.0.0.1:5389';
const roleOrigin = 'http://127.0.0.1:5482';
const report = { scope: 'Loopback UI; synthetic review data and isolated community SQLite only', views: [], pageErrors: [] };

async function checkView(page, url, ready, name, representative = false) {
    assert.equal(new URL(url).hostname, '127.0.0.1');
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    await page.locator(ready).waitFor({ timeout: 60000 });
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1);
    assert.equal(overflow, false, `${name}: horizontal overflow`);
    await page.screenshot({ path: path.join(representative ? assets : evidence, `${name}.png`), fullPage: true });
    report.views.push({ name, width: page.viewportSize().width, horizontalOverflow: overflow });
}

(async () => {
    const browser = await chromium.launch({ headless: true, executablePath: process.env.SSALDDEL_BROWSER_EXECUTABLE || 'C:/Program Files/Google/Chrome/Application/chrome.exe' });
    try {
        const context = await browser.newContext({ viewport: { width: 390, height: 844 }, locale: 'ko-KR', timezoneId: 'Asia/Seoul' });
        const page = await context.newPage();
        page.on('pageerror', error => report.pageErrors.push(error.message));
        await checkView(page, `${exchangeOrigin}/community/exchange/write?purpose=inbound-interest`, '#exchange-title', 'resident-interest-mobile', true);
        assert.match(await page.locator('h1').innerText(), /어떤 상품/);
        assert.equal(await page.locator('#exchange-intent').count(), 0);
        assert.match(await page.locator('main').innerText(), /주문이나 입고 예약이 아니/);
        await page.setViewportSize({ width: 1024, height: 900 });
        await checkView(page, `${exchangeOrigin}/community/exchange/write?purpose=inbound-interest`, '#exchange-title', 'resident-interest-desktop');
        await page.goto(`${exchangeOrigin}/community/exchange/write`, { waitUntil: 'domcontentloaded' });
        await page.locator('#exchange-intent').waitFor();
        assert.equal(await page.locator('#exchange-intent').inputValue(), '제공해요');

        await page.goto(`${exchangeOrigin}/community/exchange`, { waitUntil: 'domcontentloaded' });
        const entry = page.getByRole('link', { name: /우리 동네에 필요한 상품/ });
        await entry.waitFor();
        await entry.click();
        await page.locator('#exchange-title').waitFor();
        assert.match(page.url(), /purpose=inbound-interest/);
        const title = `화면 검증 r102 · 입고 희망 ${Date.now()}`;
        const body = '원하는 상품: 검토용 생수 2L\n대략 수량: 6개\n필요 시기: 미정\n구매 약속 없는 화면 검증 자료';
        await page.locator('#exchange-title').fill(title);
        await page.locator('#exchange-body').fill(body);
        await page.locator('#exchange-password').fill('r102-local-test-only');
        await page.getByRole('button', { name: '입고 희망 올리기', exact: true }).click();
        // Interactive Server calls the existing HTTP client on the server; the browser sees the resulting navigation.
        await page.waitForURL(/\/community\/exchange\/posts\/\d+/, { timeout: 45000 });
        await page.getByRole('heading', { name: title, exact: true }).waitFor();
        assert.match(await page.locator('main').innerText(), /필요해요/);
        assert.match(await page.locator('main').innerText(), /구매 약속 없는 화면 검증 자료/);
        const id = Number(new URL(page.url()).pathname.split('/').pop());
        const requery = await page.request.get(`${exchangeOrigin}/api/v1/community/posts/${id}`);
        assert.ok(requery.ok(), `post requery status ${requery.status()}`);
        report.post = { id, requeryStatus: requery.status(), roleTag: '필요해요', storage: 'existing community post', automaticOrderOrInbound: false };
        await page.reload({ waitUntil: 'domcontentloaded' });
        await page.getByRole('heading', { name: title, exact: true }).waitFor();
        report.post.requerySucceeded = true;

        for (const width of [320, 390, 1024]) {
            await page.setViewportSize({ width, height: 900 });
            for (const state of ['ready', 'unknown', 'included']) {
                await checkView(page, `${roleOrigin}/community-planning-r102/work-fee?case=${state}`, '[aria-label="내 기본보수 확인"]', `worker-fee-${state}-${width}`, width === 390 && state === 'ready');
                const panel = page.locator('[aria-label="내 기본보수 확인"]');
                assert.equal(await panel.locator('button').count(), 0);
                if (state === 'unknown') assert.match(await panel.innerText(), /미확정/);
                if (state === 'included') {
                    await panel.locator('summary').click();
                    assert.match(await panel.innerText(), /별도 지급액으로 더하지 않습니다/);
                }
            }
            for (const state of ['planned', 'changed', 'unknown']) {
                await checkView(page, `${roleOrigin}/community-planning-r102/inbound?case=${state}`, '[aria-label="상품별 입고 일정 요약"]', `inbound-${state}-${width}`, width === 390 && state === 'changed');
                const panel = page.locator('[aria-label="상품별 입고 일정 요약"]');
                assert.equal(await panel.locator('button').count(), 0);
                if (state === 'changed') assert.match(await panel.innerText(), /이전 확정 예약/);
                if (state === 'unknown') assert.match(await panel.innerText(), /미확인/);
            }
        }
        assert.equal(report.pageErrors.length, 0, JSON.stringify(report.pageErrors));
        fs.writeFileSync(path.join(evidence, 'browser-verification.json'), JSON.stringify(report, null, 2));
        console.log(JSON.stringify({ passed: true, views: report.views.length, post: report.post, pageErrors: report.pageErrors.length }));
    } finally {
        await browser.close();
    }
})().catch(error => {
    fs.writeFileSync(path.join(evidence, 'browser-failure.json'), JSON.stringify({ message: error.message, report }, null, 2));
    console.error(error.message);
    process.exitCode = 1;
});
