import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";

const source = (await readFile(new URL("./wwwroot/js/ssalddel-mart-scanner.js", import.meta.url), "utf8"))
    .replace("export function createScanner", "function createScanner") + "\nglobalThis.createScanner = createScanner;";
const rack = "SAMPLE-RACK-001";
const packageCode = "SAMPLE-PKG-0123456789abcdef0123456789abcdef-1";
const tick = async () => { for (let i = 0; i < 12; i++) await Promise.resolve(); };
function deferred() { let resolve, reject; const promise = new Promise((yes, no) => { resolve = yes; reject = no; }); return { promise, resolve, reject }; }

class Events {
    listeners = new Map();
    addEventListener(name, callback) { if (!this.listeners.has(name)) this.listeners.set(name, new Set()); this.listeners.get(name).add(callback); }
    removeEventListener(name, callback) { this.listeners.get(name)?.delete(callback); }
    emit(name) { for (const callback of [...(this.listeners.get(name) ?? [])]) callback(); }
    get listenerCount() { return [...this.listeners.values()].reduce((sum, values) => sum + values.size, 0); }
}

function browser(options = {}) {
    let nextTimer = 1, now = 0, mediaCalls = 0, detectCalls = 0, stopCalls = 0, pauseCalls = 0;
    const timers = new Map(), observers = [], calls = [], formats = [], constraints = [];
    const track = Object.assign(new Events(), { stop() { stopCalls++; } });
    const stream = { getTracks: () => [track] };
    const document = Object.assign(new Events(), { visibilityState: "visible", documentElement: {}, modal: false,
        querySelector() { return this.modal ? {} : null; } });
    const window = new Events();
    const video = { isConnected: true, readyState: 2, srcObject: null,
        play: options.play ?? (async () => {}), pause() { pauseCalls++; } };
    const context = vm.createContext({ isSecureContext: options.secure ?? true, document, window,
        navigator: { onLine: true, mediaDevices: { getUserMedia(request) { mediaCalls++; constraints.push(request); return options.media ? options.media(stream) : Promise.resolve(stream); } } },
        setTimeout(callback, delay) { const id = nextTimer++; timers.set(id, { callback, at: now + delay }); return id; },
        clearTimeout(id) { timers.delete(id); },
        MutationObserver: class { constructor(callback) { this.callback = callback; this.active = false; observers.push(this); } observe() { this.active = true; } disconnect() { this.active = false; } },
        BarcodeDetector: class {
            static getSupportedFormats() { return options.supported ? options.supported() : Promise.resolve(options.formats ?? ["qr_code", "code_128", "ean_13"]); }
            constructor(request) { formats.push(request.formats); }
            detect() { detectCalls++; return options.detect ? options.detect(detectCalls) : Promise.resolve([]); }
        }
    });
    vm.runInContext(source, context);
    const dotnet = { invokeMethodAsync(...args) { calls.push(args); return options.callback ? options.callback(...args) : Promise.resolve(); } };
    const scanner = context.createScanner(video, dotnet, "session-1", options.expected ?? rack, options.purpose);
    async function advance(milliseconds) {
        const target = now + milliseconds;
        let guard = 0;
        while (true) {
            const pending = [...timers].filter(([, timer]) => timer.at <= target).sort((a, b) => a[1].at - b[1].at)[0];
            if (!pending) break;
            if (++guard > 1000) throw new Error("Timer loop did not stop.");
            now = pending[1].at; timers.delete(pending[0]); pending[1].callback(); await tick();
        }
        now = target; await tick();
    }
    function mutate() { for (const observer of observers) if (observer.active) observer.callback(); }
    function clean() {
        assert.equal(timers.size, 0, "all timers removed");
        assert.equal(document.listenerCount + window.listenerCount + track.listenerCount, 0, "all listeners removed");
        assert.equal(observers.filter(observer => observer.active).length, 0, "all observers removed");
        assert.equal(video.srcObject, null, "video released");
    }
    return { scanner, context, document, window, video, track, stream, calls, formats, constraints, timers, advance, mutate, clean,
        get mediaCalls() { return mediaCalls; }, get detectCalls() { return detectCalls; }, get stopCalls() { return stopCalls; }, get pauseCalls() { return pauseCalls; } };
}

