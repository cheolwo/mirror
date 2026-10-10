import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
const { chromium } = createRequire(import.meta.url)('playwright');
const base = new URL(process.env.SSALDDEL_HISTORY_PREVIEW_URL ?? 'http://127.0.0.1:5581');
if (base.protocol !== 'http:' || base.hostname !== '127.0.0.1' || base.pathname !== '/' || base.username || base.password)
  throw Error('Loopback preview only.');
const repo = path.resolve(import.meta.dirname, '../..');
const localRoot = path.join(repo, 'artifacts', 'local') + path.sep;
const output = path.resolve(process.env.SSALDDEL_HISTORY_PREVIEW_OUTPUT ?? path.join(localRoot, 'delivery-history-replay-r54', 'ui'));
if (!output.startsWith(localRoot)) throw Error('Evidence must remain in artifacts/local.');
const mode = process.env.SSALDDEL_HISTORY_EXPECT ?? 'loaded';
if (!['loaded', 'missing', 'invalid'].includes(mode)) throw Error('Unknown expected input state.');
const datasetPath = process.env.SSALDDEL_HISTORICAL_DELIVERY_DATASET;
let dataset;
if (mode === 'loaded') {
  if (!datasetPath || !path.isAbsolute(datasetPath) || !path.resolve(datasetPath).startsWith(localRoot))
    throw Error('An explicit local dataset is required.');
  dataset = JSON.parse(fs.readFileSync(datasetPath, 'utf8'));
  if (!dataset.localVerificationOnly || dataset.driverId !== 'replay-driver') throw Error('Local verification input only.');
}
const sources = ['eng/RoleWorkspacePreview/HistoricalDeliveryDataset.cs', 'eng/RoleWorkspacePreview/HistoricalDeliveryPreviewFixture.cs',
  'eng/RoleWorkspacePreview/Pages/HistoricalDeliveryPreview.razor', 'eng/RoleWorkspacePreview/Pages/HistoricalDeliveryPreview.razor.css',
  'eng/RoleWorkspacePreview/Program.cs', 'eng/RoleWorkspacePreview/RoleWorkspacePreview.csproj', 'eng/RoleWorkspacePreview/verify-historical-delivery.mjs',
  'FDriverApp/PageModels/FDriverCompletedDeliveryListPageModel.cs', 'FDriverApp/PageModels/FDriverDailySettlementItem.cs',
  'FDriverApp/Services/IFDriverCompletedDeliveryNavigator.cs', 'FDriverApp/Services/FDriverAuthSession.cs',
  'FDriverApp/Services/FDriverAuthApiService.cs', 'FDriverApp/Services/FoodDeliveryDriverApiService.cs',
  'eng/RoleWorkspacePreview/bin/Debug/net10.0/Ssalddel.Ui.Common.dll', 'eng/RoleWorkspacePreview/bin/Debug/net10.0/Ssalddel.Contracts.dll',
  'eng/RoleWorkspacePreview/bin/Debug/net10.0/RoleWorkspacePreview.dll'];
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const hashes = () => sources.map(file => ({ path: file, sha256: hash(path.join(repo, file)) }));
const before = hashes(), passes = [], failures = [];
const money = value => new Intl.NumberFormat('ko-KR', { maximumFractionDigits: 2 }).format(value) + '원';
const distance = value => new Intl.NumberFormat('en-US', { useGrouping: false, maximumFractionDigits: 8 }).format(value) + 'km';
const recordsForDate = day => dataset.records.filter(row => row.serviceDate === day).sort((a, b) => b.sequence - a.sequence || a.recordId.localeCompare(b.recordId));
const recordsForBranch = id => dataset.records.filter(row => row.branchId === id).sort((a, b) => b.serviceDate.localeCompare(a.serviceDate) || b.sequence - a.sequence || a.recordId.localeCompare(b.recordId));
fs.mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : {}) });
try {
  for (const width of [320, 390]) {
    const context = await browser.newContext({ viewport: { width, height: 844 }, locale: 'ko-KR' });
    const blocked = [], errors = [];
    await context.route('**/*', route => {
      const request = new URL(route.request().url());
      if (request.origin !== base.origin || request.pathname.startsWith('/api/')) {
        blocked.push({ external: request.origin !== base.origin, businessApi: request.pathname.startsWith('/api/') });
        return route.abort();
      }
      return route.continue();
    });
    const page = await context.newPage();
    page.on('pageerror', error => errors.push(error.message));
    const checkLayout = async () => {
      if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)) throw Error('Horizontal overflow.');
      if (errors.length) throw Error(errors.join('; '));
      if (blocked.length) throw Error('The preview attempted external HTTP or a business API.');
    };
    const assertRows = async rows => {
      const ids = await page.locator('[data-history-record]').evaluateAll(elements => elements.map(element => element.dataset.historyRecord));
      if (JSON.stringify(ids) !== JSON.stringify(rows.map(row => row.recordId))) throw Error('Visible row identities differ.');
      for (const row of rows) {
        const entry = page.locator(`[data-history-record="${row.recordId}"]`);
        if (await entry.getAttribute('data-history-branch') !== row.branchId) throw Error('Branch identity differs.');
        if ((await entry.locator('.history__row-money').innerText()).trim() !== money(row.deliveryFeeKrw)) throw Error('Fee differs.');
        if ((await entry.locator('.history__row-distance').innerText()).trim() !== '앱 표시 거리 ' + distance(row.distanceKm)) throw Error('App distance differs.');
      }
      await checkLayout();
    };
    try {
      await page.goto(new URL('/historical-delivery-preview', base).href, { waitUntil: 'networkidle' });
      await page.locator('[data-history-ready]').waitFor();
      if (mode !== 'loaded') {
        if (await page.locator('[data-history-ready]').getAttribute('data-history-loaded') !== 'false') throw Error('Unexpected fallback input.');
        const body = await page.locator('main').innerText();
        if (!body.includes(mode === 'missing' ? '지정하지 않았습니다' : '읽지 못했습니다')) throw Error('Missing or invalid input notice absent.');
        if (await page.locator('[data-history-record]').count()) throw Error('Input failure displayed records.');
        await checkLayout();
        await page.screenshot({ path: path.join(output, `${mode}-${width}.png`), fullPage: true });
        passes.push({ name: `${mode}-${width}`, noFallback: true });
        continue;
      }
      if (await page.locator('[data-history-ready]').getAttribute('data-history-loaded') !== 'true') throw Error('Dataset not loaded.');
      const dates = [...new Set(dataset.records.map(row => row.serviceDate))].sort();
      const options = await page.locator('[data-history-date] option').evaluateAll(elements => elements.map(element => element.value));
      if (JSON.stringify(dates) !== JSON.stringify(options)) throw Error('Available dates differ.');
      let verifiedRows = 0, verifiedPages = 0;
      for (const day of dates) {
        const rows = recordsForDate(day);
        await page.locator('[data-history-date]').selectOption(day);
        await page.locator(`[data-history-record="${rows[0].recordId}"]`).waitFor();
        if ((await page.locator('[data-history-count]').innerText()).trim() !== `완료 ${rows.length}건`) throw Error('Daily count differs.');
        if ((await page.locator('[data-history-fee]').innerText()).trim() !== '배달료 합계 ' + money(rows.reduce((sum, row) => sum + row.deliveryFeeKrw, 0))) throw Error('Daily fee differs.');
        if ((await page.locator('[data-history-distance]').innerText()).trim() !== '앱 표시 거리 합계 ' + distance(rows.reduce((sum, row) => sum + row.distanceKm, 0))) throw Error('Daily app distance differs.');
        const pages = Math.max(1, Math.ceil(rows.length / 20));
        for (let index = 0; index < pages; index++) {
          const visible = rows.slice(index * 20, (index + 1) * 20);
          await assertRows(visible); verifiedRows += visible.length; verifiedPages++;
          if (index + 1 < pages) {
            await page.locator('[data-history-next]').click();
            await page.locator(`[data-history-record="${rows[(index + 1) * 20].recordId}"]`).waitFor();
          }
        }
        if (!(await page.locator('[data-history-next]').isDisabled())) throw Error('Last-page next button remains enabled.');
      }
      const day = dates.find(value => recordsForDate(value).length > 20) ?? dates[0];
      const dailyRows = recordsForDate(day), selected = dailyRows.at(-1);
      await page.locator('[data-history-date]').selectOption(day);
      await page.locator(`[data-history-record="${dailyRows[0].recordId}"]`).waitFor();
      const selectedPage = Math.floor(dailyRows.findIndex(row => row.recordId === selected.recordId) / 20);
      for (let pageIndex = 0; pageIndex < selectedPage; pageIndex++) {
        await page.locator('[data-history-next]').click();
        await page.locator(`[data-history-record="${dailyRows[(pageIndex + 1) * 20].recordId}"]`).waitFor();
      }
      await page.locator(`[data-history-open="${selected.recordId}"]`).click();
      await page.locator(`[data-history-detail="${selected.recordId}"]`).waitFor();
      if ((await page.locator('[data-history-detail-fee]').innerText()).trim() !== money(selected.deliveryFeeKrw)) throw Error('Selected fee differs.');
      if ((await page.locator('[data-history-detail-distance]').innerText()).trim() !== distance(selected.distanceKm)) throw Error('Selected distance differs.');
      if (selected.actualCompletedAtUtc == null && (await page.locator('[data-history-actual-time]').innerText()).trim() !== '미확인') throw Error('Invented actual completion time.');
      if (selected.foodOrderAmountKrw == null && (await page.locator('[data-history-order-amount]').innerText()).trim() !== '미확인') throw Error('Invented food amount.');
      await checkLayout();
      await page.screenshot({ path: path.join(output, `driver-detail-${width}.png`), fullPage: true });
      await page.locator('[data-history-to-restaurant]').click();
      await page.locator(`[data-history-record="${selected.recordId}"]`).waitFor();
      if (await page.locator('[data-history-branch-select]').inputValue() !== selected.branchId) throw Error('Cross-role branch differs.');
      const branchRows = recordsForBranch(selected.branchId);
      const branchIndex = branchRows.findIndex(row => row.recordId === selected.recordId);
      await assertRows(branchRows.slice(Math.floor(branchIndex / 20) * 20, (Math.floor(branchIndex / 20) + 1) * 20));
      if (!(await page.locator('[data-history-branch-count]').innerText()).includes(`${branchRows.length}건`)) throw Error('Branch count differs.');
      await page.screenshot({ path: path.join(output, `restaurant-detail-${width}.png`), fullPage: true });
      await page.locator('[data-history-to-driver]').click();
      await page.locator(`[data-history-record="${selected.recordId}"]`).waitFor();
      if (await page.locator('[data-history-date]').inputValue() !== selected.serviceDate) throw Error('Return date differs.');
      const pageNo = Math.floor(dailyRows.findIndex(row => row.recordId === selected.recordId) / 20) + 1;
      if (!(await page.locator('[data-history-page]').innerText()).startsWith(`${pageNo} /`)) throw Error('Return page differs.');
      await checkLayout();
      if (await page.locator('[data-history-driver]').getAttribute('aria-pressed') !== 'true') throw Error('Selected role is not identified.');
      const menuRecord = dataset.records.filter(row => row.menuItems.length && row.foodOrderAmountKrw != null)
        .sort((a, b) => b.menuItems.reduce((sum, item) => sum + item.options.length, 0) - a.menuItems.reduce((sum, item) => sum + item.options.length, 0))[0];
      if (menuRecord) {
        const menuDayRows = recordsForDate(menuRecord.serviceDate);
        await page.locator('[data-history-date]').selectOption(menuRecord.serviceDate);
        await page.locator(`[data-history-record="${menuDayRows[0].recordId}"]`).waitFor();
        const menuPage = Math.floor(menuDayRows.findIndex(row => row.recordId === menuRecord.recordId) / 20);
        for (let index = 0; index < menuPage; index++) {
          await page.locator('[data-history-next]').click();
          await page.locator(`[data-history-record="${menuDayRows[(index + 1) * 20].recordId}"]`).waitFor();
        }
        await page.locator(`[data-history-open="${menuRecord.recordId}"]`).click();
        const detail = page.locator(`[data-history-detail="${menuRecord.recordId}"]`);
        await detail.waitFor();
        const items = detail.locator('.history__menu li');
        if (await items.count() !== menuRecord.menuItems.length) throw Error('Menu items differ.');
        for (let index = 0; index < menuRecord.menuItems.length; index++) {
          const menu = menuRecord.menuItems[index], item = items.nth(index);
          if ((await item.locator('strong').innerText()).trim() !== menu.name) throw Error('Menu name differs.');
          const text = await item.innerText();
          if (!text.includes('단가 ' + (menu.unitPriceKrw == null ? '미확인' : money(menu.unitPriceKrw)))) throw Error('Menu unit price inferred.');
          if (!text.includes('표시 항목금액 ' + (menu.displayedItemAmountKrw == null ? '미확인' : money(menu.displayedItemAmountKrw)))) throw Error('Displayed item amount differs.');
          for (const option of menu.options) if (!text.includes(option)) throw Error('Menu option differs.');
        }
        if ((await page.locator('[data-history-order-amount]').innerText()).trim() !== money(menuRecord.foodOrderAmountKrw)) throw Error('Known order amount differs.');
        await checkLayout();
        await detail.screenshot({ path: path.join(output, `menu-detail-${width}.png`) });
      }
      passes.push({ name: `historical-rows-${width}`, verifiedRows, verifiedPages, sameRecordCrossRole: true, unknownValuesPreserved: true, confirmedMenuDetailsVerified: !!menuRecord });
    } catch (error) { failures.push({ name: `historical-${mode}-${width}`, error: error.message }); }
    finally { await context.close(); }
  }
} finally { await browser.close(); }
const after = hashes(), sourceAndBinaryDrift = JSON.stringify(before) !== JSON.stringify(after);
if (sourceAndBinaryDrift) failures.push({ name: 'source-and-binary', error: 'Source or built binaries changed during preview.' });
fs.writeFileSync(path.join(output, 'verification.json'), JSON.stringify({ verifiedAtUtc: new Date().toISOString(), localPreviewOnly: true, expectedInputState: mode,
  datasetSha256: datasetPath && mode === 'loaded' ? hash(datasetPath) : null, operatingRecordsChanged: false, externalNotificationsSent: false,
  sourceAndBinaryDrift, before, after, passes, failures }, null, 2));
console.log(JSON.stringify({ passed: passes.length, failed: failures.length, output }));
if (failures.length) process.exitCode = 1;
