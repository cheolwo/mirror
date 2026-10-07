import { ensureGoogleMapsRuntime } from "./community-world-google-map.js";
const instances = new Map();
const epochs = new Map();
const failures = new Set();
let authFailures = 0;
const previousAuthFailure = globalThis.gm_authFailure;
globalThis.gm_authFailure = () => { authFailures++; failures.forEach(fail => fail("auth-failed")); previousAuthFailure?.(); };
async function bounded(operation) {
    let timer, fail;
    const unavailable = new Promise(resolve => { fail = resolve; failures.add(fail); timer = setTimeout(() => resolve("timeout"),15000); });
    try { return await Promise.race([operation,unavailable]); } finally { clearTimeout(timer); failures.delete(fail); }
}
async function waitForTiles(instance) {
    if (instance.tilesReady) return "ready";
    let listener;
    const loaded = new Promise(resolve => { listener = google.maps.event.addListenerOnce(instance.map,"tilesloaded",()=>{instance.tilesReady=true;resolve("ready");}); instance.cancelTiles=()=>resolve("cancelled"); });
    try { return await bounded(loaded); } finally { listener?.remove(); instance.cancelTiles=null; }
}
const valid = point => Number.isFinite(point?.latitude) && Number.isFinite(point?.longitude) && Math.abs(point.latitude) <= 90 && Math.abs(point.longitude) <= 180;
const latLng = point => ({ lat: point.latitude, lng: point.longitude });
const defaultPublicViewport = () => ({ center: { lat: 37.579, lng: 127.083 }, zoom: 12 });
function publicViewport(state) {
    const publicMarkers = (state.markers ?? []).filter(item => valid(item) && ["region", "public-data"].includes(item.kind));
    const marker = publicMarkers.find(item => item.id === state.selectedMarkerId)
        ?? publicMarkers.find(item => item.kind === "region") ?? publicMarkers[0];
    return marker ? { center: latLng(marker), zoom: marker.kind === "region" ? 13 : 7 } : defaultPublicViewport();
}
const privateScene = state => state.selectedMarkerId?.startsWith("delivery:")
    || (state.markers ?? []).some(item => item.id?.startsWith("delivery:") || ["pickup", "dropoff", "driver", "inactive"].includes(item.kind))
    || (state.routes ?? []).length > 0;