for (const format of ["qr_code", "code_128"]) test(`${format}: 현재 합성 선반만 반환하고 장치를 해제한다`, async () => {
    const b = browser({ formats: [format, "ean_13"], detect: async () => [{ format, rawValue: rack }] });
    assert.equal(await b.scanner.start(), "scanning");
    assert.deepEqual(Array.from(b.formats[0]), [format]);
    assert.equal(b.constraints[0].audio, false);
    assert.equal(b.constraints[0].video.facingMode.ideal, "environment");
    await b.advance(0);
    assert.deepEqual(b.calls, [["OnScanAsync", "session-1", "matched", rack]]);
    assert.equal(b.stopCalls, 1); b.clean();
});

for (const value of ["SAMPLE-RACK-002", "SAMPLE-RACK-01", " SAMPLE-RACK-001", "SAMPLE-RACK-001\n", "SKU:REAL-123", "https://example.invalid/private", "x".repeat(5000)]) test(`오인식 원문은 전달하지 않는다: ${value.slice(0, 28)}`, async () => {
    const b = browser({ detect: async () => [{ format: "qr_code", rawValue: value }] });
    assert.equal(await b.scanner.start(), "scanning"); await b.advance(0);
    assert.deepEqual(b.calls, [["OnScanAsync", "session-1", "wrong-code", ""]]); b.clean();
});

test("허용 형식 밖 결과와 혼합 결과는 거절한다", async () => {
    for (const results of [[{ format: "ean_13", rawValue: rack }], [{ format: "qr_code", rawValue: rack }, { format: "qr_code", rawValue: "OTHER" }]]) {
        const b = browser({ detect: async () => results }); await b.scanner.start(); await b.advance(0);
        assert.deepEqual(b.calls[0], ["OnScanAsync", "session-1", "wrong-code", ""]); b.clean();
    }
});

test("인식 전 45초에 직접 입력으로 복귀한다", async () => {
    const b = browser(); await b.scanner.start(); await b.advance(44_999); assert.equal(b.calls.length, 0);
    await b.advance(1); assert.deepEqual(b.calls, [["OnScanAsync", "session-1", "timeout", ""]]); assert.equal(b.stopCalls, 1); b.clean();
});

test("중복 시작은 권한 요청과 스캔 루프를 만들지 않는다", async () => {
    const permission = deferred(); const b = browser({ media: () => permission.promise });
    const first = b.scanner.start(), second = b.scanner.start(); assert.equal(first, second); await tick(); assert.equal(b.mediaCalls, 1);
    permission.resolve(b.stream); assert.equal(await first, "scanning"); assert.equal(await b.scanner.start(), "scanning"); assert.equal(b.mediaCalls, 1);
    await b.advance(0); assert.equal(b.detectCalls, 1); b.scanner.stop(); b.clean();
});

test("권한 대기 취소는 즉시 끝나고 늦은 스트림도 해제한다", async () => {
    const permission = deferred(); const b = browser({ media: () => permission.promise }); const start = b.scanner.start(); await tick();
    b.scanner.stop(); assert.equal(await start, "cancelled"); b.clean(); permission.resolve(b.stream); await tick();
    assert.equal(b.stopCalls, 1); assert.equal(b.calls.length, 0); b.clean();
});

test("권한 반환과 await 재개 사이 취소도 스트림을 해제한다", async () => {
    for (let microtasks = 0; microtasks < 8; microtasks++) {
        const permission = deferred(); const b = browser({ media: () => permission.promise }); const start = b.scanner.start(); await tick();
        permission.resolve(b.stream);
        for (let i = 0; i < microtasks; i++) await Promise.resolve();
        b.scanner.stop(); await start; await tick();
        assert.equal(b.stopCalls, 1, `microtask gap ${microtasks}`); assert.equal(b.calls.length, 0); b.clean();
    }
});

