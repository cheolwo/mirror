import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

// The root invokes this after its final build. No server start, database fixture or business write is performed here.
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const base = new URL(process.env.SSALDDEL_LIFE_PREVIEW_URL ?? "http://127.0.0.1:5567");
if (base.protocol !== "http:" || base.hostname !== "127.0.0.1" || base.username || base.password || base.search || base.hash || base.pathname !== "/") {
  throw new Error("A credential-free 127.0.0.1 preview origin is required.");
}
const output = path.resolve(process.env.SSALDDEL_LIFE_PREVIEW_OUTPUT ?? path.join(repo, "artifacts/local/life-facilitation-r23/ui"));
const allowedOutput = path.join(repo, "artifacts/local") + path.sep;
if (!output.startsWith(allowedOutput)) throw new Error("Evidence must remain inside this repository's artifacts/local directory.");
fs.mkdirSync(output, { recursive: true });
const cases = [
  ["work", "pickup-null"], ["work", "delivery-zero"], ["work", "driver-delivery"],
  ["work", "waiting-other"], ["work", "completed"], ["work", "recovery"], ["work", "error"], ["work", "anonymous"],
  ["delivery", "automatic-proposed"], ["delivery", "public-call"], ["delivery", "hybrid-no-candidate"],
  ["delivery", "registration-pending"], ["delivery", "assigned-disclosure"], ["delivery", "disclosure-consented"],
  ["delivery", "completed-unpaid"], ["delivery", "error"], ["delivery", "anonymous"]
];
const sourcePaths = [
  "eng/RoleWorkspacePreview/Program.cs", "eng/RoleWorkspacePreview/LifeFacilitationPreviewFixture.cs",
  "eng/RoleWorkspacePreview/LifeFacilitationPreviewPage.razor", "eng/RoleWorkspacePreview/verify-life-facilitation.mjs",
  "Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodCollaborationDetail.razor",
  "Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodDeliveryDetail.razor",
  "Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodCollaborationPresentation.cs",
  "Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodDeliveryPresentation.cs",
  "Ssalddel.Ui.Common/Areas/App/css/neighborhood-exchange.css", "Ssalddel.Ui.Common/Areas/App/css/neighborhood-delivery.css",
  "eng/RoleWorkspacePreview/bin/Debug/net10.0/Ssalddel.Ui.Common.dll",
  "eng/RoleWorkspacePreview/bin/Debug/net10.0/RoleWorkspacePreview.dll"
];
const sha = (absolute) => crypto.createHash("sha256").update(fs.readFileSync(absolute)).digest("hex");
const fingerprints = () => sourcePaths.filter(p => fs.existsSync(path.join(repo, p))).map(p => ({ path: p, sha256: sha(path.join(repo, p)) }));
const before = fingerprints();
const records = [];
const failures = [];
const requestEvidence = [];
const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : {}) });

function ensure(value, message) { if (!value) throw new Error(message); }
function has(text, value) { ensure(text.includes(value), `Required product text missing: ${value}`); }

