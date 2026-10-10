#!/usr/bin/env python3
# 端末アプリをエミュレータで動かして確かめるための adb の操作 (実機は使わない)
#
#   python emu.py avds                       作成済みの AVD の名前
#   python emu.py boot <avd> [--timeout 秒]  エミュレータを起動して、起動の完了まで待つ (その AVD が起動済みなら何もしない)
#   python emu.py poweroff                   エミュレータを止める
#   python emu.py devices                    機器の一覧 (エミュレータは AVD の名前つき) と、使うエミュレータ
#   python emu.py install [--release] [--embed]   ビルドしてエミュレータに入れて起動する (--embed は Device Owner の間に使う)
#   python emu.py launch | stop              アプリを起動する / 止める
#   python emu.py kill                       アプリのプロセスを止める (Device Owner のアプリは stop が効かない。Debug だけ)
#   python emu.py locale [<言語>]            アプリを止めて、アプリだけの言語を替える (例: ja-JP。省くと端末の言語に戻す。Android 13 以降)
#   python emu.py owner set|clear|status     アプリを Device Owner にする / 外す / 今の状態 (専用端末、Debug だけ外せる)
#   python emu.py emm set <apk> | config [key=value ...] | clear | status [--dpc パッケージ/受け口]
#                                            外部の EMM の代わりにする DPC を入れる / 管理対象の構成を配る (値を省くと消す) / 外す / 今の状態
#   python emu.py reboot [--timeout 秒]      エミュレータを再起動して、起動の完了まで待つ
#   python emu.py shot <file.png>            画面を撮る (タブレットは 1920x1200 か縦置きの 1200x1920、スマートフォンは 1080x2400)
#   python emu.py tap <x> <y>                撮った画像の座標をタップする
#   python emu.py text <ascii>               文字を入力する (英数字と記号だけ)
#   python emu.py key <BACK|ENTER|DEL|...> [--repeat N]   キーを送る (入力欄を消すときは DEL を繰り返す)
#   python emu.py swipe <x1> <y1> <x2> <y2> [ms]
#   python emu.py screen                     今の画面の遷移 (logcat の Navigated) を出す
#   python emu.py logcat [--lines N] [--grep 正規表現]
#   python emu.py pref get <key>             アプリの設定 (shared_prefs) の値を出す
#   python emu.py pref set <key> <value>     アプリの設定の文字列を書き換える (アプリを止めてから)
#   python emu.py browse <url>               既定のブラウザで Web アプリ (キッチン端末) を開く (localhost はこの PC の同じポートにつなぐ。同じタブを使い回す)
#   python emu.py browse --reload            ブラウザで開いている画面を読み込み直す (全画面の間も使える)
#   python emu.py browse --locale <言語>     ブラウザを止めて、ブラウザだけの言語を替える (例: ja-JP。default で端末の言語に戻す)
#   python emu.py browse --close             ブラウザを止めて、ポートのつなぎを外す
#
# 対象の端末アプリは --app で選ぶ (既定は table。例: python emu.py --app table install)
# 使う機器は --avd で選んだ AVD のエミュレータ、ANDROID_SERIAL (emulator- で始まるものだけ)、ただ 1 台動いているエミュレータの順に決める
# エミュレータが複数動いているとき (タブレットとスマートフォンを並べるとき) は --avd で選ぶ。adb と emulator の場所は ADB / EMULATOR で変えられる
import argparse
import os
import re
import shlex
import shutil
import subprocess
import sys
import time
import urllib.parse
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
FRAMEWORK = 'net10.0-android'

# 端末アプリ (名前: パッケージ名、プロジェクト、Device Owner の受け口)。モノレポに端末アプリを足したらここに足す
# 受け口のないアプリは None にし、Device Owner の操作をしない
APPS = {
    'table': ('tableorder.terminal.table', 'table/src/TableOrder.TableApp/TableOrder.TableApp.csproj', '.AdminReceiver'),
    'hall': ('tableorder.terminal.hall', 'hall/src/TableOrder.HallApp/TableOrder.HallApp.csproj', '.AdminReceiver'),
    'reception': ('tableorder.terminal.reception', 'reception/src/TableOrder.ReceptionApp/TableOrder.ReceptionApp.csproj', '.AdminReceiver'),
}
PACKAGE, PROJECT, ADMIN = APPS['table']

