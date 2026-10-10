import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";

const source = (await readFile(new URL("./wwwroot/js/ssalddel-mart-package-print.js", import.meta.url), "utf8"))
    .replace("export function createPrinter", "function createPrinter") + "\nglobalThis.createPrinter = createPrinter;";
const html = "<!doctype html><html><body>표본 전표</body></html>";
class Events {
    listeners = new Map();
    addEventListener(name, callback) { if (!this.listeners.has(name)) this.listeners.set(name, new Set()); this.listeners.get(name).add(callback); }
    removeEventListener(name, callback) { this.listeners.get(name)?.delete(callback); }
    emit(name) { for (const callback of [...(this.listeners.get(name) ?? [])]) callback(); }
    get count() { return [...this.listeners.values()].reduce((n, values) => n + values.size, 0); }
}
function browser(options = {}) {
    const timers = new Map(), frames = [], window = new Events();
    let next = 0, prints = 0;
    const document = Object.assign(new Events(), { visibilityState: "visible", body: { appendChild(frame) {
        if (options.appendThrows) throw new Error("append"); frame.isConnected = true; frames.push(frame);
    } }, createElement(type) {
        assert.equal(type, "iframe");
        const target = Object.assign(new Events(), { focus() {}, print() { if (options.printThrows) throw new Error("print"); prints++; if (options.afterPrint) this.emit("afterprint"); } });
        if (options.noPrint) target.print = null;
        return { style: {}, attributes: {}, isConnected: false, contentWindow: target, setAttribute(key, value) { this.attributes[key] = value; }, remove() { this.isConnected = false; } };
    } });
    const context = vm.createContext({ document, window, setTimeout(callback) { const id = ++next; timers.set(id, callback); return id; }, clearTimeout(id) { timers.delete(id); } });
    vm.runInContext(source, context);
    const printer = context.createPrinter();
    return { printer, document, window, frames, timers, get prints() { return prints; }, load() { frames.at(-1).onload?.(); }, timeout() { for (const callback of [...timers.values()]) callback(); } };
}
test("인쇄 요청은 실물 성공이 아닌 requested를 반환하고 afterprint에 정리한다", async () => {
    const b = browser(); const result = b.printer.request("표본", html); b.load(); assert.equal(await result, "requested");
    assert.equal(b.prints, 1); assert.equal(b.frames[0].attributes.sandbox, "allow-modals allow-same-origin");
    b.frames[0].contentWindow.emit("afterprint"); assert.equal(b.frames[0].isConnected, false); assert.equal(b.timers.size, 0); b.printer.dispose();
});
test("afterprint 동기 콜백도 요청 하나만 처리한다", async () => {
    const b = browser({ afterPrint: true }); const p = b.printer.request("표본", html); b.load(); assert.equal(await p, "requested"); assert.equal(b.timers.size, 0); b.printer.dispose();
});
test("인쇄 창 종료 전 중복 요청은 거절하고 같은 전표를 재출력할 수 있다", async () => {
    const b = browser(); const p = b.printer.request("표본", html); assert.equal(await b.printer.request("표본", html), "unavailable"); b.load(); await p;
    b.frames[0].contentWindow.emit("afterprint"); const second = b.printer.request("표본", html); b.load(); assert.equal(await second, "requested"); assert.equal(b.prints, 2); b.printer.dispose();
});
for (const option of ["printThrows", "noPrint", "appendThrows"]) test(`${option}이면 요청 실패를 반환한다`, async () => {
    const b = browser({ [option]: true }); const p = b.printer.request("표본", html); if (!b.frames.length) {} else b.load();
    assert.equal(await p, "unavailable"); assert.equal(b.timers.size, 0); b.printer.dispose();
});
test("load 시간 초과와 늦은 callback은 인쇄하지 않는다", async () => {
    const b = browser(); const p = b.printer.request("표본", html); const callback = b.frames[0].onload; b.timeout();
    assert.equal(await p, "unavailable"); callback(); assert.equal(b.prints, 0); b.printer.dispose();
});
test("dispose는 대기 요청과 이벤트를 정리하고 재실행을 거절한다", async () => {
    const b = browser(); const p = b.printer.request("표본", html); const callback = b.frames[0].onload; b.printer.dispose(); b.printer.dispose();
    assert.equal(await p, "cancelled"); callback(); assert.equal(b.prints, 0); assert.equal(b.document.count + b.window.count, 0);
    assert.equal(await b.printer.request("표본", html), "unavailable");
});
for (const event of ["hidden", "pagehide"]) test(`${event}는 인쇄 대기를 취소한다`, async () => {
    const b = browser(); const p = b.printer.request("표본", html); if (event === "hidden") { b.document.visibilityState = "hidden"; b.document.emit("visibilitychange"); } else b.window.emit("pagehide");
    assert.equal(await p, "cancelled"); assert.equal(b.frames[0].isConnected, false); b.printer.dispose();
});
test("afterprint 없는 브라우저도 제한 시간에 iframe을 제거한다", async () => {
    const b = browser(); const p = b.printer.request("표본", html); b.load(); assert.equal(await p, "requested"); b.timeout(); assert.equal(b.frames[0].isConnected, false); b.printer.dispose();
});
test("잘못된 입력·과대 HTML·숨겨진 화면은 frame을 만들지 않는다", async () => {
    const b = browser(); for (const value of [null, "short", "x".repeat(1000001)]) assert.equal(await b.printer.request("표본", value), "unavailable");
    b.document.visibilityState = "hidden"; assert.equal(await b.printer.request("표본", html), "unavailable"); assert.equal(b.frames.length, 0); b.printer.dispose();
});
test("이전 인쇄 창의 늦은 afterprint·timeout은 다음 요청을 정리하지 않는다", async () => {
    const b = browser(); const first = b.printer.request("첫 표본", html); b.load(); await first;
    const oldAfter = [...b.frames[0].contentWindow.listeners.get("afterprint")][0];
    const oldTimeout = [...b.timers.values()][0];
    b.frames[0].contentWindow.emit("afterprint");
    const next = b.printer.request("다음 표본", html);
    oldAfter(); oldTimeout();
    assert.equal(b.frames[1].isConnected, true); assert.equal(b.timers.size, 1);
    b.load(); assert.equal(await next, "requested"); assert.equal(b.prints, 2); b.printer.dispose();
});
