export function current() {
    return new Promise(resolve => {
        if (!navigator.geolocation) { resolve(null); return; }
        navigator.geolocation.getCurrentPosition(p => resolve({ latitude: p.coords.latitude, longitude: p.coords.longitude,
            accuracyMeters: p.coords.accuracy, measuredAt: new Date(p.timestamp).toISOString() }),
            () => resolve(null), { enableHighAccuracy: true, timeout: 12000, maximumAge: 0 });
    });
}
