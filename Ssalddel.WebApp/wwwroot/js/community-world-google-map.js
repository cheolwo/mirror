const instances = new Map();
const applicationInteractionInstances = new Map();
let googleMapsLoadPromise;
let transportSimulationModulePromise;

const dayMapStyle = [];
// 생활 지도는 이 파일의 기존 키·출처 확인과 로더만 공유합니다.
export async function ensureGoogleMapsRuntime(suppliedRuntimeConfig) {
    const runtimeConfig = suppliedRuntimeConfig ?? globalThis.ssalddelRuntimeConfig;
    if (!isRuntimeOriginAllowed(runtimeConfig)) return "blocked-origin";
    const loaded = Boolean(globalThis.google?.maps?.importLibrary);
    const apiKey = loaded ? "" : consumeRuntimeValue(runtimeConfig, "browserApiKey", "googleMapsBrowserApiKey");
    if (!loaded && !apiKey) return "unconfigured";
    try { await loadGoogleMaps(apiKey); return "ready"; } catch { return "failed"; }
}
const nightMapStyle = [
    { elementType: "geometry", stylers: [{ color: "#202536" }] },
    { elementType: "labels.icon", stylers: [{ visibility: "simplified" }, { saturation: -35 }, { lightness: -18 }] },
    { elementType: "labels.text.fill", stylers: [{ color: "#d8d8e8" }] },
    { elementType: "labels.text.stroke", stylers: [{ color: "#202536" }] },
    { featureType: "administrative.country", elementType: "geometry.stroke", stylers: [{ color: "#77709d" }] },
    { featureType: "administrative.province", elementType: "geometry.stroke", stylers: [{ color: "#4f5368" }] },
    { featureType: "landscape.natural", elementType: "geometry", stylers: [{ color: "#252b3b" }] },
    { featureType: "poi", elementType: "geometry", stylers: [{ color: "#282f40" }] },
    { featureType: "poi", elementType: "labels.text.fill", stylers: [{ color: "#aaa9c4" }] },
    { featureType: "road", elementType: "geometry", stylers: [{ color: "#353b4c" }] },
    { featureType: "road", elementType: "labels.text.fill", stylers: [{ color: "#aaa9ba" }] },
    { featureType: "transit", elementType: "geometry", stylers: [{ color: "#30364a" }] },
    { featureType: "water", elementType: "geometry", stylers: [{ color: "#17172f" }] }
];

