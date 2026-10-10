// 렌더·확인 관문·자원 수명 검증입니다. 가상 카메라와 모의 디코더를 사용하며 실물 인식·Android 검증이 아닙니다.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const local = path.join(repo, 'artifacts/local');
const base = new URL(process.env.SSALDDEL_MART_SAMPLE_URL ?? 'http://127.0.0.1:5593/');
if (base.protocol !== 'http:' || base.hostname !== '127.0.0.1' || base.pathname !== '/' || base.username || base.password || base.search || base.hash)
  throw new Error('LOOPBACK_ORIGIN_REQUIRED');
const output = path.resolve(process.env.SSALDDEL_MART_SCANNER_OUTPUT ?? path.join(local, 'ssalddel-mart-camera-scan-r53/ui'));
const binary = path.resolve(process.env.SSALDDEL_MART_SAMPLE_BINARY_DIR ?? path.join(local, 'ssalddel-mart-camera-scan-r53/host'));
if (!output.startsWith(local + path.sep)) throw new Error('LOCAL_EVIDENCE_PATH_REQUIRED');
if (!binary.startsWith(local + path.sep)) throw new Error('LOCAL_BINARY_PATH_REQUIRED');
const hostLog = process.env.SSALDDEL_MART_SCANNER_HOST_LOG ? path.resolve(process.env.SSALDDEL_MART_SCANNER_HOST_LOG) : null;
if (hostLog && !hostLog.startsWith(local + path.sep)) throw new Error('LOCAL_HOST_LOG_REQUIRED');
const hostLogOffset = hostLog ? fs.statSync(hostLog).size : null;
const sources = [
  'eng/RoleWorkspacePreview/Program.cs', 'eng/RoleWorkspacePreview/RoleWorkspacePreview.csproj',
  'eng/RoleWorkspacePreview/SsalddelMartSampleInput.cs', 'eng/RoleWorkspacePreview/SsalddelMartSampleRuntime.cs',
  'eng/RoleWorkspacePreview/SsalddelMartSamplePage.razor', 'eng/RoleWorkspacePreview/SsalddelMartSamplePage.razor.css',
  'eng/RoleWorkspacePreview/SsalddelMartRackScanner.razor', 'eng/RoleWorkspacePreview/SsalddelMartRackScanner.razor.css',
  'eng/RoleWorkspacePreview/wwwroot/js/ssalddel-mart-scanner.js',
  'eng/RoleWorkspacePreview/verify-ssalddel-mart-scanner.mjs',
  path.relative(repo, path.join(binary, 'RoleWorkspacePreview.dll')),
  path.relative(repo, path.join(binary, 'Ssalddel.dll'))
];
const fingerprint = () => sources.map(value => ({ path: value,
  sha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(repo, value))).digest('hex') }));
const before = fingerprint(), passes = [], failures = [];
fs.mkdirSync(output, { recursive: true });
const ensure = (condition, code) => { if (!condition) throw new Error(code); };