try {
  for (const width of [320, 390]) for (const [kind, scenario] of cases) {
    const name = `${kind}-${scenario}-${width}`;
    const context = await browser.newContext({ viewport: { width, height: 844 }, locale: "ko-KR", timezoneId: "Asia/Seoul" });
    const page = await context.newPage();
    const errors = [];
    const assets = [];
    page.on("pageerror", error => errors.push(error.message));
    page.on("response", response => {
      if (new URL(response.url()).pathname.endsWith(".css")) assets.push({ url: response.url(), status: response.status() });
    });
    await context.route("**/*", async route => {
      const request = route.request();
      const url = new URL(request.url());
      const external = url.origin !== base.origin && !["data:", "blob:"].includes(url.protocol);
      const businessWrite = url.origin === base.origin && url.pathname.startsWith("/api/") && request.method() !== "GET";
      requestEvidence.push({ scene: name, method: request.method(), path: url.origin === base.origin ? url.pathname : external ? "external-blocked" : url.protocol, external, businessWrite });
      if (external || businessWrite) return route.abort();
      return route.continue();
    });
    let record = { scene: name, kind, scenario, width, checks: [] };
    try {
      await page.goto(new URL(`/life-facilitation-preview/${kind}/${scenario}`, base).href, { waitUntil: "networkidle" });
      await page.locator(`[data-life-preview-kind="${kind}"][data-life-preview-scenario="${scenario}"]`).waitFor();
      await page.locator("main.exchange-page").waitFor();
      if (scenario === "error") await page.locator('[role="alert"]').first().waitFor();
      else if (scenario === "anonymous") await page.getByRole("heading", { name: "로그인이 필요해요" }).waitFor();
      else await page.getByRole("heading", { name: kind === "work" ? "책 한 상자를 주고받기로 했어요" : "책 한 상자", exact: true }).waitFor();
      await page.evaluate(() => document.fonts.ready);
      const text = await page.locator("main.exchange-page").innerText();
      ensure((await page.evaluate(() => document.documentElement.scrollWidth)) <= width, "Horizontal overflow");
      ensure(errors.length === 0, `Page errors: ${errors.join("; ")}`);
      ensure(!assets.some(x => x.status >= 400), "A stylesheet response failed");
      for (const css of ["neighborhood-exchange.css", "neighborhood-delivery.css"]) {
        ensure(assets.some(x => new URL(x.url).pathname.endsWith(css) && x.status === 200), `Product stylesheet not loaded: ${css}`);
      }
      record.checks.push("no horizontal overflow", "no page errors", "product stylesheets loaded");
      if (scenario === "anonymous") {
        has(text, "로그인이 필요해요");
        ensure(!text.includes("합의한 인계 장소") && !text.includes("픽업 장소 1"), "Anonymous view leaked fixture detail");
        record.checks.push("login state excludes private fixture detail");
      } else if (scenario === "error") {
        ensure(!text.includes("책 한 상자"), "Read failure was replaced by normal fixture detail");
        record.checks.push("read error remains visible without success fallback");
      } else if (kind === "work") {
        if (scenario === "pickup-null") { has(text, "미정"); ensure(!text.includes("0원"), "Unknown cost was displayed as zero"); }
        if (scenario === "delivery-zero") { has(text, "0원"); ensure(!text.includes("금액 미정"), "Explicit zero became unknown"); }
        if (scenario === "driver-delivery") { has(text, "기사 배송"); has(text, "12,000원"); has(text, "기사 배송비"); }
        else ensure(!text.includes("기사 배송비는"), "Direct transfer displays driver fee guidance");
        if (!["completed", "waiting-other"].includes(scenario)) {
          const primary = page.getByRole("button", { name: "완료 확인 요청", exact: true });
          await primary.waitFor();
          const actionBeforeTerms = await page.evaluate(() => {
            const current = document.querySelector("[data-neighborhood-current]");
            const terms = document.querySelector("[data-neighborhood-terms]");
            const primary = [...document.querySelectorAll("main.exchange-page button")].find(x => x.textContent.trim() === "완료 확인 요청");
            const quantity = [...document.querySelectorAll("main.exchange-page dt")].find(x => x.textContent.trim() === "수량");
            return current && terms ? !!(current.compareDocumentPosition(terms) & Node.DOCUMENT_POSITION_FOLLOWING)
              : !!(primary && quantity && (primary.compareDocumentPosition(quantity) & Node.DOCUMENT_POSITION_FOLLOWING));
          });
          ensure(actionBeforeTerms, "Current action must appear before detailed quantity/terms");
          record.checks.push("permitted primary action precedes detailed terms");
        }
        if (scenario === "waiting-other") {
          has(text, "완료 확인 대기");
          ensure(await page.getByRole("button", { name: "완료 확인 요청", exact: true }).count() === 0, "Unexpected new completion action while waiting");
          ensure(await page.getByRole("button", { name: "완료 확인하기", exact: true }).count() === 0, "Requester-only action exposed to provider");
          record.checks.push("other-party wait preserves permitted action boundary");
        }
        if (scenario === "recovery") {
          has(text, "처리 결과 확인이 필요해요");
          await page.getByRole("button", { name: "처리 결과 확인", exact: true }).click();
          await page.getByText("처리 기록이 아직 확인되지 않았습니다. 같은 요청으로 직접 다시 시도할 수 있습니다.", { exact: true }).waitFor();
          record.checks.push("read-only pending result recovery remains unresolved");
        }
        record.checks.push("transfer guidance and agreed cost preserve their meaning");
      } else {
        if (scenario === "automatic-proposed") { has(text, "기사에게 제안 중"); ensure(!text.includes("기사 배차 확정"), "Recommendation was displayed as assignment"); }
        if (scenario === "public-call") has(text, "공개 콜");
        if (scenario === "hybrid-no-candidate") { has(text, "자동 추천과 공개 콜"); has(text, "기사 제안 대기"); ensure(!text.includes("기사 배차 확정"), "No candidate became assigned"); }
        if (scenario === "registration-pending") has(text, "배차 접수 확인 필요");
        if (scenario === "assigned-disclosure") { has(text, "기사 배차 확정"); has(text, "기사에게 배송 정보 제공"); has(text, "기사 정보 제공에 동의하기"); }
        if (scenario === "disclosure-consented") { has(text, "정보 제공 동의 철회"); has(text, "이 기사에게 배송 정보를 제공하는 데 동의한 상태입니다."); }
        if (scenario === "completed-unpaid") { has(text, "전달 완료"); has(text, "직접"); has(text, "지급 예정"); ensure(!text.includes("수금 확인 기록 있음"), "Delivery completion became receipt confirmation"); }
        record.checks.push("dispatch proposal, assignment and payment are separate states");
      }
      record.geometry = await page.evaluate(() => ({ viewport: innerWidth, scrollWidth: document.documentElement.scrollWidth,
        surface: document.querySelector(".exchange-surface") ? { background: getComputedStyle(document.querySelector(".exchange-surface")).backgroundColor, borderRadius: getComputedStyle(document.querySelector(".exchange-surface")).borderRadius } : null }));
      record.status = "Passed";
    } catch (error) {
      record.status = "Failed";
      record.error = error.message;
      failures.push({ scene: name, error: error.message });
    }
    record.pageErrors = errors;
    record.stylesheets = assets;
    const screenshotPath = path.join(output, `${name}.png`);
    await page.screenshot({ path: screenshotPath, fullPage: true });
    record.screenshot = { path: path.relative(repo, screenshotPath).replaceAll(path.sep, "/"), sha256: sha(screenshotPath) };
    fs.writeFileSync(path.join(output, `${name}.txt`), await page.locator("body").innerText(), "utf8");
    records.push(record);
    await context.close();
  }
} finally {
  await browser.close();
  const after = fingerprints();
  const sourceDrift = JSON.stringify(before) !== JSON.stringify(after);
  const writes = requestEvidence.filter(x => x.businessWrite);
  const external = requestEvidence.filter(x => x.external);
  const result = { checkedAt: new Date().toISOString(), status: failures.length || sourceDrift || writes.length || external.length ? "Failed" : "PassedWithinListedScope",
    scope: "Actual product Razor components and VMs with explicit read-only in-process fixture clients. No business API, database, dispatch, external map, physical phone or payment execution.",
    viewports: [320, 390], scenarios: cases.length, screenshots: records.length, records, failures, sourceDrift, sourceBefore: before, sourceAfter: after,
    businessWriteRequests: writes.length, blockedExternalRequests: external.length,
    actualDatabaseVerified: false, actualDispatchVerified: false, apkVerified: false, physicalDeviceVerified: false, actualPaymentVerified: false };
  fs.writeFileSync(path.join(output, "verification.json"), JSON.stringify(result, null, 2), "utf8");
  fs.writeFileSync(path.join(output, "request-evidence.json"), JSON.stringify(requestEvidence, null, 2), "utf8");
  console.log(JSON.stringify({ status: result.status, screenshots: records.length, failures, sourceDrift, businessWriteRequests: writes.length, blockedExternalRequests: external.length, output }));
  if (result.status !== "PassedWithinListedScope") process.exitCode = 1;
}