export async function initialize(
    elementId,
    markers,
    datasetCode,
    selectedCode,
    selectedMarkerId,
    dotNetReference,
    suppliedRuntimeConfig) {
    const runtimeConfig = suppliedRuntimeConfig ?? globalThis.ssalddelRuntimeConfig;
    if (!isRuntimeOriginAllowed(runtimeConfig)) {
        return "blocked-origin";
    }

    const googleMapsAlreadyLoaded = Boolean(globalThis.google?.maps?.importLibrary);
    const apiKey = googleMapsAlreadyLoaded ? "" : consumeRuntimeValue(runtimeConfig, "browserApiKey", "googleMapsBrowserApiKey");
    if (!googleMapsAlreadyLoaded && !apiKey) {
        return "unconfigured";
    }

    const element = document.getElementById(elementId);
    if (!element) {
        return "failed";
    }

    try {
        await loadGoogleMaps(apiKey);
        const { Map: GoogleMap } = await google.maps.importLibrary("maps");
        const map = new GoogleMap(element, {
            center: { lat: 20, lng: 15 },
            zoom: 2,
            minZoom: 2,
            maxZoom: 14,
            mapTypeId: "roadmap",
            mapTypeControl: true,
            mapTypeControlOptions: {
                mapTypeIds: ["roadmap", "terrain", "satellite"],
                position: google.maps.ControlPosition.TOP_RIGHT,
                style: google.maps.MapTypeControlStyle.DROPDOWN_MENU
            },
            zoomControl: true,
            zoomControlOptions: {
                position: google.maps.ControlPosition.RIGHT_CENTER
            },
            scaleControl: true,
            streetViewControl: true,
            streetViewControlOptions: {
                position: google.maps.ControlPosition.RIGHT_CENTER
            },
            fullscreenControl: true,
            clickableIcons: false,
            keyboardShortcuts: true,
            gestureHandling: "greedy",
            controlSize: 32
        });

        const instance = {
            map,
            dotNetReference,
            selectedCode,
            selectedMarkerId,
            datasetCode,
            clickListener: map.data.addListener("click", event => {
                const markerId = event.feature.getProperty("markerId");
                if (instance.suppressClickMarkerId === markerId) {
                    globalThis.clearTimeout(instance.suppressClickTimer);
                    instance.suppressClickMarkerId = undefined;
                    instance.suppressClickTimer = undefined;
                    return;
                }

                const countryCode = event.feature.getProperty("code");
                if (countryCode && markerId && instance.dotNetReference) {
                    instance.dotNetReference.invokeMethodAsync("SelectMapFeatureFromGoogleMap", countryCode, markerId);
                }
            })
        };
        instance.contextMenuListener = map.data.addListener("contextmenu", event => {
            const countryCode = event.feature.getProperty("code");
            const markerId = event.feature.getProperty("markerId");
            if (!countryCode || !markerId || !instance.dotNetReference) {
                return;
            }

            event.domEvent?.preventDefault?.();
            event.stop?.();
            const clientX = Number.isFinite(event.domEvent?.clientX)
                ? event.domEvent.clientX
                : globalThis.innerWidth / 2;
            const clientY = Number.isFinite(event.domEvent?.clientY)
                ? event.domEvent.clientY
                : globalThis.innerHeight / 2;
            instance.dotNetReference.invokeMethodAsync(
                "OpenMapApplicationsFromGoogleMap",
                countryCode,
                markerId,
                clientX,
                clientY);
        });
        const cancelLongPress = () => {
            globalThis.clearTimeout(instance.longPressTimer);
            instance.longPressTimer = undefined;
        };
        instance.longPressStartListener = map.data.addListener("mousedown", event => {
            cancelLongPress();
            const countryCode = event.feature.getProperty("code");
            const markerId = event.feature.getProperty("markerId");
            if (!countryCode || !markerId || !instance.dotNetReference) {
                return;
            }

            instance.longPressTimer = globalThis.setTimeout(() => {
                instance.longPressTimer = undefined;
                instance.suppressClickMarkerId = markerId;
                globalThis.clearTimeout(instance.suppressClickTimer);
                instance.suppressClickTimer = globalThis.setTimeout(() => {
                    instance.suppressClickMarkerId = undefined;
                    instance.suppressClickTimer = undefined;
                }, 1000);
                const clientX = Number.isFinite(event.domEvent?.clientX)
                    ? event.domEvent.clientX
                    : globalThis.innerWidth / 2;
                const clientY = Number.isFinite(event.domEvent?.clientY)
                    ? event.domEvent.clientY
                    : globalThis.innerHeight / 2;
                instance.dotNetReference.invokeMethodAsync(
                    "OpenMapApplicationsFromGoogleMap",
                    countryCode,
                    markerId,
                    clientX,
                    clientY);
            }, 650);
        });
        instance.longPressEndListener = map.data.addListener("mouseup", cancelLongPress);
        instance.longPressOutListener = map.data.addListener("mouseout", cancelLongPress);
        instance.dragStartListener = map.addListener("dragstart", cancelLongPress);

        instances.set(elementId, instance);
        updateDataset(elementId, markers, datasetCode, selectedCode, selectedMarkerId, false);
        return "ready";
    } catch {
        return "failed";
    }
}

export function updateDataset(elementId, markers, datasetCode, selectedCode, selectedMarkerId, preserveViewport = false) {
    const instance = instances.get(elementId);
    if (!instance) {
        return;
    }

    instance.datasetCode = datasetCode;
    instance.selectedCode = selectedCode;
    instance.selectedMarkerId = selectedMarkerId;
    const oldFeatures = [];
    instance.map.data.forEach(feature => oldFeatures.push(feature));
    oldFeatures.forEach(feature => instance.map.data.remove(feature));

    const bounds = new google.maps.LatLngBounds();
    for (const marker of markers ?? []) {
        const position = { lat: marker.latitude, lng: marker.longitude };
        instance.map.data.add({
            id: marker.id ?? marker.code,
            geometry: new google.maps.Data.Point(position),
            properties: {
                code: marker.code,
                markerId: marker.id ?? marker.code,
                name: marker.name,
                dataLabel: marker.dataLabel,
                layerCode: marker.layerCode
            }
        });
        bounds.extend(position);
    }

    instance.map.setOptions({
        styles: datasetCode === "night-learning" ? nightMapStyle : dayMapStyle,
        backgroundColor: datasetCode === "night-learning" ? "#17172f" : "#eef6f3"
    });
    applyDataStyle(instance);

    if (!preserveViewport && !bounds.isEmpty()) {
        instance.map.fitBounds(bounds, mapViewportPadding(instance));
    }
}