function resetPrivateCamera(instance) {
    const safe = instance.publicViewport ?? defaultPublicViewport();
    instance.map.setCenter(safe.center); instance.map.setZoom(safe.zoom);
    instance.initialized = false; instance.privateScene = false;
}
function clear(instance) {
    instance.markers.forEach(marker => { google.maps.event.clearInstanceListeners(marker); marker.setMap(null); });
    instance.lines.forEach(line => line.setMap(null)); instance.markers = []; instance.lines = [];
}
function observe(instance) {
    instance.cameraListener = instance.map.addListener("idle", () => {
        if (document.hidden || instances.get(instance.element.id) !== instance) return;
        const center = instance.map.getCenter(), zoom = instance.map.getZoom();
        if (!center || !Number.isFinite(zoom)) return;
        const latitude = center.lat(), longitude = center.lng();
        if (!valid({ latitude, longitude })) return;
        instance.reference?.invokeMethodAsync("CameraChanged", latitude, longitude, Math.max(3, Math.min(20, zoom)), instance.epoch).catch(() => {});
    });
    instance.visibilityChanged = () => {
        if (instances.get(instance.element.id) !== instance) return;
        if (document.hidden) {
            // 브라우저가 가려지는 즉시 개인 좌표를 제거하고, 복귀 때 현재 권한으로 재조회합니다.
            if (instance.privateScene) {
                instance.suspendedRevision = instance.revision;
                resetPrivateCamera(instance);
            }
            clear(instance); instance.revision = null; instance.cancelTiles?.();
            instance.reference?.invokeMethodAsync("MapSuspended", instance.lifecycle).catch(() => {});
        } else {
            instance.reference?.invokeMethodAsync("MapInvalidated", instance.lifecycle).catch(() => {});
        }
    };
    document.addEventListener("visibilitychange", instance.visibilityChanged);
}
function dispose(instance) {
    instance.cancelTiles?.(); clear(instance);
    document.removeEventListener("visibilitychange", instance.visibilityChanged);
    google.maps.event.clearInstanceListeners(instance.map);
    instance.reference = null; instance.element.replaceChildren();
}
export async function render(elementId, state, reference, runtime, allowedRouteSources, epoch, lifecycle) {
    if ((epochs.get(elementId) ?? epoch) > epoch) return "cancelled";
    epochs.set(elementId, epoch);
    const failuresBefore = authFailures;
    const status = await bounded(ensureGoogleMapsRuntime(runtime));
    if (epochs.get(elementId) !== epoch) return "cancelled";
    if (status !== "ready") return status;
    if (authFailures !== failuresBefore) return "auth-failed";
    const element = document.getElementById(elementId);
    if (!element) return "failed";
    const { Map: GoogleMap } = await google.maps.importLibrary("maps");
    await google.maps.importLibrary("marker");
    if (epochs.get(elementId) !== epoch) return "cancelled";
    let instance = instances.get(elementId);
    if (!instance || instance.element !== element) {
        if (instance) dispose(instance);
        instance = { element, map: new GoogleMap(element, { center: {lat:37.579,lng:127.083}, zoom:12, mapTypeId:"roadmap", minZoom:3, maxZoom:20,
            mapTypeControl:false, streetViewControl:false, fullscreenControl:false, clickableIcons:false, gestureHandling:"greedy", controlSize:32 }), markers:[], lines:[], initialized:false,tilesReady:false,revision:null,suspendedRevision:null,privateScene:false,publicViewport:defaultPublicViewport() };
        instances.set(elementId, instance);
        observe(instance);
    }
    instance.reference = reference;
    instance.epoch = epoch;
    instance.lifecycle = lifecycle;
    if (document.hidden) { clear(instance); instance.revision = null; return "cancelled"; }
    const containsPrivate = privateScene(state);
    // 가려지기 전 개인 장면의 늦은 재전달은 새 지도 조회 결과로 취급하지 않습니다.
    if (containsPrivate && instance.suspendedRevision !== null && state.revision <= instance.suspendedRevision) {
        clear(instance); resetPrivateCamera(instance); return "cancelled";
    }
    // 패널 문구나 입력 상태만 바뀔 때 동일한 지도 객체를 다시 만들지 않습니다.
    if (instance.revision === state.revision) return instance.tilesReady ? "ready" : await waitForTiles(instance);
    clear(instance);
    const bounds = new google.maps.LatLngBounds();
    const colors = {region:"#0f766e",pickup:"#f97316",dropoff:"#2563eb",driver:"#111827",inactive:"#6b7280","public-data":"#7c3aed"};
    for (const item of state.markers ?? []) {
        if (!valid(item)) continue;
        const marker = new google.maps.Marker({map:instance.map, position:latLng(item), title:item.label,
            zIndex:item.id===state.selectedMarkerId?300:item.kind==="inactive"?10:item.kind==="region"?100:200,
            label:item.kind === "region" ? {text:String(item.count ?? 0),color:"white",fontSize:"11px",fontWeight:"700"} : undefined,
            icon:{path:google.maps.SymbolPath.CIRCLE,scale:item.id===state.selectedMarkerId?13:11,fillColor:colors[item.kind] ?? colors.inactive,fillOpacity:1,strokeColor:"white",strokeWeight:2}});
        marker.addListener("click", () => {
            if (document.hidden || instances.get(elementId) !== instance || instance.revision !== state.revision) return;
            return instance.reference?.invokeMethodAsync("MapMarkerSelected",item.id,instance.epoch);
        });
        instance.markers.push(marker); bounds.extend(latLng(item));
    }
    for (const route of state.routes ?? []) {
        if (!allowedRouteSources.includes(route.sourceCode) || route.points?.length < 2 || !route.points.every(valid)) continue;
        const candidate = route.sourceCode === 'cargo-straight-candidate';
        instance.lines.push(new google.maps.Polyline({map:instance.map,path:route.points.map(latLng),strokeColor:route.color,
            strokeOpacity:candidate ? 0 : 0.85,strokeWeight:4,geodesic:false,
            ...(candidate ? {icons:[{icon:{path:'M 0,-1 0,1',strokeOpacity:0.85,scale:3},offset:'0',repeat:'16px'}]} : {})}));
        route.points.forEach(point => bounds.extend(latLng(point)));
    }
    if (!instance.initialized || !state.preserveViewport) {
        if (valid(state.viewport)) { instance.map.setCenter(latLng(state.viewport)); instance.map.setZoom(Math.max(3,Math.min(20,state.viewport.zoom))); }
        else if (!bounds.isEmpty()) { instance.map.fitBounds(bounds,36); google.maps.event.addListenerOnce(instance.map,"idle",()=>{ if(instance.map.getZoom()>15) instance.map.setZoom(15); }); }
    }
    instance.initialized = true;
    instance.revision = state.revision;
    instance.privateScene = containsPrivate;
    instance.publicViewport = publicViewport(state);
    const tileStatus = await waitForTiles(instance);
    return epochs.get(elementId) !== epoch ? "cancelled" : tileStatus;
}
export function hide(elementId, epoch) {
    epochs.set(elementId,epoch);
    const instance=instances.get(elementId);
    if (instance) { dispose(instance); instances.delete(elementId); }
}
