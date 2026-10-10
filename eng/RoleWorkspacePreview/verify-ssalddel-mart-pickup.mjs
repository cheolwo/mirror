// 격리 표본의 실제 Razor/Runtime 렌더를 검증합니다. 카메라·디코더·인쇄 창은 모의이며 단말/실물 검증이 아닙니다.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const local = path.join(repo, 'artifacts/local');
const base = new URL(process.env.SSALDDEL_MART_SAMPLE_URL ?? 'http://127.0.0.1:5596/');
if (base.protocol !== 'http:' || base.hostname !== '127.0.0.1' || base.pathname !== '/' || base.username || base.password || base.search || base.hash)
  throw new Error('LOOPBACK_ORIGIN_REQUIRED');
const output = path.resolve(process.env.SSALDDEL_MART_PICKUP_OUTPUT ?? path.join(local, 'ssalddel-mart-package-pickup-r56/ui'));
const binary = path.resolve(process.env.SSALDDEL_MART_SAMPLE_BINARY_DIR ?? path.join(local, 'ssalddel-mart-package-pickup-r56/host'));
if (!output.startsWith(local + path.sep)) throw new Error('LOCAL_EVIDENCE_PATH_REQUIRED');
if (!binary.startsWith(local + path.sep)) throw new Error('LOCAL_BINARY_PATH_REQUIRED');
const hostLog = process.env.SSALDDEL_MART_PICKUP_HOST_LOG ? path.resolve(process.env.SSALDDEL_MART_PICKUP_HOST_LOG) : null;
if (hostLog && !hostLog.startsWith(local + path.sep)) throw new Error('LOCAL_HOST_LOG_REQUIRED');
const hostLogOffset = hostLog ? fs.statSync(hostLog).size : null;
const sources = [
  'eng/RoleWorkspacePreview/Program.cs', 'eng/RoleWorkspacePreview/RoleWorkspacePreview.csproj',
  'eng/RoleWorkspacePreview/SsalddelMartSampleInput.cs', 'eng/RoleWorkspacePreview/SsalddelMartSampleRuntime.cs',
  'eng/RoleWorkspacePreview/SsalddelMartSamplePickup.cs',
  'eng/RoleWorkspacePreview/SsalddelMartSamplePage.razor', 'eng/RoleWorkspacePreview/SsalddelMartSamplePage.razor.css',
  'eng/RoleWorkspacePreview/SsalddelMartPackageSlip.razor', 'eng/RoleWorkspacePreview/SsalddelMartPackageSlip.razor.css',
  'eng/RoleWorkspacePreview/SsalddelMartPackageScanner.razor', 'eng/RoleWorkspacePreview/SsalddelMartPackageScanner.razor.css',
  'eng/RoleWorkspacePreview/wwwroot/js/ssalddel-mart-scanner.js',
  'eng/RoleWorkspacePreview/wwwroot/js/ssalddel-mart-package-print.js',
  'eng/RoleWorkspacePreview/verify-ssalddel-mart-pickup.mjs',
  path.relative(repo, path.join(binary, 'RoleWorkspacePreview.dll')),
  path.relative(repo, path.join(binary, 'Ssalddel.dll'))
];
const fingerprint = () => sources.map(value => ({ path: value,
  sha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(repo, value))).digest('hex') }));
const before = fingerprint(), passes = [], failures = [], labelSamples = [];
fs.mkdirSync(output, { recursive: true });
const ensure = (condition, code) => { if (!condition) throw new Error(code); };

