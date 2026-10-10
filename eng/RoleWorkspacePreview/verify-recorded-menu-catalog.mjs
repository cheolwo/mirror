import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
const { chromium } = createRequire(import.meta.url)('playwright');

const base = new URL(process.env.SSALDDEL_MENU_PREVIEW_URL ?? 'http://127.0.0.1:5584');
if (base.protocol !== 'http:' || base.hostname !== '127.0.0.1' || base.pathname !== '/' || base.username || base.password)
  throw Error('Loopback preview only.');
const repo = path.resolve(import.meta.dirname, '../..');
const localRoot = path.join(repo, 'artifacts', 'local') + path.sep;
const output = path.resolve(process.env.SSALDDEL_MENU_PREVIEW_OUTPUT ?? path.join(localRoot, 'orderer-menu-catalog-r55', 'ui'));
if (!output.toLowerCase().startsWith(localRoot.toLowerCase())) throw Error('Evidence must remain in artifacts/local.');
const binaryDirectory = path.resolve(process.env.SSALDDEL_MENU_PREVIEW_BINARY_DIR ?? path.join(localRoot, 'orderer-menu-catalog-r55', 'host'));
if (!binaryDirectory.toLowerCase().startsWith(localRoot.toLowerCase())) throw Error('The verified preview binaries must remain in artifacts/local.');
const mode = process.env.SSALDDEL_MENU_EXPECT ?? 'loaded';
if (!['loaded', 'missing', 'invalid', 'operational'].includes(mode)) throw Error('Unknown expected input state.');
const datasetPath = process.env.SSALDDEL_ORDERER_MENU_CATALOG;
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const money = value => value > 0 ? new Intl.NumberFormat('ko-KR').format(value) + '원' : '미확인';
let dataset, rows = [];
if (mode === 'loaded') {
  if (!datasetPath || !path.isAbsolute(datasetPath) || !path.resolve(datasetPath).toLowerCase().startsWith(localRoot.toLowerCase()))
    throw Error('An explicit private local catalog is required.');
  dataset = JSON.parse(fs.readFileSync(datasetPath, 'utf8'));
  if (!dataset.localVerificationOnly || dataset.executionModeCode !== 'Simulation' || dataset.operationalRegistration || dataset.publicDisplayApproved || dataset.publicUseApproved)
    throw Error('Local Simulation review input only.');
  const branches = new Map(dataset.branches.map(x => [x.branchId, x]));
  const orders = new Map(dataset.orders.map(x => [x.recordId, x]));
  rows = dataset.menus.flatMap(menu => menu.observations.map((observation, index) => ({
    ...observation, id: menu.menuId + ':' + index, menuName: menu.rawMenu, branchId: menu.branchId,
    branchName: branches.get(menu.branchId).displayName, orderAmountKrw: orders.get(observation.recordId).foodOrderAmountKrw,
    photo: menu.imageCandidates[0]
  })));
  if (rows.some(x => x.unitPriceKrw != null)) throw Error('This review expects the unconfirmed historical unit prices to remain null.');
}
const sources = [
  'eng/RoleWorkspacePreview/RecordedMenuCatalogDataset.cs', 'eng/RoleWorkspacePreview/RecordedMenuCatalogPreviewFixture.cs',
  'eng/RoleWorkspacePreview/RecordedMenuSellingPreviewFixture.cs', 'eng/RoleWorkspacePreview/Pages/RecordedMenuCatalogPreview.razor',
  'eng/RoleWorkspacePreview/Pages/RecordedMenuCatalogPreview.razor.css', 'eng/RoleWorkspacePreview/Pages/RecordedMenuSellingPreview.razor',
  'eng/RoleWorkspacePreview/Pages/RecordedMenuSellingPreview.razor.css', 'eng/RoleWorkspacePreview/Program.cs',
  'eng/RoleWorkspacePreview/verify-recorded-menu-catalog.mjs',
  'Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererRecordedMenuCatalog.razor',
  'Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererRecordedMenuCatalog.razor.css',
  'Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererRecordedMenuPresentation.cs',
  'Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererMenuImage.razor',
  'Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererMenuImage.razor.css',
  'Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererRestaurantDetailPanel.razor',
  'Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererRestaurantDetailPanel.razor.css',
  path.relative(repo, path.join(binaryDirectory, 'Ssalddel.Ui.Common.dll')),
  path.relative(repo, path.join(binaryDirectory, 'RoleWorkspacePreview.dll'))
];
const hashes = () => sources.map(file => ({ path: file, sha256: hash(path.join(repo, file)) }));
const before = hashes(), passes = [], failures = [];
fs.mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : {}) });
try {
  for (const width of [320, 390, 1280]) {
    const context = await browser.newContext({ viewport: { width, height: 900 }, locale: 'ko-KR' });
    const blocked = [], errors = [];
    await context.route('**/*', route => {
      const url = new URL(route.request().url());
      if (url.origin !== base.origin || url.pathname.startsWith('/api/')) {
        blocked.push({ external: url.origin !== base.origin, businessApi: url.pathname.startsWith('/api/') });
        return route.abort();
      }
      return route.continue();
    });
    const page = await context.newPage();
    page.on('pageerror', error => errors.push(error.message));
    const layout = async () => {
      if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)) throw Error('Horizontal overflow.');
      if (blocked.length || errors.length) throw Error('External/business request or browser exception: ' + JSON.stringify({ blocked, errors }));
    };
    const assertRows = async expected => {
      const ids = expected.map(x => x.id).sort();
      await page.waitForFunction(values => JSON.stringify([...document.querySelectorAll('[data-recorded-menu-id]')].map(x => x.dataset.recordedMenuId).sort()) === JSON.stringify(values), ids);
      for (const row of expected) {
        const card = page.locator(`[data-recorded-menu-id="${row.id}"]`);
        if (await card.getAttribute('data-recorded-menu-branch-id') !== row.branchId || await card.getAttribute('data-public-use-approved') !== 'false')
          throw Error('Branch or public-use boundary differs.');
        const amounts = await card.locator('dl > div').evaluateAll(elements => Object.fromEntries(elements.map(x => [x.querySelector('dt').textContent.trim(), x.querySelector('dd').textContent.trim()])));
        if (amounts['기록된 단가'] !== '미확인') throw Error('An unconfirmed unit price was invented.');
        if (row.displayedItemAmountKrw != null && amounts['당시 메뉴 금액'] !== money(row.displayedItemAmountKrw)) throw Error('Displayed item amount differs.');
        if (row.orderAmountKrw != null && amounts['당시 주문 합계'] !== money(row.orderAmountKrw)) throw Error('Whole order amount differs.');
      }
      await layout();
    };
    const filtered = (branch, query) => rows.filter(x => (!branch || x.branchId === branch) && (!query ||
      [x.menuName, x.branchName, ...(x.options ?? [])].some(value => value.toLowerCase().includes(query.trim().toLowerCase()))));
    const select = async row => {
      await page.getByTestId('recorded-menu-search').fill('');
      await page.getByTestId('recorded-menu-branch').selectOption(row.branchId);
      await page.getByTestId('recorded-menu-search').fill(row.menuName);
      await assertRows(filtered(row.branchId, row.menuName));
    };
    try {
      await page.goto(new URL('/recorded-menu-catalog-preview', base).href, { waitUntil: 'networkidle' });
      await page.locator('[data-menu-catalog-ready]').waitFor();
      if (mode !== 'loaded') {
        if (await page.locator('[data-menu-catalog-loaded]').getAttribute('data-menu-catalog-loaded') !== 'false' || await page.getByTestId('recorded-menu-card').count()) throw Error('Input failure displayed fallback menus.');
        const body = await page.locator('main').innerText();
        const notice = { missing: '선택하지 않았습니다', invalid: '읽지 못했습니다', operational: '개인 검토용 자료만' }[mode];
        if (!body.includes(notice)) throw Error('Expected input notice absent.');
        const denied = await context.request.get(new URL('/_local/orderer-menu-images/not-registered', base).href);
        if (denied.status() !== 404) throw Error('No-input image endpoint was open.');
        await layout();
        await page.screenshot({ path: path.join(output, `${mode}-${width}.png`), fullPage: true });
        passes.push({ name: `${mode}-${width}`, noFallback: true });
        continue;
      }
      if (await page.locator('[data-menu-catalog-loaded]').getAttribute('data-menu-catalog-loaded') !== 'true') throw Error('Catalog not loaded.');
      await page.getByTestId('recorded-menu-catalog').waitFor();
      const initialCards = await page.locator('[data-recorded-menu-id]').evaluateAll(elements => elements.map(x => ({
        id: x.dataset.recordedMenuId, branchId: x.dataset.recordedMenuBranchId, publicUseApproved: x.dataset.publicUseApproved,
        amounts: Object.fromEntries([...x.querySelectorAll('dl > div')].map(part => [part.querySelector('dt').textContent.trim(), part.querySelector('dd').textContent.trim()]))
      })));
      const visibleIds = initialCards.map(x => x.id).sort();
      if (JSON.stringify(visibleIds) !== JSON.stringify(rows.map(x => x.id).sort())) throw Error('Initial observation identities differ.');
      const initialById = new Map(initialCards.map(x => [x.id, x]));
      for (const row of rows) {
        const card = initialById.get(row.id);
        if (card.branchId !== row.branchId || card.publicUseApproved !== 'false' || card.amounts['기록된 단가'] !== '미확인') throw Error('An initial branch/unit-price/public-use boundary differs.');
        for (const [field, label] of [['displayedItemAmountKrw', '당시 메뉴 금액'], ['orderAmountKrw', '당시 주문 합계']]) {
          if (row[field] != null ? card.amounts[label] !== money(row[field]) : Object.hasOwn(card.amounts, label)) throw Error('An initial observed amount differs.');
        }
      }
      if (await page.locator('button, input[type="number"], input[type="submit"]').count()) throw Error('Read-only catalog exposed order/quantity actions.');
      const text = await page.locator('main').innerText();
      if (!text.includes('현재 판매가') || !text.includes('공개 사용 승인은 별도')) throw Error('Price/image provenance notice absent.');

      const sample = rows.find(x => x.photo && x.displayedItemAmountKrw != null && x.orderAmountKrw != null) ?? rows.find(x => x.photo);
      if (!sample) throw Error('The collected catalog did not contain a linked reference image.');
      await select(sample);
      const image = page.locator(`[data-recorded-menu-id="${sample.id}"] img`);
      await image.scrollIntoViewIfNeeded();
      await image.waitFor({ state: 'visible' });
      await page.waitForFunction(id => {
        const image = document.querySelector(`[data-recorded-menu-id="${id}"] img`);
        return image?.complete && image.naturalWidth > 0;
      }, sample.id);
      if ((await image.getAttribute('src')) !== '/_local/orderer-menu-images/' + sample.photo.assetId) throw Error('Photo did not use the allowlist route.');
      await page.screenshot({ path: path.join(output, `catalog-photo-${width}.png`), fullPage: true });

      const missing = rows.find(x => !x.photo);
      if (!missing) throw Error('The real catalog lacks a missing-photo case.');
      await select(missing);
      await page.locator(`[data-recorded-menu-id="${missing.id}"] [data-testid="recorded-menu-image-empty"]`).waitFor();
      await page.screenshot({ path: path.join(output, `catalog-no-photo-${width}.png`), fullPage: true });

      await page.getByTestId('recorded-menu-branch').selectOption('');
      await page.getByTestId('recorded-menu-search').fill('__menu_no_matching_record__');
      await assertRows([]);
      if (!(await page.locator('main').innerText()).includes('조건에 맞는 메뉴 기록이 없습니다')) throw Error('Empty search notice absent.');
      const option = rows.find(x => x.options?.some(Boolean));
      if (option) {
        await page.getByTestId('recorded-menu-branch').selectOption(option.branchId);
        const query = option.options.find(Boolean);
        await page.getByTestId('recorded-menu-search').fill(query);
        await assertRows(filtered(option.branchId, query));
      }

      const photoUrl = new URL('/_local/orderer-menu-images/' + sample.photo.assetId, base).href;
      const imageResponse = await context.request.get(photoUrl);
      const asset = dataset.assets.find(x => x.assetId === sample.photo.assetId);
      if (imageResponse.status() !== 200 || crypto.createHash('sha256').update(await imageResponse.body()).digest('hex') !== asset.sha256.toLowerCase()) throw Error('Served asset SHA differs.');
      const headers = imageResponse.headers();
      if (headers['cache-control'] !== 'no-store' || headers['x-content-type-options'] !== 'nosniff' || headers['cross-origin-resource-policy'] !== 'same-origin') throw Error('Private asset response headers absent.');
      for (const deniedPath of ['/_local/orderer-menu-images/not-registered', '/_local/orderer-menu-images/%2e%2e%2fProgram.cs', '/artifacts/local/orderer-menu-catalog-r55/catalog.json']) {
        const response = await context.request.get(new URL(deniedPath, base).href);
        if (response.status() !== 404) throw Error('Unexpected private-file route response.');
      }
      for (const headers of [{ 'Sec-Fetch-Site': 'cross-site' }, { Origin: 'https://outside.invalid' }]) {
        const response = await context.request.get(photoUrl, { headers });
        if (response.status() !== 404) throw Error('Cross-origin image request was allowed.');
      }

      // 실제 공통 이미지 컴포넌트의 onerror 복구를 확인하되 원본 파일은 수정하지 않습니다.
      await page.route(photoUrl, route => route.abort());
      await page.reload({ waitUntil: 'networkidle' });
      await page.getByTestId('recorded-menu-catalog').waitFor();
      await select(sample);
      await page.locator(`[data-recorded-menu-id="${sample.id}"]`).scrollIntoViewIfNeeded();
      await page.locator(`[data-recorded-menu-id="${sample.id}"] [data-testid="recorded-menu-image-error"]`).waitFor();
      await layout();
      await page.screenshot({ path: path.join(output, `catalog-photo-error-${width}.png`), fullPage: true });
      await page.unroute(photoUrl);

      await page.goto(new URL('/recorded-menu-selling-preview', base).href, { waitUntil: 'networkidle' });
      await page.locator('[data-selling-preview-ready="true"]').waitFor();
      if (!(await page.locator('main').innerText()).includes('합성 값')) throw Error('Synthetic sales-price provenance absent.');
      const soldOut = page.locator('.restaurant-menu-item').filter({ hasText: '품절 검토 메뉴' });
      if (!(await soldOut.innerText()).includes('품절') || await soldOut.getByRole('button').count()) throw Error('Sold-out menu exposed quantity actions.');
      await soldOut.getByTestId('recorded-menu-image-empty').waitFor();
      await page.getByRole('button', { name: '수량 검토 메뉴 수량 늘리기', exact: true }).click();
      await page.locator('[data-selling-quantity="1"][data-selling-amount="4000"]').waitFor();
      await page.getByRole('button', { name: '수량 검토 메뉴 수량 늘리기', exact: true }).click();
      await page.locator('[data-selling-quantity="2"][data-selling-amount="8000"]').waitFor();
      await page.getByRole('button', { name: '수량 검토 메뉴 수량 줄이기', exact: true }).click();
      await page.locator('[data-selling-quantity="1"][data-selling-amount="4000"]').waitFor();
      if (await page.getByRole('button', { name: '주문 제출', exact: true }).count()) throw Error('Synthetic check exposed authenticated checkout.');
      await layout();
      await page.screenshot({ path: path.join(output, `selling-synthetic-${width}.png`), fullPage: true });
      passes.push({ name: `catalog-${width}`, observedMenus: rows.length, allObservationAmountsVerified: rows.length, knownUnitPrices: 0, searchBranchOptions: true,
        photoAvailableMissingError: true, readOnly: true, explicitItemOrderAmountLabels: true, privateImageSha256: true,
        unregisteredTraversalCrossOriginDenied: true, syntheticPriceQuantity: true, syntheticSoldOutWithoutPhoto: true,
        externalRequests: blocked.length, businessApiRequests: 0 });
    } catch (error) {
      failures.push({ width, error: error.message });
      await page.screenshot({ path: path.join(output, `failure-${mode}-${width}.png`), fullPage: true }).catch(() => {});
    } finally { await context.close(); }
  }
} finally { await browser.close(); }
const after = hashes();
const drift = before.filter((entry, index) => entry.sha256 !== after[index].sha256);
if (drift.length) failures.push({ error: 'Sources or binaries changed during verification.', drift });
const result = { schemaVersion: 1, checkedAtUtc: new Date().toISOString(), mode, baseOrigin: base.origin,
  binaryDirectory: path.relative(repo, binaryDirectory),
  scope: 'Local read-only shared UI and separate synthetic selling-menu presentation. No operating menu registration/checkout/APK/device/Azure/public image approval.',
  catalogSha256: mode === 'loaded' ? hash(datasetPath) : null, sourceAndBinarySha256: before, passes, failures, drift,
  passed: failures.length === 0 && passes.length === 3 };
fs.writeFileSync(path.join(output, 'verification.json'), JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify({ passed: result.passed, passes: passes.length, failures, output }));
if (!result.passed) process.exitCode = 1;