export function updateSelection(elementId, selectedCode, selectedMarkerId) {
    const instance = instances.get(elementId);
    if (!instance) {
        return;
    }

    instance.selectedCode = selectedCode;
    instance.selectedMarkerId = selectedMarkerId;
    applyDataStyle(instance);
    focusSelection(instance);
}

export async function updateTransportSimulationLayer(elementId, scenarios, options) {
    const instance = instances.get(elementId);
    if (!instance) {
        return;
    }

    transportSimulationModulePromise ??= import("./transport-simulation-map-layer.js");
    const simulationModule = await transportSimulationModulePromise;
    if (!instance.transportSimulationLayer) {
        instance.transportSimulationLayer = simulationModule.createTransportSimulationLayer(
            instance.map,
            scenarios,
            options);
        return;
    }

    instance.transportSimulationLayer.update(scenarios, options);
}

export function prefersReducedMotion() {
    return globalThis.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches === true;
}

export function focusElement(elementId) {
    globalThis.requestAnimationFrame(() => {
        document.getElementById(elementId)?.focus({ preventScroll: true });
    });
}

export function enableApplicationInteractions(elementId, dotNetReference) {
    disableApplicationInteractions(elementId);
    const element = document.getElementById(elementId);
    if (!element) {
        return;
    }

    const interaction = {
        element,
        dotNetReference,
        longPressTimer: undefined,
        longPressTarget: undefined,
        suppressClickTarget: undefined
    };

    interaction.keydown = event => {
        const isApplicationMenuKey = (event.shiftKey && event.key === "F10")
            || event.key === "ContextMenu";
        if (!isApplicationMenuKey) {
            return;
        }

        event.preventDefault();
        const markerElement = event.target?.closest?.("[data-map-application-marker-id]");
        const rect = markerElement?.getBoundingClientRect?.() ?? event.target?.getBoundingClientRect?.();
        const clientX = rect ? rect.left + rect.width / 2 : globalThis.innerWidth / 2;
        const clientY = rect ? rect.top + rect.height / 2 : globalThis.innerHeight / 2;
        if (markerElement?.dataset.mapApplicationMarkerId && markerElement?.dataset.mapApplicationCountryCode) {
            // The fallback marker's Blazor key handler owns the invocation.
            // This capture listener only suppresses the browser context menu.
            return;
        }

        dotNetReference.invokeMethodAsync("OpenSelectedMapApplicationsFromKeyboard", clientX, clientY);
    };
    interaction.pointerdown = event => {
        const markerElement = event.target?.closest?.("[data-map-application-marker-id]");
        if (!markerElement || event.pointerType === "mouse") {
            return;
        }

        globalThis.clearTimeout(interaction.longPressTimer);
        interaction.longPressTarget = markerElement;
        interaction.longPressTimer = globalThis.setTimeout(() => {
            interaction.longPressTimer = undefined;
            interaction.suppressClickTarget = markerElement;
            globalThis.clearTimeout(interaction.suppressClickTimer);
            interaction.suppressClickTimer = globalThis.setTimeout(() => {
                interaction.suppressClickTarget = undefined;
                interaction.suppressClickTimer = undefined;
            }, 1000);
            const rect = markerElement.getBoundingClientRect();
            dotNetReference.invokeMethodAsync(
                "OpenMapApplicationsFromGoogleMap",
                markerElement.dataset.mapApplicationCountryCode,
                markerElement.dataset.mapApplicationMarkerId,
                rect.left + rect.width / 2,
                rect.top + rect.height / 2);
        }, 650);
    };
    interaction.cancelLongPress = () => {
        globalThis.clearTimeout(interaction.longPressTimer);
        interaction.longPressTimer = undefined;
        interaction.longPressTarget = undefined;
    };
    interaction.click = event => {
        const markerElement = event.target?.closest?.("[data-map-application-marker-id]");
        if (markerElement && markerElement === interaction.suppressClickTarget) {
            event.preventDefault();
            event.stopPropagation();
            interaction.suppressClickTarget = undefined;
        }
    };

    element.addEventListener("keydown", interaction.keydown);
    element.addEventListener("pointerdown", interaction.pointerdown);
    element.addEventListener("pointerup", interaction.cancelLongPress);
    element.addEventListener("pointercancel", interaction.cancelLongPress);
    element.addEventListener("pointermove", interaction.cancelLongPress);
    element.addEventListener("click", interaction.click, true);
    applicationInteractionInstances.set(elementId, interaction);
}

