// Development only: stands in for account.js, the way Azurite stands in for Blob Storage, so
// signed-in screens and real syncs can be tried without Clerk. It signs the API's tokens itself
// with the key from wwwroot/appsettings.Development.json. The local API has the same key, and the
// hosted API never accepts these tokens. AccountService uses this file in place of account.js
// while a test person is signed in, so it has the same functions.

const ISSUER = "stikling-test-sign-in"; // TestSignIn.Issuer in the API

let person = null; // { name, key } while signed in
let app = null; // the AccountService that is told about changes

function state() {
    return {
        signedIn: !!person,
        email: person ? `${person.name}@test.localhost` : null,
    };
}

export async function load(name, signingKey, dotnet) {
    app = dotnet;
    const raw = Uint8Array.from(atob(signingKey), c => c.charCodeAt(0));
    const key = await crypto.subtle.importKey("raw", raw, { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
    person = { name, key };
    return state();
}

// There is no sign-in to show. The sign-in page has its own button for a test person.
export function mountSignIn() { }

export function unmountSignIn() { }

async function end() {
    person = null;
    await app?.invokeMethodAsync("OnChanged", state());
    return state();
}

// The app stays on the page it is on, as it does after Clerk signs out
export function signOut() {
    return end();
}

// The API's side of the account was deleted already, and there is nothing else to delete
export function deleteUser() {
    return end();
}

export function profile() {
    if (!person) return null;
    return { id: `test_${person.name}`, username: person.name, primaryEmailAddress: state().email };
}

// A token shaped like Clerk's session tokens, and like them valid for a minute
export async function getToken() {
    if (!person) return null;

    const now = Math.floor(Date.now() / 1000);
    const header = { alg: "HS256", typ: "JWT" };
    const claims = { iss: ISSUER, sub: `test_${person.name}`, azp: location.origin, iat: now, nbf: now, exp: now + 60 };
    const unsigned = `${encode(header)}.${encode(claims)}`;
    const signature = await crypto.subtle.sign("HMAC", person.key, new TextEncoder().encode(unsigned));
    return `${unsigned}.${base64Url(new Uint8Array(signature))}`;
}

function encode(json) {
    return base64Url(new TextEncoder().encode(JSON.stringify(json)));
}

function base64Url(bytes) {
    return btoa(String.fromCharCode(...bytes)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

export function forget() {
    app = null;
}
