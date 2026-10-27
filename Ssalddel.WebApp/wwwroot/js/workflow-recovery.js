// A non-extractable browser key protects data at rest. This is not an XSS or shared-device login boundary.
// No tokens, photographs, automatic requests or plain text localStorage copies are created here.
const databaseName = "ssalddel.workflow.protected.v1";
const retention = 72 * 60 * 60 * 1000;
const encoder = new TextEncoder();
const decoder = new TextDecoder();

function requestResult(request) {
    return new Promise((resolve, reject) => {
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(new Error("이전 요청의 보호 기록을 읽지 못했습니다."));
    });
}

async function database() {
    if (!globalThis.indexedDB || !globalThis.crypto?.subtle || !globalThis.navigator?.locks?.request)
        throw new Error("이 브라우저에서는 안전한 요청 복구를 사용할 수 없습니다.");
    const request = indexedDB.open(databaseName, 1);
    request.onupgradeneeded = () => {
        request.result.createObjectStore("keys");
        request.result.createObjectStore("records");
    };
    return requestResult(request);
}

async function get(db, store, key) {
    return requestResult(db.transaction(store).objectStore(store).get(key));
}
async function mutate(db, store, key, value, remove = false) {
    const transaction = db.transaction(store, "readwrite");
    const done = new Promise((resolve, reject) => {
        transaction.oncomplete = () => resolve();
        transaction.onerror = transaction.onabort = () => reject(new Error("요청 보호 기록을 저장하지 못했습니다."));
    });
    if (remove) transaction.objectStore(store).delete(key);
    else transaction.objectStore(store).put(value, key);
    await done;
}
async function encryptionKey(db) {
    return navigator.locks.request(databaseName + ".key", { mode: "exclusive" }, async () => {
        let key = await get(db, "keys", "aes-gcm");
        if (!key) {
            key = await crypto.subtle.generateKey({ name: "AES-GCM", length: 256 }, false, ["encrypt", "decrypt"]);
            await mutate(db, "keys", "aes-gcm", key);
        }
        return key;
    });
}
function validateKey(key) {
    if (!/^workflow-v1-[0-9a-f]{64}$/.test(key)) throw new Error("요청 보호 범위를 확인하지 못했습니다.");
}
function parse(json) {
    if (typeof json !== "string" || json.length > 140000) throw new Error("요청 보호 기록이 올바르지 않습니다.");
    const record = JSON.parse(json);
    if (!record || typeof record.owner !== "string" || typeof record.scope !== "string"
        || !/^[0-9a-f-]{36}$/i.test(record.requestId) || !Number.isFinite(Date.parse(record.createdAtUtc)))
        throw new Error("요청 보호 기록을 확인하지 못했습니다.");
    return record;
}
function expire(record, now) {
    const time = Date.parse(now);
    if (!Number.isFinite(time)) throw new Error("요청 보호 시간을 확인하지 못했습니다.");
    if (time - Date.parse(record.createdAtUtc) >= retention) record.payloadJson = null;
    return record;
}
async function decrypt(db, key) {
    const envelope = await get(db, "records", key);
    if (!envelope) return null;
    const secret = await get(db, "keys", "aes-gcm");
    if (!secret || envelope.version !== 1) throw new Error("이전 요청의 보호 키를 확인하지 못했습니다.");
    try {
        const plain = await crypto.subtle.decrypt({ name: "AES-GCM", iv: envelope.iv,
            additionalData: encoder.encode(key) }, secret, envelope.data);
        return parse(decoder.decode(plain));
    } catch { throw new Error("이전 요청의 보호 기록을 확인하지 못했습니다. 새 요청을 보내지 않습니다."); }
}
async function encrypt(db, key, record) {
    const secret = await encryptionKey(db);
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const data = await crypto.subtle.encrypt({ name: "AES-GCM", iv,
        additionalData: encoder.encode(key) }, secret, encoder.encode(JSON.stringify(record)));
    await mutate(db, "records", key, { version: 1, iv, data });
}
async function locked(key, operation) {
    validateKey(key);
    const db = await database();
    try { return await navigator.locks.request(databaseName + "." + key, { mode: "exclusive" }, () => operation(db)); }
    finally { db.close(); }
}
export async function read(key, now) {
    return locked(key, async db => {
        const record = await decrypt(db, key);
        if (!record) return null;
        const before = record.payloadJson;
        expire(record, now);
        if (before !== record.payloadJson) await encrypt(db, key, record);
        return JSON.stringify(record);
    });
}
export async function save(key, json, now) {
    const candidate = parse(json);
    return locked(key, async db => {
        const old = await decrypt(db, key);
        if (old) {
            if (old.requestId !== candidate.requestId || old.owner !== candidate.owner || old.scope !== candidate.scope)
                throw new Error("이전에 보낸 요청을 먼저 확인해 주세요.");
            candidate.createdAtUtc = old.createdAtUtc;
            if (old.payloadJson === null) candidate.payloadJson = null;
        }
        expire(candidate, now);
        await encrypt(db, key, candidate);
        return JSON.stringify(candidate);
    });
}
export async function remove(key, requestId) {
    return locked(key, async db => {
        const old = await decrypt(db, key);
        if (old?.requestId === requestId) await mutate(db, "records", key, null, true);
    });
}
