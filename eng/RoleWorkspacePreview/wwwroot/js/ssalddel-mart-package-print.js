// 격리 표본 전표의 인쇄 창 요청만 수행합니다. 실물 인쇄 성공을 판정하지 않습니다.
export function createPrinter() {
    let disposed = false, pending = null;
    function clear(result = "cancelled", expected = pending) {
        if (!pending || pending !== expected) return;
        const job = pending;
        pending = null;
        clearTimeout(job.timer);
        job.frame.onload = null;
        job.frame.contentWindow?.removeEventListener("afterprint", job.after);
        job.frame.remove();
        job.resolve(result);
    }
    function hidden() { if (document.visibilityState === "hidden") clear(); }
    function leaving() { clear(); }
    document.addEventListener("visibilitychange", hidden);
    window.addEventListener("pagehide", leaving);
    return {
        request(title, html) {
            if (disposed || pending || document.visibilityState === "hidden" || typeof title !== "string" ||
                typeof html !== "string" || html.length < 20 || html.length > 1000000) return Promise.resolve("unavailable");
            return new Promise(resolve => {
                const frame = document.createElement("iframe");
                frame.title = title;
                frame.setAttribute("aria-hidden", "true");
                frame.setAttribute("sandbox", "allow-modals allow-same-origin");
                frame.style.cssText = "position:fixed;left:-10000px;top:0;width:320px;height:600px;border:0";
                const job = { frame, resolve, timer: null, after: () => clear("requested", job) };
                pending = job;
                frame.onload = () => {
                    if (disposed || pending !== job || !frame.isConnected || document.visibilityState === "hidden") return;
                    try {
                        const target = frame.contentWindow;
                        if (!target?.print) { clear("unavailable", job); return; }
                        target.addEventListener("afterprint", job.after, { once: true });
                        target.focus();
                        target.print();
                        // 취소된 인쇄 창도 요청은 한 번 있었으므로 성공 출력이라고 표시하지 않습니다.
                        resolve("requested");
                        if (pending === job) { clearTimeout(job.timer); job.timer = setTimeout(() => clear("requested", job), 60000); }
                    } catch { clear("unavailable", job); }
                };
                job.timer = setTimeout(() => clear("unavailable", job), 10000);
                frame.srcdoc = html;
                try { document.body.appendChild(frame); } catch { clear("unavailable", job); }
            });
        },
        dispose() {
            if (disposed) return;
            disposed = true;
            clear();
            document.removeEventListener("visibilitychange", hidden);
            window.removeEventListener("pagehide", leaving);
        }
    };
}
