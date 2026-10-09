// 店の端末として置くための部品 (画面を消さない、全画面、新しいチケットの音)

let wakeLock = null;
let audio = null;
let listener = null;

// 画面の Wake Lock を取る。画面が隠れると外れるので、前に出るたびに取り直す
async function requestWakeLock() {
    if (!('wakeLock' in navigator) || (document.visibilityState !== 'visible') || (wakeLock !== null)) {
        return;
    }

    try {
        wakeLock = await navigator.wakeLock.request('screen');
        wakeLock.addEventListener('release', () => { wakeLock = null; });
    } catch {
        wakeLock = null;
    }
}

export function keepScreenOn() {
    requestWakeLock();
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
            requestWakeLock();
        }
    });
}

// キオスクのブラウザでない (URL の帯が出る) ときは、画面に触れたときに全画面にする
// 全画面は戻るや通知で外れるので、外れていたら次に触れたときに戻す (キオスクのブラウザでは何もしない)
// タッチは指を離したとき (pointerup) に利用者の操作とみなされるので、そこで全画面にする (pointerdown では断られる)
export function keepFullscreen() {
    document.addEventListener('pointerup', () => {
        if (document.fullscreenEnabled && (document.fullscreenElement === null)) {
            document.documentElement.requestFullscreen({ navigationUI: 'hide' }).catch(() => { });
        }
    }, true);
}

// 音はブラウザが許すまで (画面に触れるまで) 出せないので、触れたとき (pointerup) に鳴らせる状態にして知らせる
// 自動再生を許したキオスクのブラウザでは、触れなくても鳴らせる
export function prepareSound(dotnet) {
    listener = dotnet;
    audio = new AudioContext();
    if (audio.state === 'running') {
        listener.invokeMethodAsync('OnSoundReady');
        return;
    }

    const unlock = () => {
        audio.resume().then(() => {
            if (audio.state === 'running') {
                document.removeEventListener('pointerup', unlock, true);
                listener.invokeMethodAsync('OnSoundReady');
            }
        });
    };
    document.addEventListener('pointerup', unlock, true);
}

// 新しいチケットの知らせ (高さの違う 2 つの短い音)
export function chime() {
    if ((audio === null) || (audio.state !== 'running')) {
        return;
    }

    const start = audio.currentTime;
    [[880, 0], [1175, 0.25]].forEach(([frequency, offset]) => {
        const oscillator = audio.createOscillator();
        const gain = audio.createGain();
        oscillator.type = 'sine';
        oscillator.frequency.value = frequency;
        gain.gain.setValueAtTime(0.0001, start + offset);
        gain.gain.exponentialRampToValueAtTime(0.5, start + offset + 0.02);
        gain.gain.exponentialRampToValueAtTime(0.0001, start + offset + 0.2);
        oscillator.connect(gain).connect(audio.destination);
        oscillator.start(start + offset);
        oscillator.stop(start + offset + 0.22);
    });
}