test("권한 창이 응답하지 않아도 45초에 대기를 끝낸다", async () => {
    const permission = deferred(); const b = browser({ media: () => permission.promise }); const start = b.scanner.start(); await tick();
    await b.advance(45_000); assert.equal(await start, "cancelled"); assert.deepEqual(b.calls[0], ["OnScanAsync", "session-1", "timeout", ""]); b.clean();
    permission.resolve(b.stream); await tick(); assert.equal(b.stopCalls, 1);
});

test("지원 형식 조회 대기 중 취소하면 카메라를 열지 않는다", async () => {
    const supported = deferred(); const b = browser({ supported: () => supported.promise }); const start = b.scanner.start(); await tick();
    b.scanner.stop(); assert.equal(await start, "cancelled"); supported.resolve(["qr_code"]); await tick(); assert.equal(b.mediaCalls, 0); b.clean();
});

test("늦은 인식과 중복 인식은 취소 뒤 반영하지 않는다", async () => {
    const detect = deferred(); const b = browser({ detect: () => detect.promise }); await b.scanner.start(); await b.advance(0);
    b.scanner.stop(); detect.resolve([{ format: "qr_code", rawValue: rack }]); await tick(); assert.equal(b.calls.length, 0); assert.equal(b.stopCalls, 1); b.clean();
});

test("새 시작 뒤 이전 인식 결과는 새 세션을 종료하지 않는다", async () => {
    const old = deferred(); const b = browser({ detect: count => count === 1 ? old.promise : Promise.resolve([]) });
    await b.scanner.start(); await b.advance(0); b.scanner.stop(); await b.scanner.start(); await b.advance(0);
    old.resolve([{ format: "qr_code", rawValue: rack }]); await tick(); assert.equal(b.calls.length, 0); assert.notEqual(b.video.srcObject, null);
    b.scanner.stop(); assert.equal(b.stopCalls, 2); b.clean();
});

for (const [name, state] of [["NotAllowedError", "permission-denied"], ["SecurityError", "permission-denied"], ["NotFoundError", "camera-unavailable"], ["NotReadableError", "camera-unavailable"], ["OverconstrainedError", "camera-unavailable"], ["AbortError", "failed"]]) test(`카메라 실패 ${name}: ${state}`, async () => {
    const b = browser({ media: async () => { const error = new Error("private detail"); error.name = name; throw error; } });
    assert.equal(await b.scanner.start(), state); assert.equal(b.calls.length, 0); b.clean();
});

test("미지원·비보안 환경은 카메라를 요청하지 않는다", async () => {
    for (const options of [{ secure: false }, { formats: ["ean_13"] }, { expected: "https://example.invalid" }, { expected: rack + "\n" }, { expected: { toString: () => rack } }]) {
        const b = browser(options); assert.equal(await b.scanner.start(), options.secure === false ? "insecure" : "unsupported");
        assert.equal(b.mediaCalls, 0); b.clean();
    }
    const b = browser(); delete b.context.BarcodeDetector; assert.equal(await b.scanner.start(), "unsupported"); assert.equal(b.mediaCalls, 0); b.clean();
});

test("숨김·이탈·오프라인·DOM 제거·재연결 표시가 카메라를 해제한다", async () => {
    const scenarios = [
        ["hidden", b => { b.document.visibilityState = "hidden"; b.document.emit("visibilitychange"); }],
        ["disconnected", b => b.window.emit("pagehide")],
        ["disconnected", b => b.window.emit("offline")],
        ["disconnected", b => { b.video.isConnected = false; b.mutate(); }],
        ["disconnected", b => { b.document.modal = true; b.mutate(); }]
    ];
    for (const [state, effect] of scenarios) {
        const b = browser(); await b.scanner.start(); effect(b); await tick();
        assert.deepEqual(b.calls, [["OnScanAsync", "session-1", state, ""]]); assert.equal(b.stopCalls, 1); b.clean();
    }
});

test("권한 대기 중 백그라운드 이동도 늦은 스트림을 정리한다", async () => {
    const permission = deferred(); const b = browser({ media: () => permission.promise }); const start = b.scanner.start(); await tick();
    b.document.visibilityState = "hidden"; b.document.emit("visibilitychange"); assert.equal(await start, "cancelled"); await tick();
    assert.deepEqual(b.calls[0], ["OnScanAsync", "session-1", "hidden", ""]); permission.resolve(b.stream); await tick(); assert.equal(b.stopCalls, 1); b.clean();
});