# Web アプリ (キッチン端末) を開いたタブを使い回すための、呼び出し元の名前
BROWSER_TAB = 'tableorder.emu'

# --avd で選んだ AVD の名前と、決めたエミュレータ (1 回の実行の中で使い回す)
AVD = None
SELECTED = None


def sdk_candidates(*parts):
    candidates = []
    for home in (os.environ.get('ANDROID_HOME'), os.environ.get('ANDROID_SDK_ROOT')):
        if home:
            candidates.append(str(Path(home).joinpath(*parts)))
    candidates += [
        str(Path('C:/Program Files (x86)/Android/android-sdk').joinpath(*parts)),
        str(Path(os.environ.get('LOCALAPPDATA', '')).joinpath('Android/Sdk', *parts)),
    ]
    return candidates


def find_tool(env, name, *parts):
    candidates = [os.environ.get(env), shutil.which(name)] + sdk_candidates(*parts)
    for candidate in candidates:
        if candidate and (shutil.which(candidate) or Path(candidate).exists()):
            return candidate
    sys.exit(f'{name} が見つかりません (環境変数 {env} で場所を指定する)')


ADB = find_tool('ADB', 'adb', 'platform-tools', 'adb.exe' if os.name == 'nt' else 'adb')


def devices():
    output = subprocess.run([ADB, 'devices'], capture_output=True, text=True, check=True).stdout
    return [line.split('\t') for line in output.splitlines()[1:] if '\t' in line]


def emulators():
    # 動いているエミュレータ (起動の途中を含む)
    return [serial for serial, _ in devices() if serial.startswith('emulator-')]


def avd_name(serial):
    # エミュレータのコンソールに AVD の名前を聞く (答えられないときは None)
    result = subprocess.run([ADB, '-s', serial, 'emu', 'avd', 'name'], capture_output=True, text=True, check=False)
    lines = [line.strip() for line in result.stdout.splitlines() if line.strip()]
    return lines[0] if (result.returncode == 0) and lines and (lines[0] != 'KO') else None


def candidates():
    # 使う候補のエミュレータ。--avd で選んだらその AVD のものだけにする
    if AVD:
        return [serial for serial in emulators() if avd_name(serial) == AVD]
    serial = os.environ.get('ANDROID_SERIAL')
    if serial:
        return [serial] if serial.startswith('emulator-') else []
    return [serial for serial, state in devices() if serial.startswith('emulator-') and state == 'device']


def running_emulator():
    found = candidates()
    if len(found) > 1:
        sys.exit('エミュレータが複数動いています。--avd <AVD の名前> で選んでください: ' + '、'.join(f'{x} ({avd_name(x)})' for x in found))
    return found[0] if found else None


def emulator_serial():
    # 実機に入れたり操作したりしないように、エミュレータ (emulator-) だけを選ぶ
    global SELECTED
    if SELECTED is not None:
        return SELECTED
    serial = os.environ.get('ANDROID_SERIAL')
    if serial and not serial.startswith('emulator-') and not AVD:
        sys.exit(f'ANDROID_SERIAL がエミュレータではありません: {serial}')
    serial = running_emulator()
    if serial is None:
        sys.exit(f'{AVD} のエミュレータが動いていません (emu.py boot {AVD} で起動する)' if AVD else '起動しているエミュレータがありません (emu.py boot <avd> で起動する)')
    SELECTED = serial
    return serial


def adb(*args, capture=False):
    command = [ADB, '-s', emulator_serial(), *args]
    if capture:
        return subprocess.run(command, capture_output=True, check=True).stdout
    return subprocess.run(command, check=True)


