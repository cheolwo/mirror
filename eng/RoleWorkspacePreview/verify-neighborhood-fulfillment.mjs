import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const base = new URL(process.env.SSALDDEL_FULFILLMENT_PREVIEW_URL ?? "http://127.0.0.1:5574");
if (base.protocol !== "http:" || base.hostname !== "127.0.0.1" || base.pathname !== "/" || base.username || base.password || base.search || base.hash)
  throw new Error("Credential-free loopback preview required.");
const output = path.resolve(process.env.SSALDDEL_FULFILLMENT_PREVIEW_OUTPUT ?? path.join(repo, "artifacts/local/neighborhood-fulfillment-r24/ui"));
if (!output.startsWith(path.join(repo, "artifacts/local") + path.sep)) throw new Error("Evidence must stay under artifacts/local.");
fs.mkdirSync(output, { recursive: true });
const scenarios = ["create", "create-lost", "feature-off", "expired", "accepted", "unknown", "awaiting-selection", "cancelled", "completed", "error", "anonymous"];
const sources = [
  "eng/RoleWorkspacePreview/Program.cs", "eng/RoleWorkspacePreview/NeighborhoodFulfillmentPreviewFixture.cs",
  "eng/RoleWorkspacePreview/NeighborhoodFulfillmentPreviewPage.razor", "eng/RoleWorkspacePreview/verify-neighborhood-fulfillment.mjs",
  "Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodDeliveryCreate.razor",
  "Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodDeliveryDetail.razor",
  "Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodFulfillmentOptions.razor",
  "Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodDeliveryAuthoringViewModel.cs",
  "Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodDeliveryQueryViewModel.cs",
  "Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodDeliveryPresentation.cs",
  "Ssalddel.Ui.Common/wwwroot/Areas/App/css/neighborhood-delivery.css",
  "eng/RoleWorkspacePreview/bin/Debug/net10.0/Ssalddel.Ui.Common.dll", "eng/RoleWorkspacePreview/bin/Debug/net10.0/RoleWorkspacePreview.dll"
];
const fingerprint = () => sources.filter(p => fs.existsSync(path.join(repo, p))).map(p => ({ path: p, sha256: crypto.createHash("sha256").update(fs.readFileSync(path.join(repo, p))).digest("hex") }));
const before = fingerprint(), records = [], failures = [], requests = [];
const ensure = (value, message) => { if (!value) throw new Error(message); };
const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : {}) });
try {
  for (const width of [320, 390]) for (const scenario of scenarios) {
    const name = `${scenario}-${width}`;
    const context = await browser.newContext({ viewport: { width, height: 844 }, locale: "ko-KR", timezoneId: "Asia/Seoul" });
    const page = await context.newPage(), errors = [], assets = [];
    page.on("pageerror", error => errors.push(error.message));
    page.on("response", response => { if (new URL(response.url()).pathname.endsWith(".css")) assets.push({ path: new URL(response.url()).pathname, status: response.status() }); });
    await context.route("**/*", async route => {
      const request = route.request(), url = new URL(request.url());
      const external = url.origin !== base.origin && !["data:", "blob:"].includes(url.protocol);
      const business = url.origin === base.origin && url.pathname.startsWith("/api/");
      requests.push({ scene: name, method: request.method(), path: external ? "external-blocked" : url.pathname, external, business });
      if (external || business) return route.abort();
      return route.continue();
    });
    const checks = [];
    try {
      await page.goto(new URL(`/neighborhood-fulfillment-preview/${scenario}`, base).href, { waitUntil: "networkidle" });
      await page.locator(`[data-fulfillment-scenario="${scenario}"]`).waitFor();
      await page.locator("main.exchange-page").waitFor();
      if (scenario === "error") await page.locator('[role="alert"]').first().waitFor();
      else if (scenario === "anonymous") await page.getByRole("heading", { name: "로그인이 필요해요", exact: true }).waitFor();
      else if (["create", "create-lost", "feature-off", "expired"].includes(scenario)) {
        await page.getByRole("checkbox", { name: "거래 당사자와 거래·개인정보 안내를 확인했습니다.", exact: true }).check();
        await page.getByRole("button", { name: "배송비 확인하기", exact: true }).click();
        await page.getByRole("heading", { name: "배송비 확인", exact: true }).waitFor();
        if (scenario !== "feature-off") await page.getByRole("radio", { name: /모의 퀵배송/ }).waitFor();
        else await page.getByRole("checkbox", { name: /직접 현장 지급/ }).waitFor();
      }
      else await page.getByRole("heading", { name: "책 한 상자", exact: true }).waitFor();
      await page.evaluate(() => document.fonts.ready);
      let text = await page.locator("main.exchange-page").innerText();
      if (scenario === "anonymous") ensure(!text.includes("픽업 장소 1"), "Anonymous detail leak");
      else if (scenario === "error") ensure(!text.includes("책 한 상자"), "Failure replaced with success");
      else if (["create", "create-lost", "expired"].includes(scenario)) {
        ensure(text.includes("금액 확인 필요"), "Unknown provider cost became zero");
        const submit = page.getByRole("button", { name: "배송 요청 접수하기", exact: true });
        ensure(await submit.isDisabled(), "Recommended option auto-confirmed");
        await page.getByRole("radio", { name: /모의 퀵배송/ }).check();
        await page.getByRole("checkbox", { name: /총비용/ }).check();
        ensure(await submit.isDisabled(), "Information sharing consent omitted");
        await page.getByRole("checkbox", { name: /선택한 처리처에/ }).check();
        await page.getByRole("checkbox", { name: /위 조건으로 배송/ }).check();
        // check() confirms the browser input. The InteractiveServer VM response and button rendering arrive asynchronously.
        await page.waitForFunction(() => {
          const button = [...document.querySelectorAll("main.exchange-page button[type='submit']")].find(value => value.textContent.trim() === "배송 요청 접수하기");
          return button && !button.disabled;
        }, null, { timeout: 10000 });
        ensure(await submit.isEnabled(), "Confirmed valid option not enabled");
        ensure(await page.getByRole("radio", { name: /모의 생활배송/ }).isDisabled(), "Unknown/expired candidate enabled");
        checks.push("recommendation requires explicit selection, cost, sharing and dispatch consent", "unknown candidate unavailable");
        if (scenario === "create-lost") {
          await submit.click(); await page.getByRole("button", { name: "같은 요청 다시 확인하기", exact: true }).waitFor();
          await page.waitForFunction(() => document.querySelector('[data-fulfillment-preview-calls]')?.dataset.createCalls === "1");
          const first = await page.locator('[data-fulfillment-preview-calls]').getAttribute("data-create-id");
          await page.getByRole("button", { name: "같은 요청 다시 확인하기", exact: true }).click();
          await page.waitForFunction(() => document.querySelector('[data-fulfillment-preview-calls]')?.dataset.createCalls === "2");
          ensure(first === await page.locator('[data-fulfillment-preview-calls]').getAttribute("data-create-id"), "Recovery changed create ID");
          checks.push("lost submission preserves original ID and explicit replay");
        }
      } else if (scenario === "feature-off") {
        ensure(await page.getByRole("radio").count() === 0, "Off feature presents new providers");
        ensure(text.includes("기사를 찾는 방식") && text.includes("직접 현장 지급"), "Legacy flow absent");
        checks.push("feature-off retains legacy dispatch and payment");
      } else if (scenario === "accepted") {
        await page.getByRole("button", { name: "현재 처리처 접수 취소 요청", exact: true }).click();
        await page.getByText("취소 결과 확인 중", { exact: true }).first().waitFor();
        ensure(await page.getByRole("button", { name: "새 처리처 견적 확인", exact: true }).count() === 0, "Switch exposed before cancellation acknowledgement");
        await page.getByRole("button", { name: "진행 새로고침", exact: true }).click();
        await page.getByRole("button", { name: "새 처리처 견적 확인", exact: true }).waitFor();
        checks.push("cancellation pending blocks switching until authoritative confirmation");
      } else if (scenario === "unknown") {
        ensure(text.includes("접수 결과 확인 중"), "Unknown displayed accepted");
        await page.getByRole("button", { name: "진행 새로고침", exact: true }).click();
        await page.getByRole("button", { name: "원 요청 결과 확인", exact: true }).waitFor();
        const original = await page.locator('[data-fulfillment-preview-calls]').getAttribute("data-action-id");
        await page.getByRole("button", { name: "원 요청 결과 확인", exact: true }).click();
        await page.getByText("처리처 접수 확인", { exact: true }).first().waitFor();
        ensure(original === await page.locator('[data-fulfillment-preview-calls]').getAttribute("data-action-id"), "Recovery changed operation ID");
        checks.push("uncertain result reuses original operation ID");
      } else if (scenario === "cancelled" || scenario === "awaiting-selection") {
        if (scenario === "awaiting-selection") {
          ensure(text.includes("배송 처리처 선택 필요") && text.includes("금액 확인 필요"), "Unselected pending intake falsely showed a known provider or cost");
          ensure(await page.getByRole("button", { name: "현재 처리처 접수 취소 요청", exact: true }).count() === 0, "Unselected intake exposed provider cancellation");
        }
        await page.getByRole("button", { name: "새 처리처 견적 확인", exact: true }).click();
        await page.getByRole("radio", { name: /자체 배차/ }).check();
        await page.getByRole("checkbox", { name: /총비용/ }).check();
        const choice = page.getByRole("button", { name: scenario === "awaiting-selection" ? "선택한 처리처로 접수" : "선택한 처리처로 다시 접수", exact: true });
        ensure(await choice.isDisabled(), "New internal choice omitted direct payment agreement");
        await page.getByRole("checkbox", { name: /직접 현장 지급/ }).check(); await choice.click();
        await page.getByText("처리처 접수 확인", { exact: true }).first().waitFor();
        checks.push(scenario === "awaiting-selection" ? "pending unselected intake recovers through cost and direct-payment-confirmed selection" : "confirmed cancellation permits cost and direct-payment-confirmed new selection");
      } else if (scenario === "completed") {
        ensure(text.includes("실제 지급 미확인") && text.includes("운영 정산 대상에서 제외"), "Mock completion became payment/settlement");
        ensure(await page.getByRole("button", { name: "현재 처리처 접수 취소 요청", exact: true }).count() === 0, "Completed cancellation exposed");
        checks.push("mock completion remains separate from real payment");
      }
      ensure(await page.evaluate(() => document.documentElement.scrollWidth) <= width, "Horizontal overflow");
      ensure(errors.length === 0, `Page errors: ${errors.join(";")}`);
      ensure(!assets.some(value => value.status >= 400), "Stylesheet failure");
      ensure(assets.some(value => value.path.endsWith("neighborhood-delivery.css") && value.status === 200), "Product stylesheet not loaded");
      await page.screenshot({ path: path.join(output, `${name}.png`), fullPage: true });
      records.push({ scene: name, width, scenario, checks: [...checks, "actual shared Razor/VM", "no horizontal overflow", "stylesheets loaded", "no page errors"], screenshot: `${name}.png` });
    } catch (error) {
      const diagnostics = await page.evaluate(() => ({
        checkboxes: [...document.querySelectorAll("main.exchange-page input[type='checkbox']")].map(value => ({ label: value.closest("label")?.innerText ?? "", checked: value.checked, disabled: value.disabled })),
        buttons: [...document.querySelectorAll("main.exchange-page button")].map(value => ({ text: value.textContent.trim(), disabled: value.disabled })),
        quoteChangedNotice: document.querySelector("main.exchange-page")?.innerText.includes("입력이 바뀌었습니다.") ?? false
      })).catch(() => null);
      failures.push({ scene: name, error: error.message, diagnostics });
      await page.screenshot({ path: path.join(output, `${name}-failed.png`), fullPage: true }).catch(() => {});
    }
    finally { await context.close(); }
  }
} finally {
  await browser.close(); const after = fingerprint();
  const sourceDrift = before.filter(value => !after.some(next => next.path === value.path && next.sha256 === value.sha256));
  const businessRequests = requests.filter(value => value.business), externalRequests = requests.filter(value => value.external);
  const result = { status: failures.length === 0 && sourceDrift.length === 0 && businessRequests.length === 0 ? "PassedWithinListedScope" : "Failed",
    scope: "Actual shared Razor and VM in loopback-only synthetic in-process fixtures; no business API/DB/provider operation/payment.",
    records, failures, sourceBefore: before, sourceAfter: after, sourceDrift, businessRequests: businessRequests.length, blockedExternalRequests: externalRequests.length,
    actualDatabaseVerified: false, actualExternalProviderVerified: false, apkVerified: false, physicalDeviceVerified: false, actualPaymentVerified: false };
  fs.writeFileSync(path.join(output, "verification.json"), JSON.stringify(result, null, 2), "utf8");
  fs.writeFileSync(path.join(output, "request-evidence.json"), JSON.stringify(requests, null, 2), "utf8");
  console.log(JSON.stringify({ status: result.status, screenshots: records.length, failures, sourceDrift, output }));
  if (result.status !== "PassedWithinListedScope") process.exitCode = 1;
}