export function disableApplicationInteractions(elementId) {
    const interaction = applicationInteractionInstances.get(elementId);
    if (!interaction) {
        return;
    }

    globalThis.clearTimeout(interaction.longPressTimer);
    globalThis.clearTimeout(interaction.suppressClickTimer);
    interaction.element.removeEventListener("keydown", interaction.keydown);
    interaction.element.removeEventListener("pointerdown", interaction.pointerdown);
    interaction.element.removeEventListener("pointerup", interaction.cancelLongPress);
    interaction.element.removeEventListener("pointercancel", interaction.cancelLongPress);
    interaction.element.removeEventListener("pointermove", interaction.cancelLongPress);
    interaction.element.removeEventListener("click", interaction.click, true);
    applicationInteractionInstances.delete(elementId);
}

export function dispose(elementId) {
    const instance = instances.get(elementId);
    if (!instance) {
        return;
    }

    if (instance.clickListener) {
        instance.clickListener.remove();
    }
    instance.contextMenuListener?.remove();
    globalThis.clearTimeout(instance.longPressTimer);
    globalThis.clearTimeout(instance.suppressClickTimer);
    instance.longPressStartListener?.remove();
    instance.longPressEndListener?.remove();
    instance.longPressOutListener?.remove();
    instance.dragStartListener?.remove();
    instance.transportSimulationLayer?.dispose();
    google.maps.event.clearInstanceListeners(instance.map);
    instances.delete(elementId);
}

function applyDataStyle(instance) {
    const night = instance.datasetCode === "night-learning";
    instance.map.data.setStyle(feature => {
        const selected = instance.selectedMarkerId
            ? feature.getProperty("markerId") === instance.selectedMarkerId
            : feature.getProperty("code") === instance.selectedCode;
        const layerCode = feature.getProperty("layerCode");
        const layerStyle = markerStyleFor(layerCode, night);
        return {
            title: `${feature.getProperty("name")} · ${feature.getProperty("dataLabel")}`,
            icon: {
                path: layerStyle.path,
                scale: selected ? layerStyle.selectedScale : layerStyle.scale,
                fillColor: selected ? (night ? "#fff0a8" : "#176b4d") : layerStyle.color,
                fillOpacity: 1,
                strokeColor: "#ffffff",
                strokeOpacity: 1,
                strokeWeight: selected ? 4 : 3
            },
            zIndex: selected ? 2 : 1
        };
    });
}

function markerStyleFor(layerCode, night) {
    switch (layerCode) {
        case "regional-culture":
            return { color: "#176b4d", path: google.maps.SymbolPath.CIRCLE, scale: 8, selectedScale: 11 };
        case "public-price":
            return { color: "#ef8f3c", path: "M 0,-10 10,0 0,10 -10,0 z", scale: .82, selectedScale: 1.08 };
        case "wholesale-market":
            return { color: "#2f6fab", path: "M -8,-5 -5,-9 5,-9 8,-5 8,8 -8,8 z", scale: .72, selectedScale: .94 };
        case "traditional-market-hub":
            return { color: "#8a4b24", path: "M -9,-2 -6,-8 6,-8 9,-2 7,0 7,9 -7,9 -7,0 z", scale: .72, selectedScale: .96 };
        case "overseas-manufacturer":
            return { color: "#7b4ab0", path: "M -9,-2 -5,-2 -5,-9 -2,-9 -2,-2 2,-5 2,-1 8,-5 8,9 -9,9 z", scale: .72, selectedScale: .96 };
        case "gyeonggi-livestock-public-evidence":
            return { color: "#c87924", path: "M 0,-10 8,-5 8,5 0,10 -8,5 -8,-5 z", scale: .78, selectedScale: 1.02 };
        case "tourism-public-evidence":
            return { color: "#148a8a", path: "M 0,-10 C 5,-10 9,-6 9,-1 C 9,5 0,11 0,11 C 0,11 -9,5 -9,-1 C -9,-6 -5,-10 0,-10 z", scale: .72, selectedScale: .96 };
        case "online-price-public-evidence":
            return { color: "#d05c42", path: "M -10,-7 7,-7 10,-4 10,7 -10,7 z M -4,-7 -4,7", scale: .7, selectedScale: .94 };
        case "kosis-statistical-context":
            return { color: "#5d63b8", path: "M -10,8 -10,2 -5,2 -5,8 z M -3,8 -3,-4 2,-4 2,8 z M 4,8 4,-9 9,-9 9,8 z", scale: .72, selectedScale: .96 };
        case "news-publisher":
            return { color: "#334e68", path: "M -10,-8 10,-8 10,8 -10,8 z M -7,-4 0,-4 0,1 -7,1 z M 3,-4 7,-4 M 3,0 7,0 M -7,4 7,4", scale: .72, selectedScale: .96 };
        case "learning-channel":
            return { color: "#6750a4", path: google.maps.SymbolPath.CIRCLE, scale: 8, selectedScale: 11 };
        case "scripture-classics":
            return { color: "#b7791f", path: "M 0,-10 10,0 0,10 -10,0 z", scale: .82, selectedScale: 1.08 };
        default:
            return { color: night ? "#6750a4" : "#ef8f3c", path: google.maps.SymbolPath.CIRCLE, scale: 8, selectedScale: 11 };
    }
}

