// One browser-origin lock covers every tab. A missing lock/storage never permits a POST.
function locked(key, operation) {
    if (!globalThis.navigator?.locks?.request)
        throw new Error("이 브라우저에서는 안전한 접수 보관을 사용할 수 없습니다. 등록은 보내지 않았습니다.");
    return navigator.locks.request(key, { mode: "exclusive" }, operation);
}

export function begin(key, candidateJson, draftKey, draftJson) {
    return locked(key, () => {
        const existing = localStorage.getItem(key);
        if (existing) return { json: existing, created: false };
        localStorage.setItem(draftKey, draftJson);
        localStorage.setItem(key, candidateJson);
        return { json: candidateJson, created: true };
    });
}

export function update(key, expectedId, candidateJson) {
    return locked(key, () => {
        const old = JSON.parse(localStorage.getItem(key) ?? "null");
        const next = JSON.parse(candidateJson);
        if (!old || old.requestId !== expectedId || next.requestId !== expectedId || old.owner !== next.owner)
            throw new Error("이전 접수 번호를 유지했습니다. 등록 결과를 다시 확인해 주세요.");
        // A late failed response must not erase a canonical result found in another tab.
        if (old.confirmedRequestId) next.confirmedRequestId = old.confirmedRequestId;
        next.createdAtUtc = old.createdAtUtc;
        const json = JSON.stringify(next);
        localStorage.setItem(key, json);
        return json;
    });
}

export function removeConfirmed(key, expectedId, confirmedId, draftKey) {
    return locked(key, () => {
        const old = JSON.parse(localStorage.getItem(key) ?? "null");
        if (!old || old.requestId !== expectedId || !confirmedId || old.confirmedRequestId !== confirmedId)
            throw new Error("확인되지 않은 접수를 닫을 수 없습니다.");
        if (draftKey) localStorage.removeItem(draftKey);
        localStorage.removeItem(key);
    });
}

export function saveDraft(pendingKey, draftKey, draftJson) {
    return locked(pendingKey, () => {
        if (localStorage.getItem(pendingKey))
            throw new Error("이전 접수의 초안을 유지했습니다. 등록 결과를 먼저 확인해 주세요.");
        localStorage.setItem(draftKey, draftJson);
    });
}
