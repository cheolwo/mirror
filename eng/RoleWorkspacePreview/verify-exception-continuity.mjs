import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
const { chromium } = createRequire(import.meta.url)('playwright');
const base = 'http://127.0.0.1:5396';
const output = path.resolve('artifacts/local/exception-continuity-hardening-20261010/ui');
fs.mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const context = await browser.newContext({ viewport: { width: 390, height: 844 }, locale: 'ko-KR' });
const page = await context.newPage();
const errors = [], checks = [], requests = [];
page.on('pageerror', error => errors.push(String(error)));
await context.route('**/*', async route => {
  const url = new URL(route.request().url());
  const external = url.origin !== base && !['data:', 'blob:'].includes(url.protocol);
  const business = url.origin === base && url.pathname.startsWith('/api/') && !url.pathname.includes('/platform/runtime/google-maps');
  requests.push({ method: route.request().method(), path: external ? 'external-blocked' : url.pathname, external, business });
  if (external || business) return route.abort();
  if (url.pathname.includes('/platform/runtime/google-maps')) return route.fulfill({ status: 204 });
  return route.continue();
});
async function check(name, fn) { await fn(); checks.push(name); }
async function count(attribute, expected) { await page.waitForFunction(([a, e]) => document.querySelector('output')?.getAttribute(a) === e, [attribute, String(expected)]); }
try {
  await page.goto(base + '/action-preview/lost-response');
  await page.getByLabel('취소 사유').selectOption({ label: '단순 변심' });
  await page.getByRole('button', { name: '요청 내용 확인', exact: true }).click();
  await page.getByLabel('대상과 요청 내용을 확인했습니다.').check();
  await page.getByRole('button', { name: '확인하고 요청', exact: true }).click();
  await check('응답 유실 후 원 요청 유지', () => count('data-preview-writes', 1));
  await check('응답 유실을 완료로 확정하지 않음', () => count('data-preview-completed', false));
  await page.screenshot({ path: path.join(output, 'food-lost-response-390.png'), fullPage: true });
  await page.getByRole('button', { name: '결과 다시 확인', exact: true }).click();
  await page.getByRole('button', { name: '같은 요청 다시 확인', exact: true }).waitFor();
  await check('GET만으로 같은 상태를 원 요청 성공으로 단정하지 않음', () => count('data-preview-writes', 1));
  await check('미확정 입력을 자동 재전송하지 않음', () => count('data-preview-completed', false));
  await page.screenshot({ path: path.join(output, 'food-explicit-recovery-390.png'), fullPage: true });
  await page.getByRole('button', { name: '같은 요청 다시 확인', exact: true }).click();
  await check('명시 재확인은 같은 번호와 본문 사용', () => count('data-preview-request-reused', true));
  await check('명령 응답과 정본 확인 후 완료', () => count('data-preview-completed', true));
  await page.goto(base + '/neighborhood-member-delivery-preview/assigned');
  await page.getByRole('button', { name: '지도 다시 표시', exact: true }).waitFor();
  await check('지도 장애에도 현재 업무 카드 유지', async () => assert.ok(await page.getByRole('button', { name: '사진 보내고 픽업 완료', exact: true }).count()));
  await page.getByRole('button', { name: '지도 다시 표시', exact: true }).click();
  await page.getByRole('button', { name: '지도 다시 표시', exact: true }).waitFor();
  await check('지도 명시 재시도 후 완료 요청을 대신 실행하지 않음', async () => assert.equal(await page.getByRole('button', { name: '사진 보내고 픽업 완료', exact: true }).isDisabled(), true));
  await page.screenshot({ path: path.join(output, 'member-map-unavailable-390.png'), fullPage: true });
  await check('휴대폰 폭에 가로 넘침 없음', async () => assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)));
  await check('브라우저 오류 없음', async () => assert.deepEqual(errors, []));
} finally {
  fs.writeFileSync(path.join(output, 'verification.json'), JSON.stringify({ checks, errors, requests, verifiedAtUtc: new Date().toISOString(), boundary: 'Loopback product UI with isolated fixtures; no operational DB, native map, camera or external notifications.' }, null, 2));
  await browser.close();
}
console.log(JSON.stringify({ checks: checks.length, errors: errors.length, output }));