// BarcodeDetector는 모의 응답입니다. canvas.captureStream은 video.play와 실제 MediaStreamTrack.stop 수명을 검증합니다.
function installCameraFixture() {
  const fixture = window.__scanTest = {
    mode: 'supported', queue: [], cameraRequests: 0, detectorCalls: 0,
    streamsCreated: 0, tracksStopped: 0, constraintsValid: true,
    holdPermission: false, holdDetection: false, pendingPermissions: [], pendingDetections: [], formats: []
  };
  function createStream() {
    const canvas = document.createElement('canvas');
    canvas.width = 640; canvas.height = 360;
    const paint = canvas.getContext('2d');
    paint.fillStyle = '#263744'; paint.fillRect(0, 0, 640, 360);
    paint.fillStyle = '#ffffff'; paint.font = '24px sans-serif';
    paint.fillText('SIMULATED CAMERA / MOCK DECODER', 40, 180);
    const stream = canvas.captureStream(5);
    fixture.streamsCreated++;
    for (const track of stream.getTracks()) {
      const stop = track.stop.bind(track);
      let stopped = false;
      track.stop = () => { if (!stopped) fixture.tracksStopped++; stopped = true; stop(); };
    }
    return stream;
  }
  const getUserMedia = async constraints => {
    fixture.cameraRequests++;
    fixture.constraintsValid &&= constraints?.audio === false && constraints?.video?.facingMode?.ideal === 'environment';
    if (fixture.mode === 'denied') throw new DOMException('Denied by fixture', 'NotAllowedError');
    if (fixture.holdPermission) return new Promise(resolve => fixture.pendingPermissions.push(() => resolve(createStream())));
    return createStream();
  };
  Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia } });
  class MockBarcodeDetector {
    static async getSupportedFormats() { return fixture.mode === 'unsupported' ? [] : ['qr_code', 'code_128']; }
    constructor(options) { fixture.formats = options?.formats ?? []; }
    async detect() {
      fixture.detectorCalls++;
      if (fixture.holdDetection) return new Promise(resolve => fixture.pendingDetections.push(resolve));
      return fixture.queue.length ? [{ format: 'qr_code', rawValue: fixture.queue.shift() }] : [];
    }
  }
  Object.defineProperty(window, 'BarcodeDetector', { configurable: true, value: MockBarcodeDetector });
  fixture.emit = code => { fixture.queue.push(code); };
  fixture.releasePermissions = () => { fixture.holdPermission = false; fixture.pendingPermissions.splice(0).forEach(resolve => resolve()); };
  fixture.releaseDetections = code => { fixture.holdDetection = false; fixture.pendingDetections.splice(0).forEach(resolve => resolve([{ format: 'qr_code', rawValue: code }])); };
  fixture.snapshot = () => ({ cameraRequests: fixture.cameraRequests, detectorCalls: fixture.detectorCalls,
    streamsCreated: fixture.streamsCreated, tracksStopped: fixture.tracksStopped, constraintsValid: fixture.constraintsValid,
    formats: fixture.formats, pendingPermissionCount: fixture.pendingPermissions.length, pendingDetectionCount: fixture.pendingDetections.length });
}

const scenarios = ['unsupported', 'permission-denied', 'matched', 'wrong-code', 'cancel', 'late-permission', 'order-switch', 'reset', 'hidden'];
const selectedScenarios = process.env.SSALDDEL_MART_SCANNER_SCENARIOS?.split(',') ?? scenarios;
const selectedWidths = process.env.SSALDDEL_MART_SCANNER_WIDTHS?.split(',').map(Number) ?? [320, 390, 1280];
if (!selectedScenarios.length || selectedScenarios.some(value => !scenarios.includes(value)) || new Set(selectedScenarios).size !== selectedScenarios.length)
  throw new Error('SCENARIO_SCOPE_INVALID');
if (!selectedWidths.length || selectedWidths.some(value => ![320, 390, 1280].includes(value)) || new Set(selectedWidths).size !== selectedWidths.length)
  throw new Error('WIDTH_SCOPE_INVALID');
const browser = await chromium.launch({ headless: true,
  ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : {}) });