test("재생 실패·디코더 실패·연결 callback 실패는 모두 장치를 정리한다", async () => {
    const play = browser({ play: async () => { throw new Error("play failure"); } }); assert.equal(await play.scanner.start(), "failed"); assert.equal(play.stopCalls, 1); play.clean();
    const detect = browser({ detect: async () => { throw new Error("decode failure"); } }); await detect.scanner.start(); await detect.advance(0); assert.deepEqual(detect.calls[0], ["OnScanAsync", "session-1", "failed", ""]); detect.clean();
    for (const callback of [() => Promise.reject(new Error("disconnect")), () => { throw new Error("sync disconnect"); }]) {
        const b = browser({ detect: async () => [{ format: "qr_code", rawValue: rack }], callback }); await b.scanner.start(); await b.advance(0); await tick(); assert.equal(b.stopCalls, 1); b.clean();
    }
});

test("재생 대기 중 취소·장치 종료·반복 폐기를 안전하게 처리한다", async () => {
    const play = deferred(); const b = browser({ play: () => play.promise }); const start = b.scanner.start(); await tick(); b.scanner.dispose();
    assert.equal(await start, "cancelled"); play.resolve(); await tick(); assert.equal(b.stopCalls, 1); assert.equal(await b.scanner.start(), "cancelled"); b.scanner.dispose(); b.clean();
    const ended = browser(); await ended.scanner.start(); ended.track.emit("ended"); await tick(); assert.deepEqual(ended.calls[0], ["OnScanAsync", "session-1", "failed", ""]); ended.clean();
});

test("영상·원문을 저장·업로드·로그하는 API가 없다", () => {
    assert.doesNotMatch(source, /\b(fetch|XMLHttpRequest|WebSocket|localStorage|sessionStorage|IndexedDB|MediaRecorder)\b|console\s*\./);
});

test("포장 전표는 QR만 읽고 장치 해제 뒤 한 번만 픽업 요청을 전달한다", async () => {
    let b;
    b = browser({ purpose: "package", expected: packageCode,
        detect: async () => [{ format: "qr_code", rawValue: packageCode }],
        callback: async () => { assert.equal(b.stopCalls, 1); assert.equal(b.video.srcObject, null); }
    });
    assert.equal(await b.scanner.start(), "scanning");
    assert.deepEqual(Array.from(b.formats[0]), ["qr_code"]);
    await b.advance(0); await b.advance(2_000);
    assert.deepEqual(b.calls, [["OnScanAsync", "session-1", "matched", packageCode]]);
    assert.equal(b.detectCalls, 1); assert.equal(b.stopCalls, 1); b.clean();
});

for (const value of [
    rack,
    packageCode.replace(/-1$/, "-2"),
    packageCode.replace("0123456789abcdef", "1123456789abcdef"),
    packageCode.toUpperCase(),
    packageCode.replace(/-1$/, "-0"),
    packageCode.replace(/-1$/, "-01"),
    packageCode.replace(/-1$/, "-1000000"),
    packageCode + "\n",
    packageCode + "\r\n",
    "https://example.invalid/pickup",
    "x".repeat(5_000)
]) test(`포장 전표의 타 대상·구 판본·원문은 전달하지 않는다: ${value.slice(0, 35)}`, async () => {
    const b = browser({ purpose: "package", expected: packageCode, detect: async () => [{ format: "qr_code", rawValue: value }] });
    assert.equal(await b.scanner.start(), "scanning"); await b.advance(0);
    assert.deepEqual(b.calls, [["OnScanAsync", "session-1", "wrong-code", ""]]); assert.equal(b.stopCalls, 1); b.clean();
});

