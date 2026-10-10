// 로컬 표본의 현재 선반 또는 포장 코드만 읽습니다. 영상·오인식 원문을 전송하거나 저장하지 않습니다.
const rackProfile = { formats: ["qr_code", "code_128"], pattern: /^SAMPLE-RACK-[0-9]{3}$/, minimumLength: 15, maximumLength: 15 };
const packageProfile = { formats: ["qr_code"], pattern: /^SAMPLE-PKG-[0-9a-f]{32}-[1-9][0-9]{0,5}$/, minimumLength: 45, maximumLength: 50 };
const timeoutMilliseconds = 45_000;
const scanIntervalMilliseconds = 200;

export function createScanner(video, dotnet, sessionToken, expectedCode, purpose = "rack") {
    const profile = purpose === "rack" ? rackProfile : purpose === "package" ? packageProfile : null;
    let generation = 0;
    let activeRun = null;
    let pendingStart = null;
    let disposed = false;

    function allowedCode(code) {
        if (!profile || typeof code !== "string" || code.length < profile.minimumLength || code.length > profile.maximumLength) return false;
        // JavaScript의 $가 허용하는 마지막 개행도 실제 전표 값으로 받아들이지 않습니다.
        return profile.pattern.exec(code)?.[0] === code;
    }

    function current(run) {
        return !disposed && activeRun === run && generation === run.generation && !run.finished;
    }

    function stopStream(stream) {
        if (!stream) return;
        try {
            for (const track of stream.getTracks()) {
                try { track.stop(); } catch { /* 다른 트랙도 반드시 해제합니다. */ }
            }
        } catch { /* 이미 폐기된 장치는 다시 사용하지 않습니다. */ }
    }

    function clearRun(run) {
        if (!run || run.finished) return;
        run.finished = true;
        clearTimeout(run.timeout);
        clearTimeout(run.scanTimer);
        for (const [target, name, handler] of run.listeners) {
            try { target.removeEventListener(name, handler); } catch { /* 폐기 중인 DOM은 무시합니다. */ }
        }
        run.listeners.length = 0;
        run.observer?.disconnect();
        run.observer = null;
        if (run.attached && video.srcObject === run.stream) {
            try { video.pause(); } catch { /* 정리 실패로 장치 해제를 건너뛰지 않습니다. */ }
            try { video.srcObject = null; } catch { /* 트랙은 아래에서 독립적으로 해제합니다. */ }
        }
        stopStream(run.stream);
        run.stream = null;
        run.cancel({ cancelled: true });
        if (activeRun === run) activeRun = null;
    }

    function finish(run, stateCode, rawCode = "") {
        if (!current(run)) return;
        clearRun(run);
        // 새 요청·폐기 뒤에는 이전 세션의 결과를 전달하지 않습니다.
        Promise.resolve().then(() => {
            if (disposed || generation !== run.generation) return;
            return dotnet.invokeMethodAsync("OnScanAsync", sessionToken, stateCode, rawCode);
        }).catch(() => { clearRun(run); });
    }

    function listen(run, target, name, handler) {
        target.addEventListener(name, handler);
        run.listeners.push([target, name, handler]);
    }

    function disconnected() {
        if (!video.isConnected || navigator.onLine === false) return true;
        return Boolean(document.querySelector(
            ".components-reconnect-show, .components-reconnect-failed, .components-reconnect-rejected, #components-reconnect-modal[open]"));
    }

    function checkLife(run) {
        if (!current(run)) return false;
        if (document.visibilityState === "hidden") {
            finish(run, "hidden");
            return false;
        }
        if (disconnected()) {
            finish(run, "disconnected");
            return false;
        }
        return true;
    }

    function armLife(run) {
        listen(run, document, "visibilitychange", () => checkLife(run));
        listen(run, window, "pagehide", () => finish(run, "disconnected"));
        listen(run, window, "offline", () => finish(run, "disconnected"));
        if (typeof MutationObserver === "function") {
            run.observer = new MutationObserver(() => checkLife(run));
            run.observer.observe(document.documentElement, {
                subtree: true, childList: true, attributes: true,
                attributeFilter: ["class", "style", "hidden", "open"]
            });
        }
        run.timeout = setTimeout(() => finish(run, "timeout"), timeoutMilliseconds);
    }

    async function awaitCurrent(run, promise) {
        const result = await Promise.race([
            Promise.resolve(promise).then(value => ({ value }), error => ({ error })),
            run.cancelled
        ]);
        if (result.cancelled || !current(run)) return { cancelled: true };
        return result;
    }

    function startFailure(run, code) {
        clearRun(run);
        return code;
    }

    async function scan(run) {
        if (!checkLife(run)) return;
        if (video.readyState < 2) {
            run.scanTimer = setTimeout(() => { void scan(run); }, scanIntervalMilliseconds);
            return;
        }
        try {
            const results = await run.detector.detect(video);
            if (!checkLife(run)) return;
            if (!Array.isArray(results)) {
                finish(run, "failed");
                return;
            }
            if (results.length > 0) {
                // 혼합·모호한 인식도 허용하지 않고 현재 작업의 정확한 값만 전달합니다.
                const matched = results.every(result => result && profile.formats.includes(result.format)
                    && allowedCode(result.rawValue) && result.rawValue === expectedCode);
                finish(run, matched ? "matched" : "wrong-code", matched ? expectedCode : "");
                return;
            }
        } catch {
            if (current(run)) finish(run, "failed");
            return;
        }
        if (current(run)) run.scanTimer = setTimeout(() => { void scan(run); }, scanIntervalMilliseconds);
    }

    async function startRun(run) {
        if (globalThis.isSecureContext !== true) return startFailure(run, "insecure");
        if (!video || typeof dotnet?.invokeMethodAsync !== "function" || !allowedCode(expectedCode)
            || typeof globalThis.BarcodeDetector !== "function"
            || typeof globalThis.BarcodeDetector.getSupportedFormats !== "function"
            || typeof navigator.mediaDevices?.getUserMedia !== "function") return startFailure(run, "unsupported");
        if (document.visibilityState === "hidden" || disconnected()) return startFailure(run, "cancelled");
        try {
            armLife(run);
            const formatsResult = await awaitCurrent(run, BarcodeDetector.getSupportedFormats());
            if (formatsResult.cancelled || !current(run)) return "cancelled";
            if (formatsResult.error || !Array.isArray(formatsResult.value)) return startFailure(run, "failed");
            const formats = profile.formats.filter(format => formatsResult.value.includes(format));
            if (formats.length === 0) return startFailure(run, "unsupported");
            run.detector = new BarcodeDetector({ formats });
            const permission = Promise.resolve(navigator.mediaDevices.getUserMedia({
                audio: false,
                video: { facingMode: { ideal: "environment" }, width: { ideal: 1280 }, height: { ideal: 720 } }
            })).then(stream => {
                if (!current(run)) { stopStream(stream); return null; }
                // 권한 반환과 await 재개 사이에 닫혀도 이 스트림을 놓치지 않습니다.
                run.stream = stream;
                return stream;
            });
            const mediaResult = await awaitCurrent(run, permission);
            if (mediaResult.cancelled || !current(run)) return "cancelled";
            if (mediaResult.error) {
                const name = mediaResult.error?.name;
                return startFailure(run, name === "NotAllowedError" || name === "SecurityError" ? "permission-denied"
                    : ["NotFoundError", "NotReadableError", "OverconstrainedError"].includes(name) ? "camera-unavailable" : "failed");
            }
            run.stream = mediaResult.value;
            if (!run.stream || typeof run.stream.getTracks !== "function") return startFailure(run, "failed");
            for (const track of run.stream.getTracks()) listen(run, track, "ended", () => finish(run, "failed"));
            video.srcObject = run.stream;
            run.attached = true;
            const playResult = await awaitCurrent(run, video.play());
            if (playResult.cancelled || !current(run)) return "cancelled";
            if (playResult.error) return startFailure(run, "failed");
            if (!checkLife(run)) return "cancelled";
            run.scanTimer = setTimeout(() => { void scan(run); }, 0);
            return "scanning";
        } catch {
            return current(run) ? startFailure(run, "failed") : "cancelled";
        }
    }

    function start() {
        if (disposed) return Promise.resolve("cancelled");
        if (pendingStart) return pendingStart;
        if (activeRun && !activeRun.finished) return Promise.resolve("scanning");
        const run = { generation: ++generation, finished: false, stream: null, attached: false,
            listeners: [], observer: null, timeout: null, scanTimer: null, detector: null };
        run.cancelled = new Promise(resolve => { run.cancel = resolve; });
        activeRun = run;
        const promise = startRun(run).finally(() => { if (pendingStart === promise) pendingStart = null; });
        pendingStart = promise;
        return promise;
    }

    function stop() {
        ++generation;
        clearRun(activeRun);
        pendingStart = null;
    }

    function dispose() {
        if (disposed) return;
        stop();
        disposed = true;
    }

    return { start, stop, dispose };
}
