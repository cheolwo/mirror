export function current() {
    return new Promise(resolve => {
        if (!navigator.geolocation) { resolve(null); return; }
        navigator.geolocation.getCurrentPosition(p => resolve({ latitude: p.coords.latitude, longitude: p.coords.longitude,
            accuracyMeters: p.coords.accuracy, measuredAt: new Date(p.timestamp).toISOString() }),
            () => resolve(null), { enableHighAccuracy: true, timeout: 12000, maximumAge: 0 });
    });
}

export async function currentWithoutPermissionPrompt() {
    if (!navigator.geolocation || !navigator.permissions?.query) return null;
    try {
        const permission = await navigator.permissions.query({ name: "geolocation" });
        if (permission.state !== "granted") return null;
        return await current();
    } catch {
        // 권한 상태를 확인할 수 없으면 자동 조회를 건너뜁니다.
        return null;
    }
}
