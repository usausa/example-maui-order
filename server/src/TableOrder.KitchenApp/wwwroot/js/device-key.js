// 端末の鍵 (P-256)。WebCrypto で取り出せない秘密鍵を作り、IndexedDB に鍵の組のまま持つ (秘密鍵の値はアプリにも渡らない)
// WebCrypto は HTTPS か localhost でしか使えない
// IndexedDB の操作は順に並べ、消したあとに読む操作が消す前の鍵を読まないようにする

const databaseName = 'tableorder';
const storeName = 'keys';
const keyName = 'device';
const algorithm = { name: 'ECDSA', namedCurve: 'P-256' };
const signAlgorithm = { name: 'ECDSA', hash: 'SHA-256' };

let queue = Promise.resolve();
let cached = null;

function enqueue(action) {
    const task = queue.then(action);
    queue = task.catch(() => { });
    return task;
}

function openDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(storeName);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function run(mode, action) {
    const database = await openDatabase();
    try {
        return await new Promise((resolve, reject) => {
            const transaction = database.transaction(storeName, mode);
            const request = action(transaction.objectStore(storeName));
            transaction.oncomplete = () => resolve(request.result);
            transaction.onerror = () => reject(transaction.error);
            transaction.onabort = () => reject(transaction.error);
        });
    } finally {
        database.close();
    }
}

// 鍵がなければ作って保存する
function loadKey() {
    return enqueue(async () => {
        if (cached !== null) {
            return cached;
        }

        let pair = await run('readonly', (store) => store.get(keyName));
        if (!pair) {
            pair = await crypto.subtle.generateKey(algorithm, false, ['sign', 'verify']);
            await run('readwrite', (store) => store.put(pair, keyName));
        }

        cached = pair;
        return pair;
    });
}

// 鍵を作れる環境か (安全な接続と、WebCrypto と IndexedDB)
export function isAvailable() {
    return window.isSecureContext && !!window.crypto && !!window.crypto.subtle && !!window.indexedDB;
}

// 公開鍵 (SubjectPublicKeyInfo)
export async function getPublicKey() {
    const pair = await loadKey();
    return new Uint8Array(await crypto.subtle.exportKey('spki', pair.publicKey));
}

// SHA-256 の ECDSA の署名 (r と s を 32 バイトずつ並べた形)
export async function sign(data) {
    const pair = await loadKey();
    return new Uint8Array(await crypto.subtle.sign(signAlgorithm, pair.privateKey, data));
}

// 鍵を消す (消す操作を並べて戻る。次に使うときに作り直す)
export function remove() {
    cached = null;
    enqueue(() => run('readwrite', (store) => store.delete(keyName)));
}
