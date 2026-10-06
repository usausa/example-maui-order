---
name: emulator
description: 端末アプリ (MAUI Android、タブレット横向き) をエミュレータに入れて動作を確かめる。エミュレータの起動、ビルドと配置、画面の撮影・タップ・文字とキーの入力、遷移のログ、アプリの設定の読み書きを行う。画面や動きを変えたときに使う。実機には入れない。
---

# エミュレータでの確認

操作はリポジトリのルートで `python .claude/skills/emulator/scripts/emu.py <コマンド>` を使う (Windows では `python`、他では `python3`)。
対象の端末アプリは `--app` で選ぶ (既定は `table`。例: `emu.py --app table install`)。
端末アプリを足したら、スクリプトの `APPS` にパッケージ名とプロジェクトを足す。
スクリプトはエミュレータ (`emulator-` で始まる機器) だけを選び、実機が接続されていても使わない。
`adb` や `dotnet build -t:Run` を直接使うときも、必ず `-s emulator-xxxx` / `-p:AdbTarget=-s emulator-xxxx` でエミュレータを指定する。

## 準備

- エミュレータを起動する: `emu.py avds` で名前を見て、`emu.py boot <名前>` (起動の完了まで待つ。起動済みなら何もしない)。対象はタブレット (1920x1200、横向き) の AVD
- `emu.py devices` で使う機器を確かめる
- 入れる: `emu.py install` (Debug。ビルドから起動まで数分かかる)。Release は `--release`
- 起動してすぐ落ち、logcat に `No assemblies found` が出るとき (高速配置の本体が端末にない) は、`emu.py install --embed` で本体を APK に含めて入れ直す

## 操作

- 画面を見る: `emu.py shot <一時フォルダ>/xxx.png` で撮って画像を読む。今の画面の名前は `emu.py screen` (logcat の `Navigated:`。Debug だけ出る)
- 押す: `emu.py tap <x> <y>`。座標は撮った画像 (1920x1200) の画素で、縮小して表示された画像から読むときは倍率を戻す
- 文字: `emu.py text <ASCII>` (日本語は送れない)。キーは `emu.py key BACK` / `ENTER` / `DEL` (入力欄を消すときは `emu.py key DEL --repeat 30`)
- なぞる: `emu.py swipe <x1> <y1> <x2> <y2> [ms]` (一覧やタブのスクロール)
- 例外の確認: `emu.py logcat --grep "Exception|FATAL"`
- アプリの設定: `emu.py pref get <キー>` / `emu.py pref set <キー> <値>` (アプリを止めてから書き換わる)

## 専用端末 (Device Owner)

- Device Owner にする: `emu.py owner set` (アカウントを足していないエミュレータで行う)。状態は `emu.py owner status` (Device Owner、ロックタスク、前の画面、ホーム)
- 掛けた制限は、アプリが前に出たときに入る。ホームアプリとして動かすには `emu.py reboot` で再起動する (ランチャーから起動しただけでは、落ちたときにランチャーへ戻る)
- Device Owner のアプリは `am force-stop` と `am crash` が効かない。止めるときは `emu.py kill` (run-as で止める。Debug だけ)
- Device Owner の間は Debug の高速配置でアプリを差し替えられない (止められないため、本体のない APK が残って起動できない)。入れるときは `emu.py install --embed`
- スタッフメニューは、ブランドの印を長押し (`emu.py swipe x y x y 3500`) して PIN (初めは 1234) を入れる
- 外す: `emu.py owner clear` (Debug は testOnly なので外せる。Release は外せない)。画面を点けたままの設定とホームの役割も元に戻す

## 後片付け

- `emu.py stop` でアプリを止める (Device Owner のときは `emu.py kill`)
- Device Owner にしたら `emu.py owner clear` で外す
- 変えた設定は控えた値に戻す
