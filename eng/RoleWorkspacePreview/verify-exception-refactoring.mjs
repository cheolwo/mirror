import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';

const require = createRequire('C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/package.json');
const { chromium } = require('playwright');
const base = 'http://127.0.0.1:5398';
const root = path.resolve('artifacts/local/exception-continuity-refactoring-r2-20261010');
const output = path.join(root, 'ui');
fs.mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const context = await browser.newContext({ viewport: { width: 390, height: 844 }, locale: 'ko-KR' });
const page = await context.newPage();
const checks = [], failures = [], errors = [], requests = [], after = [], styleReferences = [], supportStyles = [];
page.on('pageerror', error => errors.push(String(error)));
await context.route('**/*', async route => {
  const url = new URL(route.request().url());
  const external = url.origin !== base && !['data:', 'blob:'].includes(url.protocol);
  const business = url.origin === base && url.pathname.startsWith('/api/');
  requests.push({ method: route.request().method(), path: external ? 'external-blocked' : url.pathname, external, business });
  if (external || business) return route.abort();
  return route.continue();
});

const normalize = html => html.replace(/<!--[\s\S]*?-->/g, '').replace(/\s+b-[a-z0-9]+(?:="[^"]*")?/gi, '')
  .replace(/\s+/g, ' ').replace(/>\s+</g, '><').trim();
async function check(name, fn) {
  try { await fn(); checks.push(name); }
  catch (error) { failures.push({ name, error: String(error) }); }
}
async function count(attribute, expected) {
  await page.waitForFunction(([key, value]) => document.querySelector('output')?.getAttribute(key) === value, [attribute, String(expected)]);
}
async function capture(name) {
  await check(name + ': 가로 넘침 없음', async () => assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)));
  await page.screenshot({ path: path.join(output, name + '-390.png'), fullPage: true });
}
async function open(scenario) {
  await page.goto(base + '/exception-refactoring-preview/' + scenario);
  await count('data-refactoring-scenario', scenario);
}
async function foodConfirmation() {
  await page.getByRole('button', { name: '요청 내용 확인', exact: true }).click();
  await page.getByRole('heading', { name: '이 내용으로 요청합니다.', exact: true }).waitFor();
}
async function readActionDom(scenario, target = page) {
  return target.locator('.food-role-action').evaluate((section, scenario) => ({
    scenario, html: section.innerHTML,
    styles: [...section.querySelectorAll('input,select,textarea')].map(node => {
      const style = getComputedStyle(node), rect = node.getBoundingClientRect();
      return { tag: node.tagName, font: style.font, padding: style.padding, border: style.border, width: rect.width, height: rect.height };
    })
  }), scenario);
}
async function renderOriginalStyleReference(original) {
  // The first live baseline had no component stylesheet. Preserve that evidence
  // and compare against its captured markup plus the original CSS source instead.
  const source = fs.readFileSync(path.join(root, 'before/FoodRoleActionForm.razor.css'), 'utf8');
  const scope = original.html.match(/\b(b-[a-z0-9]+)=""/)?.[1];
  assert.ok(scope, 'Original CSS isolation attribute is required.');
  const scoped = source.replace(/([^{}]+)\{/g, (_, selectors) => selectors.split(',')
    .map(selector => selector.trim().replace(/(:disabled)?$/, `[${scope}]$1`)).join(', ') + ' {');
  const themePath = 'Ssalddel.Ui.Common/obj/Debug/net10.0/scopedcss/projectbundle/Ssalddel.Ui.Common.bundle.scp.css';
  const theme = fs.readFileSync(themePath, 'utf8');
  const marker = /\/\* _content\/Ssalddel\.Ui\.Common\/Areas\/App\/RoleWorkspace\/Food\/FoodRoleActionForm\.razor\.rz\.scp\.css \*\/[\s\S]*?(?=\/\*|$)/;
  assert.ok(marker.test(theme), 'Current form CSS block must be removed from the reference theme.');
  const framing = await page.evaluate(({ html, scope }) => {
    const clone = document.documentElement.cloneNode(true);
    clone.querySelectorAll('script').forEach(script => script.remove());
    clone.querySelector('base').href = location.origin + '/';
    const section = clone.querySelector('.food-role-action');
    section.innerHTML = html; section.setAttribute(scope, '');
    return '<!DOCTYPE html>' + clone.outerHTML;
  }, { html: original.html, scope });
  const reference = await context.newPage();
  await reference.route('**/Ssalddel.Ui.Common*.bundle.scp.css', route =>
    route.fulfill({ status: 200, contentType: 'text/css', body: theme.replace(marker, '') }));
  await reference.setContent(framing);
  await reference.addStyleTag({ content: scoped });
  const result = await readActionDom(original.scenario, reference);
  assert.equal(result.styles[0].height, 44, 'Original input styling must be applied.');
  await reference.screenshot({ path: path.join(output, 'reference-' + original.scenario + '-390.png'), fullPage: true });
  styleReferences.push(result);
  await reference.close();
  return result;
}
async function inspectSupportStyles(scenario) {
  const values = await page.evaluate(() => {
    const panel = document.querySelector('section[aria-label="연결된 배송 복구"]');
    const ordinary = document.querySelector('.support-workspace > section');
    const navButton = document.querySelector('.support-workspace > nav button');
    const fields = node => {
      const s = getComputedStyle(node), r = node.getBoundingClientRect();
      return { font: s.font, padding: s.padding, border: s.border, borderRadius: s.borderRadius,
        minHeight: s.minHeight, display: s.display, width: r.width, height: r.height };
    };
    return { section: fields(panel), ordinarySection: fields(ordinary), heading: fields(panel.querySelector('h2')),
      ordinaryHeading: fields(ordinary.querySelector('h2')), button: fields(panel.querySelector('button')),
      navButton: fields(navButton), select: fields(panel.querySelector('select')), fieldset: fields(panel.querySelector('fieldset')) };
  });
  supportStyles.push({ scenario, ...values });
  await check(scenario + ': Panel 카드가 부모 카드와 같은 외형', () => {
    for (const key of ['padding', 'border', 'borderRadius']) assert.equal(values.section[key], values.ordinarySection[key], key);
    for (const key of ['font', 'padding']) assert.equal(values.heading[key], values.ordinaryHeading[key], key);
    for (const key of ['font', 'padding', 'borderRadius', 'minHeight']) assert.equal(values.button[key], values.navButton[key], key);
    assert.equal(values.select.minHeight, '48px'); assert.equal(values.select.padding, '10px 12px');
    assert.equal(values.fieldset.display, 'grid');
  });
}

try {
  const before = JSON.parse(fs.readFileSync(path.join(root, 'before/action-dom.json'), 'utf8'));
  for (const original of before) {
    await page.goto(base + '/action-preview/' + original.scenario);
    await page.getByLabel('취소 사유').waitFor();
    const current = await readActionDom(original.scenario); after.push(current);
    await check(original.scenario + ': 기존 주문자 DOM 유지', () => assert.equal(normalize(current.html), normalize(original.html)));
    const reference = await renderOriginalStyleReference(original);
    await check(original.scenario + ': 원본 DOM·CSS 참조와 입력 computed style 일치', () => assert.deepEqual(current.styles, reference.styles));
    await capture('baseline-' + original.scenario);
  }

  await open('orderer'); await page.getByLabel('취소 사유').selectOption({ label: '단순 변심' });
  await page.getByLabel('사유·메모').fill('입력 보존 확인'); await foodConfirmation();
  await check('주문자: 확인 전 요청 버튼 비활성', async () => assert.equal(await page.getByRole('button', { name: '확인하고 요청', exact: true }).isDisabled(), true));
  await page.getByRole('button', { name: '입력으로 돌아가기', exact: true }).click();
  await check('주문자: 입력 복귀 시 값 보존', async () => assert.equal(await page.getByLabel('사유·메모').inputValue(), '입력 보존 확인'));
  await capture('orderer-input'); await count('data-food-writes', 0);

  await open('restaurant-accept'); await page.getByLabel('음식점 이름', { exact: true }).waitFor();
  await page.getByLabel('지금 픽업할 수 있습니다.').check();
  await page.getByLabel('이번 주문 조리 예상 시간(분)').waitFor({ state: 'detached' });
  await check('음식점 수락: 즉시 픽업 토글이 조리시간 입력을 숨김', async () => assert.equal(await page.getByLabel('이번 주문 조리 예상 시간(분)').count(), 0));
  await foodConfirmation();
  await check('음식점 수락: 자식 토글을 부모 확인 요약에 반영', async () => assert.equal(await page.getByText('즉시 픽업 가능', { exact: true }).count(), 1));
  await capture('restaurant-immediate-confirmation');
  await page.getByRole('button', { name: '입력으로 돌아가기', exact: true }).click();
  await page.getByLabel('지금 픽업할 수 있습니다.').uncheck();
  await page.getByLabel('이번 주문 조리 예상 시간(분)').fill('22'); await foodConfirmation();
  await check('음식점 수락: 조리시간 변경을 부모 확인 요약에 반영', async () => assert.equal(await page.getByText('조리 예상 22분', { exact: true }).count(), 1));
  await capture('restaurant-preparation-confirmation'); await count('data-food-writes', 0);

  await open('restaurant-time'); await page.getByLabel('이번 주문 조리 예상 시간(분)').fill('18'); await foodConfirmation();
  await check('조리시간 변경: 역할 입력과 확인 요약 연결', async () => assert.equal(await page.getByText('조리 예상 18분', { exact: true }).count(), 1));
  await capture('restaurant-time-confirmation'); await count('data-food-writes', 0);

  await open('food-driver'); await page.getByLabel('이번 배달 중단 사유').selectOption({ label: '배터리 부족' });
  await page.getByLabel('사유·메모').fill('안전한 장소에 정차했습니다.'); await foodConfirmation();
  await capture('driver-interruption-confirmation'); await count('data-food-writes', 0);

  await open('operator'); await page.getByLabel('이번 중단 건 책임 판정').selectOption({ label: '보호 사유' });
  await page.getByLabel('판정 근거').fill('기록을 확인했습니다.'); await foodConfirmation();
  await capture('operator-review-confirmation'); await count('data-food-writes', 0);

  await open('support-stored'); await page.getByRole('button', { name: '연결된 배송 확인', exact: true }).click();
  await page.getByLabel('배송 처리', { exact: true }).waitFor();
  await inspectSupportStyles('support-stored');
  await check('운영자 복구: 명시 확인 전 전송 차단', async () => assert.equal(await page.getByRole('button', { name: '선택한 배송 처리 요청', exact: true }).isDisabled(), true));
  await page.getByLabel('배송 처리', { exact: true }).selectOption('CancelBeforePickup');
  await page.getByLabel('안내한 처리방안과 현재 배송을 확인했습니다.').check();
  await page.getByRole('button', { name: '선택한 배송 처리 요청', exact: true }).click();
  await count('data-recovery-writes', 1);
  await page.getByRole('button', { name: '결과 다시 확인', exact: true }).waitFor();
  await check('운영자 복구: 선택 변경이 원 명령에 반영', () => count('data-recovery-last-resolution', 'CancelBeforePickup'));
  await check('운영자 복구: 저장 후 응답 유실을 완료로 표시하지 않음', async () => assert.equal(await page.getByText('배송 복구 요청의 처리 기록을 확인했습니다.', { exact: false }).count(), 0));
  await capture('support-stored-lost-response');
  await page.getByRole('button', { name: '결과 다시 확인', exact: true }).click();
  await page.getByText('배송 복구 요청의 처리 기록을 확인했습니다.', { exact: false }).waitFor();
  await check('운영자 복구: GET 영수증 확인으로 완료하고 POST 추가 없음', () => count('data-recovery-writes', 1));
  await check('운영자 복구: 배송 처리와 수령·정산·지급을 구분', async () => assert.equal(await page.getByText('수령·정산·지급 완료는 별개입니다.', { exact: false }).count(), 1));
  await capture('support-stored-get-confirmed');

  await open('support-retry'); await page.getByRole('button', { name: '연결된 배송 확인', exact: true }).click();
  await page.getByLabel('배송 처리', { exact: true }).selectOption('Resume');
  await page.getByLabel('안내한 처리방안과 현재 배송을 확인했습니다.').check();
  await page.getByRole('button', { name: '선택한 배송 처리 요청', exact: true }).click();
  await count('data-recovery-writes', 1); await page.getByRole('button', { name: '결과 다시 확인', exact: true }).click();
  await page.getByRole('button', { name: '같은 복구 요청 다시 확인', exact: true }).waitFor();
  await check('운영자 복구: 조회만으로 미저장 원 명령을 재전송하지 않음', () => count('data-recovery-writes', 1));
  await check('운영자 복구: 미확정 원 입력 변경 차단', async () => assert.equal(await page.getByLabel('배송 처리', { exact: true }).isDisabled(), true));
  await capture('support-explicit-retry-ready');
  await page.getByRole('button', { name: '같은 복구 요청 다시 확인', exact: true }).click();
  await page.getByText('배송 복구 요청의 처리 기록을 확인했습니다.', { exact: false }).waitFor();
  await check('운영자 복구: 명시 재확인은 같은 번호와 본문', () => count('data-recovery-request-reused', true));
  await check('운영자 복구: 명시 재확인만 두 번째 POST 실행', () => count('data-recovery-writes', 2));
  await capture('support-explicit-retry-confirmed');
  await check('브라우저 실행 오류 없음', () => assert.deepEqual(errors, []));
  await check('운영·외부 HTTP 요청을 시도하지 않음', () => assert.deepEqual(requests.filter(request => request.external || request.business), []));
} catch (error) {
  failures.push({ name: 'UI 실행 경로', error: String(error) });
} finally {
  fs.writeFileSync(path.join(root, 'after-action-dom.json'), JSON.stringify(after, null, 2));
  fs.writeFileSync(path.join(output, 'original-markup-css-reference.json'), JSON.stringify({
    method: 'Captured pre-refactor markup with original CSS selectors scoped by its original isolation attribute. Static reference rendering, not a pre-refactor interactive runtime.',
    originalLiveBaseline: 'before/action-dom.json (component stylesheet was missing; retained without modification)',
    cssSource: 'before/FoodRoleActionForm.razor.css', scenarios: styleReferences
  }, null, 2));
  fs.writeFileSync(path.join(output, 'support-computed-styles.json'), JSON.stringify(supportStyles, null, 2));
  fs.writeFileSync(path.join(output, 'verification.json'), JSON.stringify({ checks, failures, errors, requests,
    verifiedAtUtc: new Date().toISOString(), boundary: '390x844 loopback rendering of real product components with isolated in-memory fixtures. No business HTTP, DB, native camera/map, APK or external notifications.' }, null, 2));
  await browser.close();
}
console.log(JSON.stringify({ checks: checks.length, failures, errors: errors.length, output }));
assert.deepEqual(failures, []);