def shell(command, capture=True):
    output = adb('shell', command, capture=capture)
    return output.decode('utf-8', errors='replace') if capture else None


def pause(seconds=1.2):
    # 操作の結果が画面に出るまで待つ
    time.sleep(seconds)


#--------------------------------------------------------------------------------
# Emulator
#--------------------------------------------------------------------------------

def avds():
    emulator = find_tool('EMULATOR', 'emulator', 'emulator', 'emulator.exe' if os.name == 'nt' else 'emulator')
    output = subprocess.run([emulator, '-list-avds'], capture_output=True, text=True, check=True).stdout
    return [line.strip() for line in output.splitlines() if line.strip() and not line.startswith('INFO')]


def boot(avd, timeout):
    if avd not in avds():
        sys.exit(f'AVD がありません: {avd} (emu.py avds で名前を見る)')
    # ほかの AVD のエミュレータが動いていても、この AVD が動いていなければ起動する
    if not any(avd_name(serial) == avd for serial in emulators()):
        emulator = find_tool('EMULATOR', 'emulator', 'emulator', 'emulator.exe' if os.name == 'nt' else 'emulator')
        # このスクリプトが終わってもエミュレータは動き続けるように切り離して起動する
        flags = (subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP) if os.name == 'nt' else 0
        subprocess.Popen([emulator, '-avd', avd, '-no-boot-anim'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, stdin=subprocess.DEVNULL, creationflags=flags, start_new_session=os.name != 'nt')
        print(f'{avd} を起動しています', flush=True)
    return wait_boot(timeout, avd)


def reboot(timeout):
    # 電源を入れたときの動き (専用端末のホームアプリ、ロック画面) を確かめる
    serial = emulator_serial()
    avd = avd_name(serial)
    subprocess.run([ADB, '-s', serial, 'reboot'], check=True)
    subprocess.run([ADB, '-s', serial, 'wait-for-device'], check=True)
    return wait_boot(timeout, avd)


def poweroff():
    serial = emulator_serial()
    subprocess.run([ADB, '-s', serial, 'emu', 'kill'], capture_output=True, check=False)
    print(f'止めました: {serial}')


def wait_boot(timeout, avd):
    # その AVD のエミュレータが、起動の完了 (sys.boot_completed) を返すまで待つ
    deadline = time.time() + timeout
    while time.time() < deadline:
        for serial, state in devices():
            if serial.startswith('emulator-') and (state == 'device') and (avd_name(serial) == avd):
                completed = subprocess.run([ADB, '-s', serial, 'shell', 'getprop', 'sys.boot_completed'], capture_output=True, text=True, check=False).stdout.strip()
                if completed == '1':
                    print(f'起動しました: {serial} ({avd})')
                    return 0
        time.sleep(3)
    sys.exit(f'{timeout} 秒で起動が終わりませんでした')


#--------------------------------------------------------------------------------
# App
#--------------------------------------------------------------------------------

def install(release, embed):
    serial = emulator_serial()
    configuration = 'Release' if release else 'Debug'
    print(f'{serial} に {configuration} を入れて起動します', flush=True)
    command = ['dotnet', 'build', PROJECT, '-f', FRAMEWORK, '-c', configuration, '-t:Run', f'-p:AdbTarget=-s {serial}']
    # Debug の高速配置は入れたあとにアプリを止めて差し替えるが、Device Owner のアプリは止められない
    # そのままだと本体のない APK が残って起動できないので、本体を APK に入れる
    if embed:
        command.append('-p:EmbedAssembliesIntoApk=true')
    return subprocess.run(command, cwd=ROOT, check=False).returncode


def launch():
    shell(f'monkey -p {PACKAGE} -c android.intent.category.LAUNCHER 1')


def app_pid():
    # pidof は見つからないときに終了コード 1 を返すので、失敗として扱わない
    result = subprocess.run([ADB, '-s', emulator_serial(), 'shell', f'pidof {PACKAGE}'], capture_output=True, text=True, check=False)
    return result.stdout.strip()


def stop():
    shell(f'am force-stop {PACKAGE}')
    if app_pid():
        print('止まりませんでした (Device Owner のアプリは force-stop が効かないので emu.py kill を使う)')


def kill():
    # アプリの権限 (run-as) で自分のプロセスを止める。Debug (debuggable) のときだけ使える
    pid = app_pid()
    if pid:
        shell(f'run-as {PACKAGE} kill -9 {pid}')
    print(f'止めました: {pid}' if pid else '動いていません')


def locale(tag):
    # 端末の言語に従うアプリ (ホール端末) を、端末の言語を替えずにほかの言語で確かめる
    # アプリは起動のときに言語を読むので、止めてから替える (起動は emu.py launch)
    if tag and not re.fullmatch(r'[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*', tag):
        sys.exit(f'言語の名前が正しくありません: {tag} (例: ja-JP)')
    stop()
    shell(f"cmd locale set-app-locales {PACKAGE} --user 0 --locales '{tag or ''}'")
    print(shell(f'cmd locale get-app-locales {PACKAGE} --user 0').strip())


#--------------------------------------------------------------------------------
# Device owner
#--------------------------------------------------------------------------------

def owner(action):
    if ADMIN is None:
        sys.exit(f'{PACKAGE} は Device Owner の受け口を持っていません')
    component = f'{PACKAGE}/{ADMIN}'
    if action == 'set':
        # 端末にアカウントやほかの利用者があると設定できない (エミュレータはアカウントを足さずに使う)
        print(shell(f'dpm set-device-owner {component}').strip())
        print('アプリが前に出たとき (emu.py kill と launch) に端末の制限を掛ける。ホームアプリにするには emu.py reboot で再起動する')
    elif action == 'clear':
        # testOnly (Debug) のアプリだけ外せる。端末の制限とロックタスクは外れるが、次の 2 つは残るので戻す
        # - 画面を点けたままの設定 (グローバルの設定)
        # - ホームの役割 (常に使うホームにしたときにこのアプリに移る)。ホームの候補のうち、ほかのランチャーに戻す
        try:
            print(shell(f'dpm remove-active-admin {component}').strip())
        except subprocess.CalledProcessError:
            print('Device Owner ではありません (外れているときも、残った設定は戻す)')
        shell('settings put global stay_on_while_plugged_in 0')
        candidates = shell('cmd package query-activities --brief -a android.intent.action.MAIN -c android.intent.category.HOME')
        launchers = [line.strip().split('/')[0] for line in candidates.splitlines() if '/' in line]
        launchers = [name for name in launchers if name not in (PACKAGE, 'com.android.settings')]
        if launchers:
            shell(f'cmd role add-role-holder android.app.role.HOME {launchers[0]}')
            print(f'ホームを戻しました: {launchers[0]}')
    else:
        print(shell('dpm list-owners').strip())
        for line in shell('dumpsys activity activities').splitlines():
            if 'mLockTaskModeState' in line or 'topResumedActivity' in line:
                print(line.strip())
        home = shell('cmd package resolve-activity -a android.intent.action.MAIN -c android.intent.category.HOME')
        for line in home.splitlines():
            if line.strip().startswith('name='):
                print(f'home: {line.strip()[5:]}')
                break


#--------------------------------------------------------------------------------
# EMM
#--------------------------------------------------------------------------------

# 外部の EMM の代わりに、adb (dumpsys) から操作できる DPC を Device Owner にする
# DPC は dumpsys の引数で set-lock-task-packages、set-app-restrictions、clear-device-owner を受けるものを使う
def emm(action, dpc, values):
    if not dpc:
        sys.exit('DPC の受け口 (パッケージ/受け口) を --dpc か環境変数 EMU_DPC で渡してください')
    package = dpc.split('/')[0]

    def command(*args):
        # DPC のサービスが動いていないと dumpsys のコマンドが届かない (Device Owner にした直後など) ので、動くまで送り直す
        line = ' '.join(['dumpsys', 'activity', 'service', package, *(shlex.quote(x) for x in args)])
        for _ in range(15):
            output = shell(line).strip()
            if 'pid=(not running)' not in output:
                return output
            time.sleep(1)
        return output

    if action == 'set':
        if len(values) != 1:
            sys.exit('emm set には DPC の APK を 1 つ渡してください')
        # アプリ自身が Device Owner のときは先に外す (Device Owner は端末に 1 つだけ)
        adb('install', '-r', values[0])
        if package not in shell('dpm list-owners'):
            print(shell(f'dpm set-device-owner {dpc}').strip())
        print(command('set-lock-task-packages', PACKAGE))
        print('アプリが前に出たときに、EMM が許したロックタスクに入る (emu.py stop と launch)')
    elif action == 'config':
        # 値を並べないと、配った構成を消す
        print(command('set-app-restrictions', PACKAGE, *values))
    elif action == 'clear':
        print(command('set-app-restrictions', PACKAGE))
        print(command('set-lock-task-packages'))
        print(command('clear-device-owner'))
        adb('uninstall', package)
        print(f'外して消しました: {package}')
    else:
        print(shell('dpm list-owners').strip())
        print(command('is-lock-task-permitted', PACKAGE))
        for line in shell('dumpsys activity activities').splitlines():
            if 'mLockTaskModeState' in line:
                print(line.strip())


#--------------------------------------------------------------------------------
# Screen
#--------------------------------------------------------------------------------

def shot(file):
    data = adb('exec-out', 'screencap', '-p', capture=True)
    Path(file).write_bytes(data)
    print(file)


def tap(x, y):
    shell(f'input tap {x} {y}', capture=False)
    pause()


def text(value):
    if not re.fullmatch(r'[\x21-\x7e]+', value):
        sys.exit('text は空白のない ASCII だけを送れる (日本語は送れない)')
    # 端末のシェルで解釈される記号を逃がす
    escaped = re.sub(r'([\\\'"`$&|;<>()*?~#%])', r'\\\1', value)
    shell(f'input text {escaped}', capture=False)
    pause(0.6)


def logcat(lines, pattern):
    output = adb('logcat', '-d', '-t', str(lines), capture=True).decode('utf-8', errors='replace')
    for line in output.splitlines():
        if pattern is None or re.search(pattern, line):
            print(line)


#--------------------------------------------------------------------------------
# Browser
#--------------------------------------------------------------------------------

def browser_package():
    # http の URL を開く既定のブラウザ (パッケージの名前は書かずに、エミュレータに聞く)
    output = shell('cmd package resolve-activity --brief -a android.intent.action.VIEW -c android.intent.category.BROWSABLE -d http://localhost/')
    lines = [line.strip() for line in output.splitlines() if line.strip()]
    component = lines[-1] if lines else ''
    # 既定が決まっていないときは、選ぶ画面 (android の受け口) が返る
    package = component.split('/')[0] if '/' in component else ''
    if package in ('', 'android'):
        sys.exit('既定のブラウザが決まっていません (エミュレータの設定で既定のブラウザを選ぶ)')
    return package


def browse(url, reload, tag, close):
    browser = browser_package()
    if tag:
        # ブラウザは起動のときに言語を読むので、止めてから替える (Web アプリはブラウザの言語で文言を選ぶ)
        if not re.fullmatch(r'default|[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*', tag):
            sys.exit(f'言語の名前が正しくありません: {tag} (例: ja-JP。端末の言語に戻すときは default)')
        shell(f'am force-stop {browser}')
        shell(f"cmd locale set-app-locales {browser} --user 0 --locales '{'' if tag == 'default' else tag}'")
        print(shell(f'cmd locale get-app-locales {browser} --user 0').strip())
    if url:
        parsed = urllib.parse.urlsplit(url)
        if parsed.scheme not in ('http', 'https'):
            sys.exit(f'http か https の URL を渡してください: {url}')
        # WebCrypto は安全な接続元 (HTTPS か localhost) でしか使えないので、10.0.2.2 ではなく localhost で開き、
        # エミュレータの localhost のポートをこの PC の同じポートにつなぐ
        if (parsed.hostname in ('localhost', '127.0.0.1')) and parsed.port:
            adb('reverse', f'tcp:{parsed.port}', f'tcp:{parsed.port}', capture=True)
        # 開くたびにタブが増えると、どのタブも Web アプリを動かして重くなるので、呼び出し元の名前を付けて同じタブで開く
        shell(f'am start -a android.intent.action.VIEW -d {shlex.quote(url)} --es com.android.browser.application_id {BROWSER_TAB} {browser}')
        print(f'開きました: {url}')
    if reload:
        # 全画面では URL の帯 (読み込み直すボタン) が出ないので、Ctrl+R (CTRL_LEFT と R) を送る
        shell('input keycombination 113 46', capture=False)
        pause()
    if close:
        shell(f'am force-stop {browser}')
        adb('reverse', '--remove-all', capture=True)
        print('ブラウザを止めて、ポートのつなぎを外しました')


#--------------------------------------------------------------------------------
# Preferences
#--------------------------------------------------------------------------------

def preference_files():
    output = shell(f'run-as {PACKAGE} ls shared_prefs')
    return [name.strip() for name in output.split() if name.strip().endswith('.xml')]


def pref_get(key):
    for name in preference_files():
        content = shell(f'run-as {PACKAGE} cat shared_prefs/{name}')
        for match in re.finditer(r'<(\w+) name="' + re.escape(key) + r'"(?: value="([^"]*)")?\s*/?>(?:([^<]*)</\1>)?', content):
            print(f'{name}: {match.group(2) if match.group(2) is not None else match.group(3)}')


def pref_set(key, value):
    if re.search(r"['#<>&\\]", value) or re.search(r"['#<>&\\\"]", key):
        sys.exit('キーと値に使えない文字が入っています')
    # アプリが動いていると終了時に書き戻されるので、止めてから書き換える
    stop()
    changed = 0
    for name in preference_files():
        content = shell(f'run-as {PACKAGE} cat shared_prefs/{name}')
        if f'<string name="{key}">' in content:
            shell(f"run-as {PACKAGE} sed -i 's#<string name=\"{key}\">[^<]*</string>#<string name=\"{key}\">{value}</string>#' shared_prefs/{name}")
            changed += 1
    print(f'{key} を {changed} ファイルで書き換えました')
    pref_get(key)


#--------------------------------------------------------------------------------
# Main
#--------------------------------------------------------------------------------

def main():
    if not sys.stdout.isatty():
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    if not sys.stderr.isatty():
        sys.stderr.reconfigure(encoding='utf-8', errors='replace')
    parser = argparse.ArgumentParser(description='端末アプリをエミュレータで確かめるための adb の操作')
    parser.add_argument('--app', choices=sorted(APPS), default='table', help='対象の端末アプリ (既定は table)')
    parser.add_argument('--avd', default=os.environ.get('EMU_AVD'), help='使うエミュレータの AVD の名前 (エミュレータが複数動いているときに選ぶ。既定は環境変数 EMU_AVD)')
    sub = parser.add_subparsers(dest='command', required=True)
    sub.add_parser('avds')
    p = sub.add_parser('boot')
    p.add_argument('avd')
    p.add_argument('--timeout', type=int, default=300)
    sub.add_parser('devices')
    sub.add_parser('poweroff')
    p = sub.add_parser('install')
    p.add_argument('--release', action='store_true')
    p.add_argument('--embed', action='store_true')
    sub.add_parser('launch')
    sub.add_parser('stop')
    sub.add_parser('kill')
    p = sub.add_parser('locale')
    p.add_argument('tag', nargs='?')
    p = sub.add_parser('owner')
    p.add_argument('action', choices=['set', 'clear', 'status'])
    p = sub.add_parser('emm')
    p.add_argument('action', choices=['set', 'config', 'clear', 'status'])
    p.add_argument('values', nargs='*', help='set は DPC の APK、config は key=value')
    p.add_argument('--dpc', default=os.environ.get('EMU_DPC'), help='DPC の受け口 (パッケージ/受け口。既定は環境変数 EMU_DPC)')
    p = sub.add_parser('reboot')
    p.add_argument('--timeout', type=int, default=300)
    p = sub.add_parser('shot')
    p.add_argument('file')
    p = sub.add_parser('tap')
    p.add_argument('x', type=int)
    p.add_argument('y', type=int)
    p = sub.add_parser('text')
    p.add_argument('value')
    p = sub.add_parser('key')
    p.add_argument('name')
    p.add_argument('--repeat', type=int, default=1)
    p = sub.add_parser('swipe')
    p.add_argument('coords', type=int, nargs=4)
    p.add_argument('ms', type=int, nargs='?', default=300)
    sub.add_parser('screen')
    p = sub.add_parser('logcat')
    p.add_argument('--lines', type=int, default=200)
    p.add_argument('--grep')
    p = sub.add_parser('pref')
    p.add_argument('action', choices=['get', 'set'])
    p.add_argument('key')
    p.add_argument('value', nargs='?')
    p = sub.add_parser('browse')
    p.add_argument('url', nargs='?')
    p.add_argument('--reload', action='store_true')
    p.add_argument('--locale', dest='tag')
    p.add_argument('--close', action='store_true')
    args = parser.parse_args()

    global PACKAGE, PROJECT, ADMIN, AVD
    PACKAGE, PROJECT, ADMIN = APPS[args.app]
    AVD = args.avd

    if args.command == 'avds':
        for name in avds():
            print(name)
    elif args.command == 'boot':
        return boot(args.avd, args.timeout)
    elif args.command == 'devices':
        for serial, state in devices():
            print(f'{serial}\t{state}\t{(avd_name(serial) or "") if serial.startswith("emulator-") else "(使わない)"}')
        found = candidates()
        print(f'使うエミュレータ: {found[0] if len(found) == 1 else ("(--avd で選ぶ)" if found else "(なし)")}')
    elif args.command == 'poweroff':
        poweroff()
    elif args.command == 'install':
        return install(args.release, args.embed)
    elif args.command == 'launch':
        launch()
    elif args.command == 'stop':
        stop()
    elif args.command == 'kill':
        kill()
    elif args.command == 'locale':
        locale(args.tag)
    elif args.command == 'owner':
        owner(args.action)
    elif args.command == 'emm':
        emm(args.action, args.dpc, args.values)
    elif args.command == 'reboot':
        return reboot(args.timeout)
    elif args.command == 'shot':
        shot(args.file)
    elif args.command == 'tap':
        tap(args.x, args.y)
    elif args.command == 'text':
        text(args.value)
    elif args.command == 'key':
        code = args.name if args.name.isdigit() else 'KEYCODE_' + args.name.upper()
        shell('input keyevent ' + ' '.join([code] * max(1, args.repeat)), capture=False)
        pause(0.6)
    elif args.command == 'swipe':
        x1, y1, x2, y2 = args.coords
        shell(f'input swipe {x1} {y1} {x2} {y2} {args.ms}', capture=False)
        pause()
    elif args.command == 'screen':
        logcat(500, r'Navigated:')
    elif args.command == 'logcat':
        logcat(args.lines, args.grep)
    elif args.command == 'pref':
        if args.action == 'get':
            pref_get(args.key)
        else:
            if args.value is None:
                sys.exit('pref set には値が要る')
            pref_set(args.key, args.value)
    elif args.command == 'browse':
        if not (args.url or args.reload or args.tag or args.close):
            sys.exit('browse には URL か --reload / --locale / --close が要る')
        browse(args.url, args.reload, args.tag, args.close)
    return 0


if __name__ == '__main__':
    sys.exit(main())
