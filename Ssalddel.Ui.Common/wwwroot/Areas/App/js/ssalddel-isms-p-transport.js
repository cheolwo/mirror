export async function encryptJson(publicKeyPem, keyId, value, associatedData) {
    const algorithmCode = "RSA-OAEP-256+A256GCM";
    const json = typeof value === "string" ? value : JSON.stringify(value);
    const payloadBytes = new TextEncoder().encode(json);
    const aesKey = await crypto.subtle.generateKey(
        { name: "AES-GCM", length: 256 },
        true,
        ["encrypt"]);
    const nonce = crypto.getRandomValues(new Uint8Array(12));
    const aesOptions = { name: "AES-GCM", iv: nonce };

    if (associatedData) {
        aesOptions.additionalData = new TextEncoder().encode(associatedData);
    }

    const cipherBuffer = await crypto.subtle.encrypt(aesOptions, aesKey, payloadBytes);
    const rawAesKey = await crypto.subtle.exportKey("raw", aesKey);
    const publicKey = await importRsaPublicKey(publicKeyPem);
    const encryptedKey = await crypto.subtle.encrypt(
        { name: "RSA-OAEP" },
        publicKey,
        rawAesKey);

    return {
        keyId,
        algorithmCode,
        encryptedKeyBase64: arrayBufferToBase64(encryptedKey),
        nonceBase64: arrayBufferToBase64(nonce),
        cipherTextBase64: arrayBufferToBase64(cipherBuffer),
        associatedData: associatedData || null
    };
}

async function importRsaPublicKey(publicKeyPem) {
    const binaryDer = pemToArrayBuffer(publicKeyPem);
    return await crypto.subtle.importKey(
        "spki",
        binaryDer,
        { name: "RSA-OAEP", hash: "SHA-256" },
        false,
        ["encrypt"]);
}

function pemToArrayBuffer(pem) {
    const base64 = pem
        .replace(/-----BEGIN PUBLIC KEY-----/g, "")
        .replace(/-----END PUBLIC KEY-----/g, "")
        .replace(/\s/g, "");
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);

    for (let i = 0; i < binary.length; i += 1) {
        bytes[i] = binary.charCodeAt(i);
    }

    return bytes.buffer;
}

function arrayBufferToBase64(buffer) {
    const bytes = buffer instanceof Uint8Array
        ? buffer
        : new Uint8Array(buffer);
    let binary = "";

    for (let i = 0; i < bytes.byteLength; i += 1) {
        binary += String.fromCharCode(bytes[i]);
    }

    return btoa(binary);
}
