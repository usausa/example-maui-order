---
name: emulator
description: 端末アプリ (MAUI Android。テーブル端末と受付機はタブレット横向き、ホール端末はスマートフォン縦向き) をエミュレータに入れて動作を確かめる。エミュレータの起動と選択、ビルドと配置、画面の撮影・タップ・文字とキーの入力、遷移のログ、アプリの設定の読み書きを行う。画面や動きを変えたときに使う。実機には入れない。
---

# エミュレータでの確認

操作はリポジトリのルートで `python .claude/skills/emulator/scripts/emu.py <コマンド>` を使う (Windows では `python`、他では `python3`)。
対象の端末アプリは `--app` で選ぶ (既定は `table`。例: `emu.py --app table install`)。
ホール端末は `hall`、受付機は `reception`。
端末アプリを足したら、スクリプトの `APPS` にパッケージ名とプロジェクトを足す。
テーブル端末と受付機はタブレット (1920x1200、横向き)、ホール端末はスマートフォン (1080x2400、縦向き) の AVD で確かめる。
スクリプトはエミュレータ (`emulator-` で始まる機器) だけを選び、実機が接続されていても使わない。
`adb` や `dotnet build -t:Run` を直接使うときも、必ず `-s emulator-xxxx` / `-p:AdbTarget=-s emulator-xxxx` でエミュレータを指定する。

## 準備

- エミュレータを起動する: `emu.py avds` で名前を見て、`emu.py boot <名前>` (起動の完了まで待つ。その AVD が起動済みなら何もしない)
- `emu.py devices` で使う機器を確かめる (エミュレータは AVD の名前つきで出る)
- 入れる: `emu.py install` (Debug。ビルドから起動まで数分かかる)。Release は `--release`
- 起動してすぐ落ち、logcat に `No assemblies found` が出るとき (高速配置の本体が端末にない) は、`emu.py install --embed` で本体を APK に含めて入れ直す

## タブレットとスマートフォンを並べる

- テーブル端末とホール端末を一緒に確かめるときは、タブレットとスマートフォンの AVD を両方 `boot` する
- エミュレータが複数動いているときは、どのコマンドにも `--avd <AVD の名前>` を付けて選ぶ (付けないと止まる。環境変数 `EMU_AVD` でも選べる)
- 例: `emu.py --avd <スマートフォンの AVD> --app hall install`、`emu.py --avd <タブレットの AVD> shot <一時フォルダ>/table.png`
- スマートフォンの画面は 1080x2400 (縦向き)。座標は撮った画像の画素で読む
- ホール端末で提供と呼び出しを操作して確かめるときは、開発のサーバの自動の進行が先に進めないように、提供と向かうまでの秒を長くして起動する (例: `--Simulation:ServedSeconds=3600 --Simulation:AcknowledgeSeconds=3600`)
- 新しい呼び出しの音と振動はエミュレータでは聞こえないので、`adb -s <機器> shell dumpsys vibrator_manager` (振動の記録) と `dumpsys audio` (鳴らした音の区分) で確かめる
- 受付機とテーブル端末を同じタブレットで切り替えて確かめるときは、裏に回したアプリは前に出すまで通知を扱わない (`emu.py --app <アプリ> launch` で前に出してから画面を見る)
- 並べるために起動したエミュレータは、使い終わったら `emu.py --avd <名前> poweroff` で止める

## 操作

- 画面を見る: `emu.py shot <一時フォルダ>/xxx.png` で撮って画像を読む。今の画面の名前は `emu.py screen` (logcat の `Navigated:`。Debug だけ出る)
- 押す: `emu.py tap <x> <y>`。座標は撮った画像 (1920x1200) の画素で、縮小して表示された画像から読むときは倍率を戻す
- 文字: `emu.py text <ASCII>` (日本語は送れない)。キーは `emu.py key BACK` / `ENTER` / `DEL` (入力欄を消すときは `emu.py key DEL --repeat 30`)
- なぞる: `emu.py swipe <x1> <y1> <x2> <y2> [ms]` (一覧やタブのスクロール)
- 例外の確認: `emu.py logcat --grep "Exception|FATAL"`
- アプリの設定: `emu.py pref get <キー>` / `emu.py pref set <キー> <値>` (アプリを止めてから書き換わる)
- アプリの言語: 端末の言語に従うアプリ (ホール端末) は、`emu.py locale ja-JP` でアプリだけの言語を替えて確かめる (アプリを止めて替えるので `emu.py launch` で起動する。`emu.py locale` で端末の言語に戻す)

## 専用端末 (Device Owner)

- Device Owner にする: `emu.py owner set` (アカウントを足していないエミュレータで行う)。状態は `emu.py owner status` (Device Owner、ロックタスク、前の画面、ホーム)
- 掛けた制限は、アプリが前に出たときに入る。ホームアプリとして動かすには `emu.py reboot` で再起動する (ランチャーから起動しただけでは、落ちたときにランチャーへ戻る)
- Device Owner のアプリは `am force-stop` と `am crash` が効かない。止めるときは `emu.py kill` (run-as で止める。Debug だけ)
- Device Owner の間は Debug の高速配置でアプリを差し替えられない (止められないため、本体のない APK が残って起動できない)。入れるときは `emu.py install --embed`
- スタッフメニューは、ブランドの印を長押し (`emu.py swipe x y x y 3500`) して PIN (店舗の設定。サンプルのデータはデモが 1234、検証用が 5678) を入れる
- ホール端末の端末の画面は、タブの画面のヘッダの右の記号から開く (端末の設定と一時的な解除のときに PIN を入れる)
- 外す: `emu.py owner clear` (Debug は testOnly なので外せる。Release は外せない)。画面を点けたままの設定とホームの役割も元に戻す

## 外部の EMM と管理対象の構成

- 外部の EMM で配るとき (EMM が許すロックタスク、管理対象の構成) は、adb (dumpsys) から操作できる DPC を EMM の代わりにして確かめる
- 使う DPC の APK と受け口の名前は、このフォルダの `__` で始まる控え (Git の管理の外) にある。受け口は `--dpc <パッケージ/受け口>` か環境変数 `EMU_DPC` で渡す
- 入れる: `emu.py emm set <DPC の APK>` (DPC を Device Owner にし、アプリのロックタスクを許す)。アプリが Device Owner なら先に `emu.py owner clear` で外す
- 構成を配る: `emu.py emm config apiEndPoint=https://... enrollmentToken=...` (値を並べないと消す)。アプリは知らせを受けて読み直し、スタッフメニューの「EMM の設定」に出す
- 状態: `emu.py emm status` (Device Owner、ロックタスクの許可と状態)。配った構成はアプリのログ (`emu.py logcat --grep "Managed configuration"`) で見る
- 外す: `emu.py emm clear` (構成とロックタスクの許可を消し、DPC を Device Owner から外して消す)

## 後片付け

- `emu.py stop` でアプリを止める (Device Owner のときは `emu.py kill`)
- 並べるために起動したエミュレータは `emu.py --avd <名前> poweroff` で止める
- Device Owner にしたら `emu.py owner clear` で外す
- EMM の代わりの DPC を入れたら `emu.py emm clear` で外す
- 変えた設定は控えた値に戻す
- アプリの言語を替えたら `emu.py locale` で端末の言語に戻す