function installFixtures() {
  async function rasterizeImage(image) {
    const display = image.getBoundingClientRect();
    let source = image, svg;
    if (image instanceof SVGElement) {
      svg = new XMLSerializer().serializeToString(image);
      const bytes = new TextEncoder().encode(svg);
      let encoded = '';
      for (let offset = 0; offset < bytes.length; offset += 32768) encoded += String.fromCharCode(...bytes.subarray(offset, offset + 32768));
      source = new Image(); source.src = 'data:image/svg+xml;base64,' + btoa(encoded);
    } else {
      if (!image.src.startsWith('data:image/svg+xml;base64,')) throw new Error('LOCAL_SVG_REQUIRED');
      svg = atob(image.src.split(',')[1]);
    }
    await source.decode();
    const viewBox = new DOMParser().parseFromString(svg, 'image/svg+xml').documentElement.getAttribute('viewBox').split(/\s+/).map(Number);
    const sizes = [['svg-viewbox', viewBox[2], viewBox[3]],
      ['display-dpr1', Math.round(display.width), Math.round(display.height)],
      ['display-dpr3', Math.round(display.width * 3), Math.round(display.height * 3)]];
    const rasters = [];
    for (const [basis, width, height] of sizes) {
      if (!(width > 0 && height > 0 && width <= 4096 && height <= 4096)) throw new Error('QR_RASTER_DIMENSIONS_INVALID');
      const canvas = document.createElement('canvas'); canvas.width = width; canvas.height = height;
      const drawing = canvas.getContext('2d'); drawing.fillStyle = '#ffffff'; drawing.fillRect(0, 0, width, height);
      drawing.drawImage(source, 0, 0, width, height);
      const pixels = drawing.getImageData(0, 0, width, height).data;
      let bytes = '';
      for (let offset = 0; offset < pixels.length; offset += 32768) bytes += String.fromCharCode(...pixels.subarray(offset, offset + 32768));
      rasters.push({ basis, width, height, rgba: btoa(bytes) });
    }
    return rasters;
  }
  if (window !== window.top) {
    // sandbox iframe에는 실제 출력 HTML이 로드됩니다. 시스템 대화상자만 교체합니다.
    window.print = () => {
      const root = window.top.__pickupTest;
      root.printRequests++;
      root.printDocuments.push(document.documentElement.outerHTML);
      root.pendingPrintCaptures++;
      // 공통 문서 출력기는 QR을 img가 아닌 첫 번째 inline SVG로 출력합니다.
      const qr = document.querySelector('.doc-code svg');
      Promise.resolve().then(async () => {
        if (!qr) throw new Error('PRINT_QR_MISSING');
        const rasters = await rasterizeImage(qr);
        root.printRasters.push(rasters);
      }).catch(() => { root.printCaptureFailures++; }).finally(() => {
        root.pendingPrintCaptures--;
        window.dispatchEvent(new Event('afterprint'));
      });
    };
    return;
  }
  const fixture = window.__pickupTest = {
    mode: 'supported', queue: [], cameraRequests: 0, detectorCalls: 0,
    streamsCreated: 0, tracksStopped: 0, constraintsValid: true,
    holdPermission: false, holdDetection: false, pendingPermissions: [], pendingDetections: [], formats: [],
    printRequests: 0, printDocuments: [], directPrintRequests: 0,
    pendingPrintCaptures: 0, printCaptureFailures: 0, printRasters: []
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
  Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: {
    async getUserMedia(constraints) {
      fixture.cameraRequests++;
      fixture.constraintsValid &&= constraints?.audio === false && constraints?.video?.facingMode?.ideal === 'environment';
      if (fixture.mode === 'denied') throw new DOMException('Denied by fixture', 'NotAllowedError');
      if (fixture.holdPermission) return new Promise(resolve => fixture.pendingPermissions.push(() => resolve(createStream())));
      return createStream();
    }
  } });
  class MockBarcodeDetector {
    static async getSupportedFormats() { return fixture.mode === 'unsupported' ? [] : ['qr_code', 'code_128']; }
    constructor(options) { fixture.formats = options?.formats ?? []; }
    async detect() {
      fixture.detectorCalls++;
      if (fixture.holdDetection) return new Promise(resolve => fixture.pendingDetections.push(resolve));
      if (!fixture.queue.length) return [];
      const value = fixture.queue.shift();
      return Array.isArray(value) ? value : [{ format: 'qr_code', rawValue: value }];
    }
  }
  Object.defineProperty(window, 'BarcodeDetector', { configurable: true, value: MockBarcodeDetector });
  fixture.emit = value => { fixture.queue.push(value); };
  fixture.rasterize = rasterizeImage;
  fixture.releasePermissions = () => { fixture.holdPermission = false; fixture.pendingPermissions.splice(0).forEach(resolve => resolve()); };
  fixture.releaseDetections = code => { fixture.holdDetection = false; fixture.pendingDetections.splice(0).forEach(resolve => resolve([{ format: 'qr_code', rawValue: code }])); };
  fixture.snapshot = () => ({ cameraRequests: fixture.cameraRequests, detectorCalls: fixture.detectorCalls,
    streamsCreated: fixture.streamsCreated, tracksStopped: fixture.tracksStopped, constraintsValid: fixture.constraintsValid,
    formats: fixture.formats, pendingPermissionCount: fixture.pendingPermissions.length, pendingDetectionCount: fixture.pendingDetections.length,
    printRequests: fixture.printRequests, directPrintRequests: fixture.directPrintRequests,
    pendingPrintCaptures: fixture.pendingPrintCaptures, printCaptureFailures: fixture.printCaptureFailures });
  window.print = () => { fixture.directPrintRequests++; };
}

