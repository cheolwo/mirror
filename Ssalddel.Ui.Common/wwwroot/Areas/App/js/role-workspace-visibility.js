const subscriptions = new Map();
let nextId = 0;

// 지도 설정 실패·목록 모드에서도 업무 화면의 표시 수명을 관찰합니다.
export function watch(reference) {
    const id = ++nextId;
    const changed = () => reference.invokeMethodAsync("SetWorkspaceVisibility", !document.hidden).catch(() => {});
    const hidden = () => reference.invokeMethodAsync("SetWorkspaceVisibility", false).catch(() => {});
    document.addEventListener("visibilitychange", changed);
    window.addEventListener("pagehide", hidden);
    window.addEventListener("pageshow", changed);
    subscriptions.set(id, { changed, hidden });
    if (document.hidden) hidden();
    return id;
}

export function stop(id) {
    const subscription = subscriptions.get(id);
    if (!subscription) return;
    document.removeEventListener("visibilitychange", subscription.changed);
    window.removeEventListener("pagehide", subscription.hidden);
    window.removeEventListener("pageshow", subscription.changed);
    subscriptions.delete(id);
}
