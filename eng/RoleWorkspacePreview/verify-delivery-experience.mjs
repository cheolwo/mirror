import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import crypto from 'node:crypto';
const { chromium } = createRequire(import.meta.url)('playwright');
const base = new URL(process.env.SSALDDEL_EXPERIENCE_PREVIEW_URL ?? 'http://127.0.0.1:5579');
if (base.protocol !== 'http:' || base.hostname !== '127.0.0.1' || base.pathname !== '/' || base.username || base.password) throw Error('Loopback preview only.');
const repo = path.resolve(import.meta.dirname, '../..');
const output = path.resolve(process.env.SSALDDEL_EXPERIENCE_PREVIEW_OUTPUT ?? path.join(repo, 'artifacts/local/delivery-experience/ui'));
if (!output.startsWith(path.join(repo, 'artifacts/local') + path.sep)) throw Error('Evidence must remain in artifacts/local.');
fs.mkdirSync(output, { recursive: true });
const sources = ['Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/DeliveryExperiencePresentation.cs',
  'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverRoleWorkspaceAdapter.cs', 'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/RestaurantRoleWorkspaceAdapter.cs',
  'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/OrdererRoleWorkspaceAdapter.cs', 'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleMapWorkspace.razor',
  'Ssalddel.Ui.Common/Areas/App/Components/Food/DeliveryExperienceLines.razor', 'Ssalddel.Ui.Common/Areas/App/Components/Food/DeliveryExperienceLines.razor.css',
  'eng/RoleWorkspacePreview/DeliveryExperiencePreviewFixture.cs', 'eng/RoleWorkspacePreview/DeliveryExperiencePreviewPage.razor',
  'eng/RoleWorkspacePreview/bin/Debug/net10.0/Ssalddel.Ui.Common.dll', 'eng/RoleWorkspacePreview/bin/Debug/net10.0/Ssalddel.Contracts.dll',
  'eng/RoleWorkspacePreview/bin/Debug/net10.0/RoleWorkspacePreview.dll'];
const hashes = () => sources.map(name => ({ path: name, sha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(repo, name))).digest('hex') }));
const before = hashes();
const passes = [], failures = [];
const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : {}) });
try {
  for (const width of [320, 390]) for (const role of ['food-driver', 'restaurant', 'orderer']) for (const scenario of ['known', 'unknown', 'revoked', 'ended', 'simulation', 'partial', ...(role === 'food-driver' ? ['pickup'] : [])]) {
    const name = `${role}-${scenario}-${width}`;
    const context = await browser.newContext({ viewport: { width, height: 844 }, locale: 'ko-KR' });
    await context.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
    const page = await context.newPage(), errors = [];
    page.on('pageerror', error => errors.push(error.message));
    try {
      await page.goto(new URL(`/delivery-experience-preview/${role}/${scenario}`, base).href, { waitUntil: 'networkidle' });
      await page.locator('[data-experience-ready]').waitFor();
      await page.waitForFunction(() => !document.querySelector('.role-map-workspace__loading'));
      const lines = page.locator('[data-delivery-experience] p');
      const expected = ['unknown', 'revoked', 'ended'].includes(scenario) ? 0 : role === 'restaurant' || scenario === 'pickup' ? 1 : 2;
      if (await lines.count() !== expected) throw Error(`Experience count ${await lines.count()} != ${expected}`);
      const texts = await lines.allTextContents();
      if (role === 'restaurant' && texts.some(text => text.includes('오고 있습니다') || !text.includes('픽업한 기사님입니다'))) throw Error('Restaurant experience inferred arrival state.');
      if (texts.some(text => text.includes('번째') || text.includes('방문'))) throw Error('Completed orders presented as physical visits.');
      if (scenario === 'simulation' && texts.some(text => !text.includes('모의 기록'))) throw Error('Simulation basis missing.');
      if (scenario === 'partial' && texts.some(text => !text.includes('확인된 앱 기록'))) throw Error('Partial basis missing.');
      if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)) throw Error('Horizontal overflow.');
      if (errors.length) throw Error(errors.join('; '));
      await page.screenshot({ path: path.join(output, `${name}.png`), fullPage: true });
      if (scenario === 'known' || scenario === 'partial') {
        await page.locator('[data-experience-withdraw]').click();
        await page.waitForFunction(() => !document.querySelector('[data-delivery-experience]'));
      }
      passes.push({ name, expectedLines: expected, text: texts, withdrawalRequery: ['known', 'partial'].includes(scenario) });
    } catch (error) { failures.push({ name, error: error.message }); }
    finally { await context.close(); }
  }
} finally { await browser.close(); }
const after = hashes(), sourceAndBinaryDrift = JSON.stringify(before) !== JSON.stringify(after);
if (sourceAndBinaryDrift) failures.push({ name: 'source-and-binary', error: 'Source or built binaries changed during preview.' });
fs.writeFileSync(path.join(output, 'verification.json'), JSON.stringify({ verifiedAtUtc: new Date().toISOString(), localPreviewOnly: true, externalNotificationsSent: false, operatingRecordsChanged: false, sourceAndBinaryDrift, before, after, passes, failures }, null, 2));
console.log(JSON.stringify({ passed: passes.length, failed: failures.length, output }));
if (failures.length) process.exitCode = 1;