test("포장 전표의 Code128·혼합·잘못된 결과는 픽업 요청이 아니다", async () => {
    for (const results of [
        [{ format: "code_128", rawValue: packageCode }],
        [{ format: "qr_code", rawValue: packageCode }, { format: "qr_code", rawValue: rack }],
        [{ format: "qr_code", rawValue: packageCode }, { format: "code_128", rawValue: packageCode }],
        [{ format: "qr_code", rawValue: null }]
    ]) {
        const b = browser({ purpose: "package", expected: packageCode, detect: async () => results });
        await b.scanner.start(); await b.advance(0);
        assert.deepEqual(b.calls, [["OnScanAsync", "session-1", "wrong-code", ""]]); b.clean();
    }
});

test("잘못된 목적·포장 대상·QR 미지원은 카메라를 열지 않는다", async () => {
    for (const options of [
        { purpose: "other", expected: packageCode },
        { purpose: "", expected: packageCode },
        { purpose: null, expected: packageCode },
        { purpose: "rack", expected: packageCode },
        { purpose: "package", expected: rack },
        { purpose: "package", expected: packageCode + "\n" },
        { purpose: "package", expected: packageCode.replace(/-1$/, "-0") },
        { purpose: "package", expected: packageCode.replace(/-1$/, "-01") },
        { purpose: "package", expected: packageCode, formats: ["code_128"] }
    ]) {
        const b = browser(options); assert.equal(await b.scanner.start(), "unsupported");
        assert.equal(b.mediaCalls, 0); b.clean();
    }
});

test("포장 전표의 최대 판본도 정확히 일치할 때만 읽는다", async () => {
    const expected = packageCode.replace(/-1$/, "-999999");
    const b = browser({ purpose: "package", expected, detect: async () => [{ format: "qr_code", rawValue: expected }] });
    assert.equal(await b.scanner.start(), "scanning"); await b.advance(0);
    assert.deepEqual(b.calls, [["OnScanAsync", "session-1", "matched", expected]]); b.clean();
});

test("포장 전표도 취소·폐기 후 늦은 인식을 전달하지 않는다", async () => {
    for (const action of ["stop", "dispose"]) {
        const detect = deferred(); const b = browser({ purpose: "package", expected: packageCode, detect: () => detect.promise });
        await b.scanner.start(); await b.advance(0); b.scanner[action]();
        detect.resolve([{ format: "qr_code", rawValue: packageCode }]); await tick();
        assert.equal(b.calls.length, 0); assert.equal(b.stopCalls, 1); b.clean();
    }
});

test("포장 전표 권한 대기 중 폐기는 늦은 스트림을 해제한다", async () => {
    const permission = deferred(); const b = browser({ purpose: "package", expected: packageCode, media: () => permission.promise });
    const start = b.scanner.start(); await tick(); b.scanner.dispose(); assert.equal(await start, "cancelled");
    permission.resolve(b.stream); await tick(); assert.equal(b.calls.length, 0); assert.equal(b.stopCalls, 1); b.clean();
});

test("포장 전표 숨김·연결 해제·45초 만료는 픽업 요청을 만들지 않는다", async () => {
    for (const [state, effect] of [
        ["hidden", b => { b.document.visibilityState = "hidden"; b.document.emit("visibilitychange"); }],
        ["disconnected", b => { b.video.isConnected = false; b.mutate(); }],
        ["disconnected", b => { b.document.modal = true; b.mutate(); }],
        ["timeout", b => b.advance(45_000)]
    ]) {
        const b = browser({ purpose: "package", expected: packageCode });
        await b.scanner.start(); await effect(b); await tick();
        assert.deepEqual(b.calls, [["OnScanAsync", "session-1", state, ""]]); assert.equal(b.stopCalls, 1); b.clean();
    }
});

test("포장 전표 결과가 microtask에 대기할 때 폐기해도 픽업을 전달하지 않는다", async () => {
    const detect = deferred(); const b = browser({ purpose: "package", expected: packageCode, detect: () => detect.promise });
    await b.scanner.start(); await b.advance(0);
    detect.resolve([{ format: "qr_code", rawValue: packageCode }]);
    await Promise.resolve();
    b.scanner.dispose(); await tick(); assert.equal(b.calls.length, 0); assert.equal(b.stopCalls, 1); b.clean();
});
