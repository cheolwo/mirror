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

// DOM 존재만으로 표시를 인증하지 않습니다. 현재 선택 제안이 전경 viewport와 겹칠 때만 확인합니다.
export function isElementInViewport(id) {
    if (document.hidden) return false;
    const element = document.getElementById(id);
    if (!element || element.checkVisibility?.({ checkOpacity: true, checkVisibilityCSS: true }) === false) return false;
    const style = window.getComputedStyle(element);
    if (style.display === "none" || style.visibility === "hidden" || style.opacity === "0") return false;
    const rect = element.getBoundingClientRect();
    return rect.width > 0 && rect.height > 0 && rect.bottom > 0 && rect.right > 0
        && rect.top < window.innerHeight && rect.left < window.innerWidth;
}