function focusSelection(instance) {
    const bounds = new google.maps.LatLngBounds();
    instance.map.data.forEach(feature => {
        if (instance.selectedMarkerId && feature.getProperty("markerId") !== instance.selectedMarkerId) {
            return;
        }

        if (!instance.selectedMarkerId
            && instance.selectedCode
            && feature.getProperty("code") !== instance.selectedCode) {
            return;
        }

        const point = feature.getGeometry();
        if (point instanceof google.maps.Data.Point) {
            bounds.extend(point.get());
        }
    });

    if (!bounds.isEmpty()) {
        instance.map.fitBounds(bounds, mapViewportPadding(instance));
    }
}

function mapViewportPadding(instance) {
    const compact = globalThis.matchMedia?.("(max-width: 560px)")?.matches;
    if (compact) {
        return { top: 170, right: 28, bottom: 150, left: 28 };
    }

    return {
        top: 72,
        right: instance.selectedCode ? 430 : 72,
        bottom: 112,
        left: 330
    };
}

function consumeRuntimeValue(runtimeConfig, primaryName, legacyName) {
    const configName = typeof runtimeConfig?.[primaryName] === "string" ? primaryName : legacyName;
    const runtimeValue = runtimeConfig?.[configName];
    if (typeof runtimeValue === "string" && runtimeValue.trim()) {
        const value = runtimeValue.trim();
        try {
            delete runtimeConfig[configName];
        } catch {
            try {
                runtimeConfig[configName] = "";
            } catch {
                // A frozen deployment config is still readable; do not fail map loading.
            }
        }
        return value;
    }

    return "";
}

function isRuntimeOriginAllowed(runtimeConfig) {
    const currentOrigin = globalThis.location?.origin;
    const hostname = globalThis.location?.hostname?.toLowerCase();
    const isLocalDevelopment = (hostname === "localhost" || hostname === "127.0.0.1" || hostname === "::1")
        && (globalThis.location?.protocol === "http:" || globalThis.location?.protocol === "https:");
    if (isLocalDevelopment) {
        return true;
    }

    if (globalThis.location?.protocol !== "https:") {
        return false;
    }

    const allowedOrigins = runtimeConfig?.allowedOrigins ?? runtimeConfig?.googleMapsAllowedOrigins;
    return typeof currentOrigin === "string"
        && Array.isArray(allowedOrigins)
        && allowedOrigins.some(origin => origin === currentOrigin);
}

function loadGoogleMaps(apiKey) {
    if (globalThis.google?.maps?.importLibrary) {
        return Promise.resolve();
    }
    if (googleMapsLoadPromise) {
        return googleMapsLoadPromise;
    }

    googleMapsLoadPromise = new Promise((resolve, reject) => {
        const callbackName = "__ssalddelCommunityGoogleMapsReady";
        globalThis[callbackName] = () => {
            delete globalThis[callbackName];
            script.remove();
            resolve();
        };

        const parameters = new URLSearchParams({
            key: apiKey,
            loading: "async",
            callback: callbackName,
            v: "weekly",
            language: "ko",
            region: "KR",
            auth_referrer_policy: "origin"
        });
        const script = document.createElement("script");
        script.async = true;
        script.referrerPolicy = "strict-origin-when-cross-origin";
        script.src = `https://maps.googleapis.com/maps/api/js?${parameters}`;
        script.onerror = () => {
            delete globalThis[callbackName];
            script.remove();
            googleMapsLoadPromise = undefined;
            reject(new Error("Google Maps JavaScript API를 불러오지 못했습니다."));
        };
        document.head.append(script);
    });

    return googleMapsLoadPromise;
}
