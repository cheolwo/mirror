import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import crypto from 'node:crypto';
const { chromium } = createRequire(import.meta.url)('playwright');
const base = new URL(process.env.SSALDDEL_DRIVER_CONTROLS_PREVIEW_URL ?? 'http://127.0.0.1:5392');
if (base.protocol !== 'http:' || base.hostname !== '127.0.0.1' || base.pathname !== '/') throw Error('Loopback preview only.');
const repo = path.resolve(import.meta.dirname, '../..');
const output = path.resolve(process.env.SSALDDEL_DRIVER_CONTROLS_PREVIEW_OUTPUT ?? path.join(repo, 'artifacts/local/food-driver-controls/ui'));
if (!output.startsWith(path.join(repo, 'artifacts/local') + path.sep)) throw Error('Evidence must remain local.');
fs.mkdirSync(output, { recursive: true });
const sources = ['Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleMapWorkspace.razor', 'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleMapWorkspace.razor.css',
  'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/FoodDriverDispatchToggle.razor', 'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/FoodDriverDispatchToggle.razor.css',
  'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleWorkspaceViewModel.cs',
  'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverRoleWorkspaceAdapter.cs', 'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/RoleWorkspaceContracts.cs',
  'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverStagePresentation.cs', 'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodWorkspacePresentation.cs',
  'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverWorkRecoveryPolicy.cs', 'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverWorkspacePresentation.cs',
  'eng/RoleWorkspacePreview/FoodDriverControlsPreviewFixture.cs', 'eng/RoleWorkspacePreview/FoodDriverControlsPreviewPage.razor',
  'eng/RoleWorkspacePreview/bin/Debug/net10.0/RoleWorkspacePreview.dll', 'eng/RoleWorkspacePreview/bin/Debug/net10.0/Ssalddel.Ui.Common.dll'];