try {
  for (const width of selectedWidths) {
    for (const scenario of selectedScenarios) {
      const context = await browser.newContext({ viewport: { width, height: 900 }, locale: 'ko-KR', timezoneId: 'Asia/Seoul', reducedMotion: 'reduce' });
      const errors = [], blocked = [], checks = [], screenshots = [];
      await context.route('**/*', route => {
        const url = new URL(route.request().url());
        const external = url.origin !== base.origin && !['data:', 'blob:'].includes(url.protocol);
        const businessApi = url.origin === base.origin && url.pathname.startsWith('/api/');
        if (external || businessApi) { blocked.push({ external, businessApi }); return route.abort(); }
        return route.continue();
      });
      await context.addInitScript(installCameraFixture);
      const page = await context.newPage();
      let storageBefore = null;
      page.on('pageerror', () => errors.push('PAGE_ERROR'));
      page.on('console', message => { if (message.type() === 'error') errors.push('CONSOLE_ERROR'); });
      page.on('websocket', socket => { if (new URL(socket.url()).host !== base.host) blocked.push({ externalWebSocket: true }); });
      const primary = () => page.locator('[data-action="primary"]');
      const start = () => page.locator('[data-scan-start]');
      const stop = () => page.locator('[data-scan-stop]');
      const rackInput = () => page.locator('[data-rack-input]');
      const snapshot = () => page.evaluate(() => window.__scanTest.snapshot());
      const scanState = () => page.locator('[data-scan-state]').evaluateAll(nodes => nodes[0]?.getAttribute('data-scan-state') ?? null);
      const stateIs = state => page.locator(`[data-scan-state="${state}"]`).waitFor();
      const allTracksStopped = async () => {
        await page.waitForFunction(() => window.__scanTest.streamsCreated === window.__scanTest.tracksStopped);
      };
      const beginScan = async () => { await start().click(); await stateIs('scanning'); };
      const inspect = async () => {
        await page.evaluate(() => document.fonts.ready);
        ensure(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'HORIZONTAL_OVERFLOW');
        const heights = await page.locator('.mart-sample button:visible,.mart-sample summary:visible,.mart-sample__check:visible,.mart-sample__field input:visible')
          .evaluateAll(nodes => nodes.map(node => node.getBoundingClientRect().height));
        ensure(heights.every(height => height >= 47.5), 'TOUCH_TARGET_TOO_SHORT');
        ensure(await primary().count() <= 1, 'MULTIPLE_PRIMARY_ACTIONS');
        ensure(errors.length === 0, 'BROWSER_ERROR'); ensure(blocked.length === 0, 'EXTERNAL_OR_BUSINESS_REQUEST');
        ensure(await page.locator('main').evaluate(node => !/replay-(?:record|branch|pickup)-[a-f0-9]{64}|B\s*마트|비마트|₩|[\d,]+\s*원/.test(node.innerText)), 'PRIVATE_PROVENANCE_OR_AMOUNT_DISPLAYED');
        ensure(await page.evaluate(() => JSON.stringify([Object.entries(localStorage), Object.entries(sessionStorage)])) === storageBefore, 'BROWSER_STORAGE_CHANGED');
      };
      const capture = async name => {
        await inspect(); const file = `${scenario}-${name}-${width}.png`;
        await page.screenshot({ path: path.join(output, file), fullPage: true }); screenshots.push(file);
      };
      try {
        await page.goto(new URL('/workspace/warehouse/ssalddel-mart-sample', base).href);
        await page.locator('[data-mart-sample-ready="true"][data-mart-sample-loaded="true"]').waitFor({ timeout: 60000 });
        await page.locator('[data-action-kind="start"]').waitFor();
        storageBefore = await page.evaluate(() => JSON.stringify([Object.entries(localStorage), Object.entries(sessionStorage)]));
        ensure((await snapshot()).cameraRequests === 0, 'CAMERA_REQUESTED_BEFORE_USER_ACTION');
        ensure(await start().count() === 0, 'SCANNER_VISIBLE_BEFORE_PICKING');
        await primary().click(); await page.locator('[data-action-kind="pick"]').waitFor();
        const currentLine = await page.locator('[data-current-line]').getAttribute('data-current-line');
        const rack = (await page.locator('[data-current-rack]').innerText()).trim();
        const pickedBefore = await page.locator('[data-line-picked="true"]').count();
        ensure(/^SAMPLE-RACK-[0-9]{3}$/.test(rack), 'SYNTHETIC_RACK_REQUIRED');
        ensure(await primary().isDisabled(), 'PICKING_WITHOUT_CONFIRMATION');
        ensure((await snapshot()).cameraRequests === 0, 'CAMERA_REQUESTED_BEFORE_SCAN_BUTTON');

        if (scenario === 'unsupported' || scenario === 'permission-denied') {
          await page.evaluate(mode => { window.__scanTest.mode = mode; }, scenario === 'unsupported' ? 'unsupported' : 'denied');
          await start().click(); await stateIs(scenario);
          ensure((await snapshot()).cameraRequests === (scenario === 'unsupported' ? 0 : 1), 'INCORRECT_PERMISSION_REQUEST_COUNT');
          await rackInput().fill(rack);
          ensure(await primary().isDisabled(), 'FALLBACK_BYPASSED_CONFIRMATION');
          checks.push('unsupported or denied camera preserves direct input and confirmation gates');
        } else if (scenario === 'matched') {
          await beginScan();
          await page.evaluate(value => { window.__scanTest.emit(value); window.__scanTest.emit(value); }, rack);
          await page.waitForFunction(value => document.querySelector('[data-rack-input]')?.value === value, rack);
          await stateIs('matched'); await allTracksStopped();
          ensure(await primary().isDisabled(), 'SCAN_COMPLETED_PICKING_AUTOMATICALLY');
          ensure(!await page.locator('[data-product-confirm]').isChecked() && !await page.locator('[data-quantity-confirm]').isChecked(), 'SCAN_CHECKED_PRODUCT_OR_QUANTITY');
          ensure(await page.locator('[data-line-picked="true"]').count() === pickedBefore, 'SCAN_MUTATED_PICKED_QUANTITY');
          await page.locator('[data-scan-labels] > summary').click();
          for (const selector of ['[data-scan-qr]', '[data-scan-barcode]']) {
            const label = page.locator(selector);
            ensure(await label.count() === 1, 'SYNTHETIC_LABEL_MISSING');
            const source = await label.getAttribute('src');
            ensure(source?.startsWith('data:image/svg+xml'), 'LOCAL_SVG_LABEL_REQUIRED');
            ensure(await label.evaluate(node => node.complete && node.naturalWidth > 0 && node.naturalHeight > 0), 'SYNTHETIC_LABEL_NOT_RENDERED');
          }
          checks.push('local QR and Code128 synthetic labels render; decoding is tested separately, not by this mock');
          await capture('matched-labels');
          await page.locator('[data-product-confirm]').check();
          ensure(await primary().isDisabled(), 'QUANTITY_CONFIRMATION_NOT_REQUIRED');
          await page.locator('[data-quantity-confirm]').check();
          await page.waitForFunction(() => !document.querySelector('[data-action="primary"]')?.disabled);
          await primary().click();
          await page.waitForFunction(previous => document.querySelector('[data-current-line]')?.dataset.currentLine !== previous, currentLine);
          ensure(await page.locator('[data-line-picked="true"]').count() === pickedBefore + 1, 'EXPLICIT_PICK_DID_NOT_ADVANCE_ONCE');
          await allTracksStopped();
          checks.push('matched code fills only input; duplicate recognition cannot complete picking; explicit product/quantity/complete advances once');
        } else if (scenario === 'wrong-code') {
          for (const value of ['https://example.invalid/do-not-open', 'SAMPLE-RACK-999', 'X'.repeat(121)]) {
            await beginScan(); await page.evaluate(code => window.__scanTest.emit(code), value);
            await stateIs('wrong-code'); await allTracksStopped();
            ensure(await rackInput().inputValue() === '', 'WRONG_CODE_POPULATED_INPUT');
            ensure(!await page.locator('main').evaluate((node, code) => node.innerText.includes(code), value), 'WRONG_RAW_CODE_DISPLAYED');
          }
          ensure(await primary().isDisabled(), 'WRONG_CODE_BYPASSED_CONFIRMATION');
          checks.push('URL, another rack and oversized values stop scanning without input, navigation or raw display');
        } else if (scenario === 'cancel') {
          await beginScan(); await stop().click(); await stateIs('cancelled'); await allTracksStopped();
          ensure(await rackInput().inputValue() === '', 'CANCEL_POPULATED_INPUT');
          await rackInput().fill(rack); ensure(await primary().isDisabled(), 'CANCEL_FALLBACK_BYPASSED_CONFIRMATION');
          checks.push('explicit close stops camera and preserves manual confirmation flow');
        } else if (scenario === 'late-permission') {
          await page.evaluate(() => { window.__scanTest.holdPermission = true; });
          await start().click();
          await page.waitForFunction(() => window.__scanTest.pendingPermissions.length === 1);
          await stop().click(); await stateIs('cancelled');
          await page.evaluate(() => window.__scanTest.releasePermissions());
          await page.waitForFunction(() => window.__scanTest.streamsCreated === 1 && window.__scanTest.tracksStopped === 1);
          ensure(await rackInput().inputValue() === '', 'LATE_PERMISSION_POPULATED_INPUT');
          checks.push('permission returning after cancellation is immediately released');
        } else if (scenario === 'order-switch') {
          await page.evaluate(() => { window.__scanTest.holdDetection = true; });
          await beginScan(); await page.waitForFunction(() => window.__scanTest.pendingDetections.length > 0);
          const nextOrder = await page.locator('[data-order-id]').nth(1).getAttribute('data-order-id');
          await page.locator('[data-order-id]').nth(1).click();
          await page.waitForFunction(value => document.querySelector('[data-selected-order]')?.dataset.selectedOrder === value, nextOrder);
          await allTracksStopped(); await page.locator('[data-action-kind="start"]').click();
          await page.locator('[data-action-kind="pick"]').waitFor();
          await page.evaluate(value => window.__scanTest.releaseDetections(value), rack);
          // Flush browser/SignalR callbacks before asserting the new line remains untouched.
          await page.waitForTimeout(300);
          ensure(await rackInput().inputValue() === '', 'STALE_DETECTION_CONTAMINATED_NEW_ORDER');
          ensure(await primary().isDisabled(), 'ORDER_SWITCH_BYPASSED_CONFIRMATION');
          ensure(await page.locator('[data-line-picked="true"]').count() === 0, 'STALE_DETECTION_CHANGED_OTHER_ORDER');
          checks.push('order switch releases camera; delayed old-rack detection cannot populate new order');
        } else if (scenario === 'reset') {
          await beginScan(); await page.locator('[data-reset-open]').click(); await page.locator('[data-reset-confirm]').click();
          await page.locator('[data-action-kind="start"]').waitFor(); await allTracksStopped();
          ensure(await start().count() === 0, 'RESET_LEFT_SCANNER_ACTIVE');
          ensure(await page.locator('[data-line-picked="true"]').count() === 0, 'RESET_LEFT_PICKED_LINE');
          checks.push('explicit sample reset disposes scanner and restores waiting line');
        } else if (scenario === 'hidden') {
          await beginScan();
          await page.evaluate(() => {
            Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' });
            document.dispatchEvent(new Event('visibilitychange'));
          });
          await stateIs('hidden'); await allTracksStopped();
          ensure(await rackInput().inputValue() === '', 'HIDDEN_CAMERA_POPULATED_INPUT');
          checks.push('simulated visibilitychange stops camera without completing work; physical background is unverified');
        }
        const stats = await snapshot();
        ensure(stats.constraintsValid, 'CAMERA_NOT_REAR_ONLY_OR_AUDIO_REQUESTED');
        ensure(stats.formats.every(format => ['qr_code', 'code_128'].includes(format)), 'UNAPPROVED_BARCODE_FORMAT');
        await allTracksStopped(); await capture('final');
        passes.push({ width, scenario, checks: [...checks, 'no overflow; 48px controls; no browser errors, external/business requests or browser storage'],
          camera: stats, finalScanState: await scanState().catch(() => null), screenshots });
        process.stdout.write(JSON.stringify({ width, scenario, passed: true }) + '\n');
      } catch (error) {
        const code = /^[A-Z0-9_]+$/.test(error.message) ? error.message : 'UI_VERIFICATION_FAILED';
        failures.push({ width, scenario, code, browserErrorCount: errors.length, blockedRequestCount: blocked.length,
          camera: await snapshot().catch(() => null), scanState: await scanState().catch(() => null) });
        process.stdout.write(JSON.stringify({ width, scenario, passed: false, code }) + '\n');
        await page.screenshot({ path: path.join(output, `${scenario}-failure-${width}.png`), fullPage: true }).catch(() => {});
      } finally { await context.close(); }
    }
  }
} finally { await browser.close(); }
const after = fingerprint(), sourceFilesUnchanged = JSON.stringify(before) === JSON.stringify(after);
if (!sourceFilesUnchanged) failures.push({ code: 'SOURCE_OR_BINARY_CHANGED_DURING_VERIFICATION' });
let serverFailureCount = null;
if (hostLog) {
  try {
    const size = fs.statSync(hostLog).size;
    if (size < hostLogOffset || size > 64 * 1024 * 1024) throw new Error('HOST_LOG_WINDOW_INVALID');
    const window = fs.readFileSync(hostLog).subarray(hostLogOffset).toString('utf8');
    serverFailureCount = (window.match(/^(?:.*\s)?(?:fail|crit):|Unhandled exception/gim) ?? []).length;
    if (serverFailureCount) failures.push({ code: 'SERVER_HOST_FAILURE', count: serverFailureCount });
  } catch {
    failures.push({ code: 'SERVER_HOST_LOG_UNREADABLE' });
  }
}
fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ schemaVersion: 1, evidenceKind: 'RenderedUiWithSyntheticCameraAndMockBarcodeDetector',
  decoderIsMock: true, physicalScanVerified: false, androidDeviceVerified: false, sourceFilesUnchanged, sourceFingerprints: before,
  serverLogChecked: hostLog !== null, serverFailureCount, serverLogWindow: 'start offset through browser-close only; delayed circuit expiry requires separate host-log audit',
  selectedWidths, selectedScenarios, passes, failures,
  boundary: 'loopback-only; isolated SQLite; no operational business API, camera upload/persistence, actual barcode decoding, native APK, device, real dispatch or payment' }, null, 2));
process.stdout.write(JSON.stringify({ passed: passes.length, failed: failures.length, sourceFilesUnchanged, decoderIsMock: true, serverFailureCount }) + '\n');
if (failures.length) process.exitCode = 1;