const scenarios = ['qr-pickup', 'number-pickup', 'wrong-code', 'mode-switch', 'reset', 'camera-fallback', 'late-permission'];
const selectedScenarios = process.env.SSALDDEL_MART_PICKUP_SCENARIOS?.split(',') ?? scenarios;
const selectedWidths = process.env.SSALDDEL_MART_PICKUP_WIDTHS?.split(',').map(Number) ?? [320, 390, 1280];
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
      await context.addInitScript(installFixtures);
      const page = await context.newPage();
      let storageBefore = null;
      page.on('pageerror', () => errors.push('PAGE_ERROR'));
      page.on('console', message => { if (message.type() === 'error') errors.push('CONSOLE_ERROR'); });
      page.on('websocket', socket => { if (new URL(socket.url()).host !== base.host) blocked.push({ externalWebSocket: true }); });
      const primary = () => page.locator('[data-action="primary"]');
      const start = () => page.locator('[data-package-scan-start]');
      const stop = () => page.locator('[data-package-scan-stop]');
      const snapshot = () => page.evaluate(() => window.__pickupTest.snapshot());
      const stage = () => page.locator('[data-selected-order]').getAttribute('data-stage');
      const stateIs = state => page.locator(`[data-package-scan-state="${state}"]`).waitFor();
      const allTracksStopped = () => page.waitForFunction(() => window.__pickupTest.streamsCreated === window.__pickupTest.tracksStopped);
      const selectRole = async role => {
        await page.locator(`[data-mart-role="${role}"]`).click();
        if (role === 'driver') await page.locator('[data-package-scan-state]').waitFor();
        else await page.locator('[data-package-print]').waitFor();
      };
      const openSlip = async () => {
        const closed = page.locator('details[data-package-slip-details]:not([open]) > summary');
        if (await closed.count()) await closed.first().click();
        const prepare = page.locator('[data-package-issue]');
        if (await prepare.count()) await prepare.click();
        await page.locator('[data-package-qr]').waitFor();
      };
      const readSlip = async () => {
        const qr = page.locator('[data-package-qr]');
        const code = await qr.getAttribute('data-package-code');
        const slip = page.locator('[data-package-confirmation-code]').first();
        const number = await slip.getAttribute('data-package-confirmation-code');
        const labelId = await slip.getAttribute('data-package-label-id');
        const revision = await slip.getAttribute('data-package-revision');
        ensure(/^SAMPLE-PKG-[0-9a-f]{32}-[1-9][0-9]{0,5}$/.test(code ?? ''), 'SYNTHETIC_PACKAGE_CODE_REQUIRED');
        ensure(/^[0-9]{4}$/.test(number ?? ''), 'FOUR_DIGIT_CODE_REQUIRED');
        ensure(typeof labelId === 'string' && labelId.length > 0 && /^[1-9][0-9]*$/.test(revision ?? ''), 'LABEL_IDENTITY_REQUIRED');
        ensure((await qr.getAttribute('src'))?.startsWith('data:image/svg+xml'), 'LOCAL_SVG_QR_REQUIRED');
        ensure(await qr.evaluate(node => node.complete && node.naturalWidth > 0), 'PACKAGE_QR_NOT_RENDERED');
        return { code, number, labelId, revision };
      };
      const beginScan = async () => { await start().click(); await stateIs('scanning'); };
      const emit = value => page.evaluate(code => window.__pickupTest.emit(code), value);
      const hasPickup = async () => await stage() === 'completed';
      const waitPrimary = async kind => page.locator(`[data-action="primary"][data-action-kind="${kind}"]`).waitFor();
      const waitEnabledPrimary = () => page.waitForFunction(() => {
        const button = document.querySelector('[data-action="primary"]'); return button && !button.disabled;
      });
      const packCurrentOrder = async () => {
        await selectRole('worker');
        let guard = 0;
        while (await stage() === 'picking') {
          ensure(++guard <= 40, 'PICKING_DID_NOT_CONVERGE');
          await waitPrimary('start'); await primary().click(); await waitPrimary('pick');
          const lineId = await page.locator('[data-current-line]').getAttribute('data-current-line');
          const rack = (await page.locator('[data-current-rack]').innerText()).trim();
          ensure(await primary().isDisabled(), 'PICKING_CONFIRMATION_BYPASSED');
          await page.locator('[data-rack-input]').fill(rack);
          await page.locator('[data-product-confirm]').check(); await page.locator('[data-quantity-confirm]').check();
          await waitEnabledPrimary(); await primary().click();
          await page.waitForFunction(previous => document.querySelector('[data-current-line]')?.dataset.currentLine !== previous, lineId);
        }
        ensure(await stage() === 'packing', 'PACKING_STAGE_REQUIRED');
        await waitPrimary('pack'); ensure(await primary().isDisabled(), 'PACKING_CONFIRMATION_BYPASSED');
        await page.locator('[data-inventory-confirm]').check(); await page.locator('[data-package-confirm]').check();
        await waitEnabledPrimary(); await primary().click();
        await page.waitForFunction(() => document.querySelector('[data-selected-order]')?.dataset.stage === 'handoff');
        ensure(await page.locator('[data-line-packed="true"]').count() === await page.locator('[data-mart-line]').count(), 'PARTIAL_PACKING_ACCEPTED');
      };
      const inspect = async () => {
        await page.evaluate(() => document.fonts.ready);
        ensure(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'HORIZONTAL_OVERFLOW');
        const heights = await page.locator('.mart-sample button:visible,.mart-sample summary:visible,.mart-sample__check:visible,.mart-sample__field input:visible,[data-pickup-number]:visible')
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
        await page.locator('[data-mart-sample-ready="true"][data-mart-sample-loaded="true"]').waitFor({ timeout: 60_000 });
        storageBefore = await page.evaluate(() => JSON.stringify([Object.entries(localStorage), Object.entries(sessionStorage)]));
        ensure((await snapshot()).cameraRequests === 0, 'CAMERA_REQUESTED_WITHOUT_ACTION');
        await selectRole('worker'); await openSlip();
        const original = await readSlip();
        ensure(await stage() === 'picking' && !await hasPickup(), 'SLIP_ISSUE_COMPLETED_WORK');
        checks.push('slip can be prepared before picking/packing without completing either');

        if (scenario === 'qr-pickup') {
          await capture('slip');
          if ([320, 390].includes(width)) {
            const rasters = await page.evaluate(() => window.__pickupTest.rasterize(document.querySelector('[data-package-qr]')));
            for (const raster of rasters) labelSamples.push({ format: 'QR_CODE', kind: 'preview', expected: original.code, viewportWidth: width, ...raster });
          }
          for (let copy = 0; copy < 2; copy++) {
            const beforePrint = (await snapshot()).printRequests;
            await page.locator('[data-package-print]').click();
            await page.waitForFunction(count => window.__pickupTest.printRequests > count, beforePrint);
            await page.waitForFunction(() => window.__pickupTest.pendingPrintCaptures === 0 && document.querySelectorAll('iframe[aria-hidden="true"]').length === 0);
            ensure((await snapshot()).printCaptureFailures === 0, 'PRINT_QR_RASTER_CAPTURE_FAILED');
            const same = await readSlip();
            ensure(same.labelId === original.labelId && same.revision === original.revision && same.code === original.code, 'REPRINT_CHANGED_LABEL_IDENTITY');
            ensure(await stage() === 'picking' && !await hasPickup(), 'PRINT_COMPLETED_WORK');
          }
          if ([320, 390].includes(width)) {
            const rasters = await page.evaluate(() => window.__pickupTest.printRasters[0]);
            ensure(Array.isArray(rasters) && rasters.length === 3, 'PRINT_QR_RASTERS_MISSING');
            for (const raster of rasters) labelSamples.push({ format: 'QR_CODE', kind: 'print', expected: original.code, viewportWidth: width, ...raster });
          }
          ensure(await page.evaluate(() => window.__pickupTest.printDocuments.every(html =>
            !/replay-(?:record|branch|pickup)-[a-f0-9]{64}|010[- ]?\d{3,4}[- ]?\d{4}|<script\b/i.test(html))), 'PRINT_DOCUMENT_CONTAINS_PRIVATE_OR_EXECUTABLE_DATA');
          checks.push('two mock print dialog requests preserve the label revision; actual printing is not verified');
          await selectRole('driver');
          ensure(await page.locator('[data-package-not-ready]').count() === 1, 'PREPACK_WAIT_REASON_MISSING');
          if (!await start().isDisabled()) {
            await beginScan(); await emit(original.code); await page.locator('[data-mart-operation-error]').waitFor();
            await allTracksStopped(); ensure(!await hasPickup(), 'PREPACK_SCAN_COMPLETED_PICKUP');
            checks.push('prepack exact QR reaches Runtime but cannot complete pickup');
          } else checks.push('prepack scan is disabled and the preparation wait reason is visible');
          await packCurrentOrder();
          await selectRole('driver'); await capture('ready');
          ensure(await page.locator('[data-action-kind="handoff"],[data-driver-confirm]').count() === 0, 'EXTRA_PICKUP_CONFIRMATION_CONTROL');
          await beginScan(); await page.evaluate(code => { window.__pickupTest.emit(code); window.__pickupTest.emit(code); }, original.code);
          await page.locator('[data-pickup-completed]').waitFor(); await allTracksStopped();
          ensure(await stage() === 'completed', 'QR_DID_NOT_COMPLETE_PICKUP');
          const receiptCount = page.locator('[data-pickup-receipt-count]');
          if (await receiptCount.count()) ensure(await receiptCount.first().getAttribute('data-pickup-receipt-count') === '1', 'DUPLICATE_PICKUP_RECEIPT');
          ensure((await snapshot()).formats.every(format => format === 'qr_code'), 'PACKAGE_ACCEPTED_NON_QR_FORMAT');
          await capture('completed');
          await selectRole('worker'); ensure(await hasPickup(), 'WORKER_DID_NOT_OBSERVE_SAME_PICKUP');
          checks.push('a matching scan directly completes pickup without another button; duplicate detection keeps one result', 'worker and driver read the same completed order');
        } else if (scenario === 'number-pickup') {
          await packCurrentOrder(); await selectRole('driver');
          const wrong = original.number === '0000' ? '0001' : '0000';
          await page.locator('[data-pickup-number]').fill(wrong); await page.locator('[data-pickup-number-submit]').click();
          await page.locator('[data-mart-operation-error]').waitFor(); ensure(!await hasPickup(), 'WRONG_NUMBER_COMPLETED_PICKUP');
          await capture('wrong-number');
          await page.locator('[data-pickup-number]').fill(original.number); await page.locator('[data-pickup-number-submit]').click();
          await page.locator('[data-pickup-completed]').waitFor(); ensure(await stage() === 'completed', 'NUMBER_DID_NOT_COMPLETE_PICKUP');
          ensure((await snapshot()).cameraRequests === 0, 'MANUAL_FALLBACK_REQUESTED_CAMERA');
          await capture('completed'); checks.push('wrong four digits keep the order ready; corrected input submits pickup directly without camera or another confirmation');
        } else if (scenario === 'wrong-code') {
          await selectRole('driver');
          ensure(!await start().isDisabled(), 'WRONG_CODE_SCENARIO_REQUIRES_SCANNABLE_ORDER');
          for (const value of [
            'https://example.invalid/not-a-pickup', 'SAMPLE-RACK-001', 'x'.repeat(5_000),
            original.code.replace(/-[1-9][0-9]*$/, '-999999'),
            [{ format: 'qr_code', rawValue: original.code }, { format: 'qr_code', rawValue: 'SAMPLE-RACK-001' }]
          ]) {
            await beginScan(); await emit(value); await stateIs('wrong-code'); await allTracksStopped();
            ensure(!await hasPickup(), 'WRONG_CODE_COMPLETED_PICKUP');
            if (typeof value === 'string') ensure(!await page.locator('main').evaluate((node, code) => node.innerText.includes(code), value), 'WRONG_RAW_CODE_DISPLAYED');
          }
          await capture('rejected'); checks.push('URL, rack, oversized, other revision and mixed results never become pickup requests or displayed raw text');
        } else if (scenario === 'mode-switch') {
          await selectRole('driver'); await page.evaluate(() => { window.__pickupTest.holdDetection = true; });
          await beginScan(); await page.waitForFunction(() => window.__pickupTest.pendingDetections.length > 0);
          await selectRole('worker'); await allTracksStopped();
          await page.evaluate(code => window.__pickupTest.releaseDetections(code), original.code);
          await page.waitForTimeout(300);
          ensure(await stage() === 'picking' && !await hasPickup(), 'LATE_RESULT_CROSSED_ROLE_BOUNDARY');
          ensure(await page.locator('[data-mart-operation-error]').count() === 0, 'OLD_SCAN_REACHED_RUNTIME_AFTER_ROLE_SWITCH');
          await capture('released'); checks.push('leaving the driver mode stops camera; delayed old detection cannot invoke pickup');
        } else if (scenario === 'reset') {
          await selectRole('driver'); await page.evaluate(() => { window.__pickupTest.holdDetection = true; });
          await beginScan(); await page.waitForFunction(() => window.__pickupTest.pendingDetections.length > 0);
          await page.locator('[data-reset-open]').click(); await page.locator('[data-reset-confirm]').click();
          await allTracksStopped(); await selectRole('worker'); await openSlip();
          const next = await readSlip(); ensure(next.code !== original.code && next.labelId !== original.labelId, 'RESET_REUSED_OLD_LABEL');
          await page.evaluate(code => window.__pickupTest.releaseDetections(code), original.code); await page.waitForTimeout(300);
          await selectRole('driver'); await beginScan(); await emit(original.code); await stateIs('wrong-code'); await allTracksStopped();
          ensure(await stage() === 'picking' && !await hasPickup(), 'OLD_LABEL_COMPLETED_RESET_ORDER');
          ensure(await page.locator('[data-line-picked="true"],[data-line-packed="true"]').count() === 0, 'RESET_RETAINED_WORK');
          await capture('old-label-rejected'); checks.push('reset replaces label identity, releases pending camera and rejects the previous run QR');
        } else if (scenario === 'camera-fallback') {
          await selectRole('driver');
          for (const [mode, state] of [['unsupported', 'unsupported'], ['denied', 'permission-denied']]) {
            await page.evaluate(value => { window.__pickupTest.mode = value; }, mode);
            await start().click(); await stateIs(state); await allTracksStopped();
            ensure(await page.locator('[data-pickup-number]').isVisible() && !await hasPickup(), 'CAMERA_FAILURE_REMOVED_MANUAL_FALLBACK');
          }
          await capture('manual-available'); checks.push('unsupported and denied camera show direct four-digit fallback without pickup');
        } else if (scenario === 'late-permission') {
          await selectRole('driver'); await page.evaluate(() => { window.__pickupTest.holdPermission = true; });
          await start().click(); await page.waitForFunction(() => window.__pickupTest.pendingPermissions.length === 1);
          await stop().click(); await stateIs('cancelled');
          await page.evaluate(() => window.__pickupTest.releasePermissions());
          await page.waitForFunction(() => window.__pickupTest.streamsCreated === 1 && window.__pickupTest.tracksStopped === 1);
          ensure(!await hasPickup(), 'LATE_PERMISSION_COMPLETED_PICKUP');
          await capture('cancelled'); checks.push('permission returning after cancellation immediately releases its stream and cannot pick up');
        }
        await allTracksStopped(); await inspect();
        const stats = await snapshot(); ensure(stats.constraintsValid, 'CAMERA_CONSTRAINTS_INVALID');
        ensure(stats.formats.every(format => format === 'qr_code'), 'PACKAGE_ACCEPTED_NON_QR_FORMAT');
        passes.push({ width, scenario, checks: [...checks, 'no overflow; 48px controls; browser storage unchanged; no browser or external/business request errors'], camera: stats, screenshots });
        process.stdout.write(JSON.stringify({ width, scenario, passed: true }) + '\n');
      } catch (error) {
        const code = /^[A-Z0-9_]+$/.test(error.message) ? error.message : 'UI_VERIFICATION_FAILED';
        failures.push({ width, scenario, code, browserErrorCount: errors.length, blockedRequestCount: blocked.length,
          camera: await snapshot().catch(() => null) });
        process.stdout.write(JSON.stringify({ width, scenario, passed: false, code }) + '\n');
        await page.screenshot({ path: path.join(output, `${scenario}-failure-${width}.png`), fullPage: true }).catch(() => {});
      } finally { await context.close(); }
    }
  }
} finally { await browser.close(); }
const after = fingerprint(), sourceFilesUnchanged = JSON.stringify(before) === JSON.stringify(after);
if (!sourceFilesUnchanged) failures.push({ code: 'SOURCE_OR_BINARY_CHANGED_DURING_VERIFICATION' });
if (selectedScenarios.includes('qr-pickup')) {
  fs.writeFileSync(path.join(output, 'label-pixels.json'), JSON.stringify(labelSamples));
  fs.writeFileSync(path.join(output, 'label-capture-result.json'), JSON.stringify({
    scope: 'Actual generated preview/print QR SVG rendered to RGBA at viewBox and actual layout DPR1/DPR3; no mock decoder is used in raster capture; decode must be separately verified.',
    physicalPrintVerified: false, physicalScanVerified: false,
    samples: labelSamples.map(({ rgba, expected, ...sample }) => sample)
  }, null, 2));
}
let serverFailureCount = null;
if (hostLog) {
  try {
    const size = fs.statSync(hostLog).size;
    if (size < hostLogOffset || size > 64 * 1024 * 1024) throw new Error('HOST_LOG_WINDOW_INVALID');
    const window = fs.readFileSync(hostLog).subarray(hostLogOffset).toString('utf8');
    serverFailureCount = (window.match(/^(?:.*\s)?(?:fail|crit):|Unhandled exception/gim) ?? []).length;
    if (serverFailureCount) failures.push({ code: 'SERVER_HOST_FAILURE', count: serverFailureCount });
  } catch { failures.push({ code: 'SERVER_HOST_LOG_UNREADABLE' }); }
}
fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ schemaVersion: 1,
  evidenceKind: 'RenderedPackagePickupUiWithSyntheticCameraMockDecoderAndMockPrintDialog',
  decoderIsMock: true, printDialogIsMock: true, physicalScanVerified: false, physicalPrintVerified: false, androidDeviceVerified: false,
  sourceFilesUnchanged, sourceFingerprints: before, selectedWidths, selectedScenarios,
  serverLogChecked: hostLog !== null, serverFailureCount,
  serverLogWindow: 'start offset through browser-close only; delayed circuit expiry requires a separate host audit',
  passes, failures,
  boundary: 'loopback-only; isolated SQLite; no operational API, uploaded camera frames, persistent browser scan data, actual QR decoding, Android APK, real dispatch, payment or printer' }, null, 2));
process.stdout.write(JSON.stringify({ passed: passes.length, failed: failures.length, sourceFilesUnchanged, decoderIsMock: true, printDialogIsMock: true, serverFailureCount }) + '\n');
if (failures.length) process.exitCode = 1;
