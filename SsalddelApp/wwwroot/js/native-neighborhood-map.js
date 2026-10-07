const watchers = new Map();
const shown = element => element.getClientRects().length > 0
    && !['hidden', 'collapse'].includes(getComputedStyle(element).visibility)
    && getComputedStyle(element).display !== 'none';
const textInputFocused = () => {
    const active = document.activeElement;
    return active?.isContentEditable || active?.matches('textarea, input:not([type="button"]):not([type="submit"]):not([type="checkbox"]):not([type="radio"]):not([type="range"]):not([type="hidden"])');
};
function bounds(id) {
    const element = document.getElementById(id);
    if (!element || !element.isConnected) return { x: 0, y: 0, width: 0, height: 0, viewportWidth: innerWidth, visible: false };
    const rect = element.getBoundingClientRect();
    const viewport = window.visualViewport;
    const originX = viewport?.offsetLeft ?? 0, originY = viewport?.offsetTop ?? 0;
    const viewportWidth = viewport?.width ?? innerWidth, viewportHeight = viewport?.height ?? innerHeight;
    const top = Math.max(originY, rect.top), left = Math.max(originX, rect.left);
    const right = Math.min(originX + viewportWidth, rect.right), bottom = Math.min(originY + viewportHeight, rect.bottom);
    const width = Math.max(0, right - left), height = Math.max(0, bottom - top);
    const overlaps = other => {
        if (!shown(other)) return false;
        const obstacle = other.getBoundingClientRect();
        return obstacle.left < right && obstacle.right > left && obstacle.top < bottom && obstacle.bottom > top;
    };
    // 실제 네이티브 지도는 HTML z-index보다 위에 있으므로 겹치는 대화상자/패널과 입력 시 숨깁니다.
    const occluded = [...document.querySelectorAll('[data-neighborhood-map-occlusion], [role="dialog"], .mud-dialog-container, .community-mobile-shell__drawer, .community-mobile-shell__backdrop')].some(overlaps);
    return { x: left - originX, y: top - originY, width, height, viewportWidth,
        visible: !document.hidden && !textInputFocused() && !occluded && width > 0 && height > 0 && shown(element) };
}
export function watch(id, reference) {
    unwatch(id);
    let frame, previous = '';
    const update = () => {
        cancelAnimationFrame(frame);
        frame = requestAnimationFrame(() => {
            const value = bounds(id), serialized = JSON.stringify(value);
            if (serialized === previous) return;
            previous = serialized;
            reference.invokeMethodAsync('UpdateBounds', value).catch(() => unwatch(id));
        });
    };
    const resize = new ResizeObserver(update);
    const element = document.getElementById(id);
    if (element) resize.observe(element);
    const mutation = new MutationObserver(update);
    mutation.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class', 'style', 'hidden', 'open'] });
    window.addEventListener('resize', update);
    window.addEventListener('scroll', update, true);
    window.visualViewport?.addEventListener('resize', update);
    window.visualViewport?.addEventListener('scroll', update);
    document.addEventListener('focusin', update);
    document.addEventListener('focusout', update);
    document.addEventListener('visibilitychange', update);
    watchers.set(id, () => {
        cancelAnimationFrame(frame); resize.disconnect(); mutation.disconnect();
        window.removeEventListener('resize', update); window.removeEventListener('scroll', update, true);
        window.visualViewport?.removeEventListener('resize', update); window.visualViewport?.removeEventListener('scroll', update);
        document.removeEventListener('focusin', update); document.removeEventListener('focusout', update); document.removeEventListener('visibilitychange', update);
    });
    return bounds(id);
}
export function unwatch(id) { watchers.get(id)?.(); watchers.delete(id); }