const hashes = () => sources.map(name => ({ path: name, sha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(repo, name))).digest('hex') }));
const before = hashes(), passes = [], failures = [];
const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : {}) });
try {
  for (const width of [320, 390]) for (const scenario of ['on', 'off', 'active-off', 'permission', 'toggle-error', 'toggle-response-lost', 'not-working', 'not-working-start-response-lost', 'not-working-intent-response-lost']) {
    const name = `${scenario}-${width}`;
    const context = await browser.newContext({ viewport: { width, height: 844 }, locale: 'ko-KR' });
    await context.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
    const page = await context.newPage(), errors = [];
    page.on('pageerror', error => errors.push(error.message));
    try {
      await page.goto(new URL(`/food-driver-controls-preview/${scenario}`, base).href, { waitUntil: 'networkidle' });
      await page.locator('[data-controls-ready]').waitFor({ state: 'attached' });
      const toggle = page.getByRole('switch', { name: '신규 배차 받기' });
      await toggle.waitFor();
      const receiving = ['on', 'permission', 'toggle-error', 'toggle-response-lost'].includes(scenario);
      if (await toggle.getAttribute('aria-checked') !== String(receiving)) throw Error('Toggle not based on server receiving state.');
      const canvas = page.locator('#role-workspace-map-canvas');
      const bounds = await canvas.boundingBox(), switchBounds = await toggle.boundingBox();
      if (!bounds || !switchBounds || switchBounds.x < bounds.x || switchBounds.y < bounds.y || switchBounds.x - bounds.x > 24 || switchBounds.y - bounds.y > 24) throw Error('Toggle not at map top left.');
      if (await page.getByRole('button', { name: '현재 위치 갱신', exact: true }).count()) throw Error('Normal manual location action remains.');
      if (await page.getByRole('button', { name: '새로고침', exact: true }).count()) throw Error('Normal manual refresh remains in food-driver workspace.');
      if ((await page.locator('main.role-map-workspace').innerText()).includes('새로고침')) throw Error('Food-driver workflow still asks the driver to refresh.');
      const profile = page.locator('.role-map-workspace__profile');
      await profile.locator('summary').click();
      const stop = profile.getByRole('button', { name: '운행 종료', exact: true });
      if (!scenario.startsWith('not-working')) {
        if (await page.getByRole('button', { name: '운행 종료', exact: true }).count() !== 1) throw Error('Stop work is duplicated.');
        if (scenario === 'active-off' && !await stop.isDisabled()) throw Error('Active delivery permits stopping work.');
      }
      await page.screenshot({ path: path.join(output, `${name}.png`), fullPage: true });
      if (scenario === 'on' || scenario === 'off') {
        await toggle.click();
        await page.waitForFunction(expected => document.querySelector('[data-controls-ready]')?.getAttribute('data-receiving') === expected, String(!receiving));
        await page.waitForFunction(expected => document.querySelector('[role="switch"]')?.getAttribute('aria-checked') === expected, String(!receiving));
        const afterBounds = await canvas.boundingBox();
        if (bounds.width !== afterBounds?.width || bounds.height !== afterBounds?.height) throw Error('Receiving toggle resized map.');
        if (scenario === 'on') {
          await stop.click();
          await profile.getByRole('button', { name: '확인', exact: true }).click();
          await page.waitForFunction(() => document.querySelector('[data-controls-ready]')?.getAttribute('data-stop-posts') === '1');
        }
      } else if (scenario === 'permission') {
        if (await page.locator('[data-controls-ready]').getAttribute('data-explicit-location') !== '0') throw Error('Automatic location requested permission.');
        await page.getByRole('button', { name: '위치 확인', exact: true }).click();
        await page.waitForFunction(() => document.querySelector('[data-controls-ready]')?.getAttribute('data-explicit-location') === '1');
        await page.waitForFunction(() => !Array.from(document.querySelectorAll('button')).some(button => button.textContent.trim() === '위치 확인'));
      } else if (scenario === 'toggle-error' || scenario === 'toggle-response-lost') {
        await toggle.click();
        await page.getByRole('alert').filter({ hasText: '신규 배차 수신 변경 결과' }).waitFor();
        if (await toggle.getAttribute('aria-checked') !== 'false' || !await toggle.isDisabled() || !await toggle.textContent().then(text => text.includes('확인 필요'))) throw Error('Unconfirmed outcome permits another command or shows old ON as confirmed.');
        const expectedWrites = scenario === 'toggle-response-lost' ? '1' : '0';
        if (await page.locator('[data-controls-ready]').getAttribute('data-intent-writes') !== expectedWrites) throw Error('Response-loss fixture does not represent actual server application.');
        await page.screenshot({ path: path.join(output, `${name}-failure.png`), fullPage: true });
        await page.waitForFunction(expected => {
          const control = document.querySelector('[role="switch"]');
          return control && !control.disabled && control.getAttribute('aria-checked') === expected;
        }, scenario === 'toggle-response-lost' ? 'false' : 'true');
        if (await page.getByRole('alert').filter({ hasText: '신규 배차 수신 변경 결과' }).count()) throw Error('Canonical read did not clear unconfirmed command notice.');
        if (await page.locator('[data-controls-ready]').getAttribute('data-intent-writes') !== expectedWrites) throw Error('Automatic canonical read issued another dispatch command.');
        await page.screenshot({ path: path.join(output, `${name}-recovered.png`), fullPage: true });
      } else if (scenario.startsWith('not-working')) {
        await page.getByRole('button', { name: '운행 시작', exact: true }).click();
        if (scenario !== 'not-working') {
          await page.getByRole('alert').filter({ hasText: '운행 시작 결과와 신규 배차 수신 상태' }).waitFor();
          if (!await toggle.isDisabled() || await toggle.getAttribute('aria-checked') !== 'false') throw Error('Unconfirmed start permits another receiving change.');
          await page.screenshot({ path: path.join(output, `${name}-failure.png`), fullPage: true });
        }
        await page.waitForFunction(() => {
          const control = document.querySelector('[role="switch"]');
          return control && !control.disabled && control.getAttribute('aria-checked') === 'true';
        });
        if (await page.locator('[data-controls-ready]').getAttribute('data-intent-writes') !== '1') throw Error('Start did not enable receiving once.');
        if (await page.getByRole('alert').filter({ hasText: '운행 시작 결과와 신규 배차 수신 상태' }).count()) throw Error('Automatic read did not resolve start outcome.');
        await page.screenshot({ path: path.join(output, `${name}-started.png`), fullPage: true });
      }
      if ((await page.locator('main.role-map-workspace').innerText()).includes('새로고침')) throw Error('Food-driver workflow asks for a manual refresh after interaction.');
      if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)) throw Error('Horizontal overflow.');
      if (errors.length) throw Error(errors.join('; '));
      passes.push({ name, clickVerified: scenario !== 'active-off', automaticRecoveryVerified: scenario.includes('response-lost') || scenario === 'toggle-error' });
    } catch (error) { failures.push({ name, error: error.message }); }
    finally { await context.close(); }
  }
} finally { await browser.close(); }
const after = hashes(), sourceAndBinaryDrift = JSON.stringify(before) !== JSON.stringify(after);
if (sourceAndBinaryDrift) failures.push({ name: 'source-and-binary', error: 'Preview source or binary drifted.' });
fs.writeFileSync(path.join(output, 'verification.json'), JSON.stringify({ verifiedAtUtc: new Date().toISOString(), localPreviewOnly: true, operationalApiCalled: false, sourceAndBinaryDrift, before, after, passes, failures }, null, 2));
console.log(JSON.stringify({ passed: passes.length, failed: failures.length, output }));
if (failures.length) process.exitCode = 1;
