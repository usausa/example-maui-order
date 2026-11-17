---
name: emulator
description: 端末アプリ (テーブル端末、ホール端末、受付機) とキッチン端末 (Web) をエミュレータで動かして確認する (インストール、撮影、タップ、入力、ログ、設定)。画面や動きを変更したときに使う
---

# エミュレータでの確認

操作は、リポジトリのルートで `python .claude/skills/emulator/scripts/emu.py <コマンド>` を実行する (Windows では `python`、他では `python3`)。

## 対象のアプリとデバイス

- 対象の端末アプリは `--app` で選ぶ (既定は `table`。例: `emu.py --app table install`)
- ホール端末は `hall`、受付機は `reception` を指定する
- 端末アプリを追加したら、スクリプトの `APPS` にパッケージ名、プロジェクト、Device Owner のレシーバ (`.AdminReceiver`。持たないアプリは `None`) を追加する
- テーブル端末は横置きのタブレット (1920x1200)、受付機は縦置きのタブレット (1200x1920)、ホール端末はスマートフォン (1080x2400、縦向き) の AVD で確認する
- スクリプトはエミュレータ (`emulator-` で始まるデバイス) だけを選び、実機が接続されていても使わない
- `adb` を直接使うときも、必ずエミュレータを指定する (`-s emulator-xxxx`)
- `dotnet build -t:Run` を直接使うときも、必ずエミュレータを指定する (値に空白があるので `"-p:AdbTarget=-s emulator-xxxx"` と引用符で囲む)

## 起動とインストール

- AVD の名前は `emu.py avds` で見る
- エミュレータを起動する: `emu.py boot <名前>` (起動の完了まで待つ。その AVD が起動済みなら何もしない)
- 使うデバイスは `emu.py devices` で確認する (エミュレータは AVD の名前つきで出る)
- インストールする: `emu.py install` (Debug。ビルドから起動まで数分かかる)
- Release は `--release` を付ける
- 起動直後にクラッシュし、logcat に `No assemblies found` が出るときは、Fast Deployment のアセンブリが端末にない
- そのときは、`emu.py install --embed` でアセンブリを APK に含めて再インストールする

## 操作

- 画面を見る: `emu.py shot <一時フォルダ>/xxx.png` で撮って画像を読む
- 今の画面の名前: `emu.py screen` (logcat の `Navigated:`。Debug だけ出る)
- タップ: `emu.py tap <x> <y>`
- 座標は、撮った画像の画素で読む
- 縮小して表示された画像から座標を読むときは、倍率を戻す
- 文字の入力: `emu.py text <ASCII>` (日本語は送れない)
- キー: `emu.py key BACK` / `ENTER` / `DEL`
- 入力欄を消すときは `emu.py key DEL --repeat 30`
- スワイプ: `emu.py swipe <x1> <y1> <x2> <y2> [ms]` (一覧、タブのスクロール)
- 例外の確認: `emu.py logcat --grep "Exception|FATAL"`
- アプリの設定: `emu.py pref get <キー>` / `emu.py pref set <キー> <値>`
- `pref set` は、動いているアプリを止めてから書き換える (Device Owner のアプリは止まらないので、先に `emu.py kill` で止める)
- `pref set` は設定のファイルにないキーを追加しないので、インストールしたばかりのアプリの接続先は端末の設定の画面で入力する
- アプリの言語: 端末の言語に従うアプリ (ホール端末) は、`emu.py locale ja-JP` でアプリだけの言語を変更して確認する
- `locale` はアプリを止めて言語を変更するので、そのあと `emu.py launch` で起動する (Device Owner のアプリは先に `emu.py kill` で止める)

## 並べた確認

- テーブル端末とホール端末を一緒に確認するときは、タブレットとスマートフォンの AVD を両方 `boot` する
- エミュレータが複数動いているときは、どのコマンドにも `--avd <AVD の名前>` を付けて選ぶ (付けないとエラーで止まる)
- 環境変数 `EMU_AVD` でも選べる
- 例: `emu.py --avd <スマートフォンの AVD> --app hall install`、`emu.py --avd <タブレットの AVD> shot <一時フォルダ>/table.png`
- 受付機は縦置きのタブレットの AVD にインストールし、テーブル端末 (横置きのタブレット) と並べて確認する
- 同じ AVD に両方をインストールして切り替えると、バックグラウンドのアプリはフォアグラウンドに戻すまで通知を扱わない
- 同じ AVD で切り替えたときは、`emu.py --app <アプリ> launch` でフォアグラウンドに戻してから画面を見る

## ホール端末の呼び出しと提供

- ホール端末で提供と呼び出しを操作して確認するときは、提供と向かうまでの秒を長くしてサーバを起動する (開発のサーバのシミュレーションが先に進めないように)
  - 例: `--Simulation:ServedSeconds=3600 --Simulation:AcknowledgeSeconds=3600`
- 新しい呼び出しの音と振動はエミュレータでは聞こえないので、dumpsys で確認する
  - 振動の記録: `adb -s <デバイス> shell dumpsys vibrator_manager`
  - 鳴らした音の区分: `dumpsys audio`
- 画面が消えている間の通知は、次の順に確認する
  1. `adb -s <デバイス> shell input keyevent KEYCODE_SLEEP` で画面を消す
  2. `dumpsys battery unplug` と `dumpsys deviceidle force-idle` で Doze に入れる
  3. 呼んで確認する
  4. 終わったら `dumpsys deviceidle unforce` と `dumpsys battery reset` で戻す
- フォアグラウンドサービスとウェイクロックは、`dumpsys activity services <パッケージ>` と `dumpsys power` で見る

## キッチン端末 (Web アプリ)

- 画面を作っている間は、アプリの中のブラウザをタブレットの横向きの大きさ (1396x873) にして確認する
- 並べた確認と README の画像は、タブレットのエミュレータのブラウザ (既定のブラウザ) で見る
- 開く: `emu.py browse http://localhost:<ポート>/kitchen/` (エミュレータの localhost を、この PC の同じポートに転送する)
- 10.0.2.2 では開かない (端末の鍵の WebCrypto は HTTPS か localhost でしか使えないため)
- `browse` は同じタブを使い回す (開くたびにタブが増えると、どのタブも Web アプリを動かして重くなるため)
- ほかの方法で開いて増えたタブは、タブの一覧のメニューの「すべてのタブを閉じる」で閉じる
- 画面を 1 回タップすると全画面になり、新しいチケットの音が鳴るようになる
- 再読み込み: `emu.py browse --reload` (全画面の間も使える)
- キッチン端末はブラウザの言語で文言を選ぶ
- 日本語で見るときは、`emu.py browse --locale ja-JP` でブラウザだけの言語を変更する (ブラウザを止めて変更する)
- ブラウザを初めて開くと、利用規約と設定の画面が出る
- 利用規約への同意は、人に確認してから行う
- 使用状況の送信、通知、広告の機能はオフ (プライバシーを守る側) を選ぶ

## 管理画面

- README の管理画面の画像は、エミュレータではなく PC のブラウザで撮る (readme.md の規則)
- エミュレータのブラウザでは、`emu.py browse http://localhost:<ポート>/account/sign-in` を開いてサインインする
- 入力欄はソフトキーボードで隠れるので、メールアドレスを入力したら `emu.py key TAB` でパスワードの欄に移る
- パスワードを入力したら、`emu.py key ENTER` で送信する
- 管理画面の Cookie は SameSite=Strict なので、`browse` でほかのパスを開くと外部からの遷移になり、サインインの画面に戻る
- ほかの画面へは、サインインしたタブで画面の左のメニューから移る
- 再読み込みは `browse --reload` で行う
- ブラウザのパスワードの保存と翻訳の案内は、保存も翻訳もせずに、案内の外をタップして閉じる
- 撮る前に画面を少しスワイプして、アドレスバーを隠す (バーが出ている間は、タップする位置がバーの高さだけ下にずれる)

## 専用端末 (Device Owner)

- Device Owner にする: `emu.py owner set` (アカウントを追加していないエミュレータで行う)
- 状態: `emu.py owner status` (Device Owner、ロックタスク、フォアグラウンドの Activity、ホーム)
- Device Owner の制限は、アプリがフォアグラウンドになったときに適用される
- ホームアプリとして動かすときは、`emu.py reboot` で再起動する (ランチャーから起動しただけでは、クラッシュしたときにランチャーへ戻る)
- Device Owner のアプリには `am force-stop` と `am crash` が効かない
- 止めるときは `emu.py kill` を使う (run-as で止める。Debug だけ)
- Device Owner の間は、Debug の Fast Deployment でアプリを差し替えられない (アプリを止められず、アセンブリのない APK が残って起動できないため)
- Device Owner の間にインストールするときは、`emu.py install --embed` を使う
- スタッフメニューは、ブランドの印を長押しして (`emu.py swipe x y x y 3500`) PIN を入力すると開く
- PIN は店舗の設定で決まる (サンプルのデータはデモが 1234、検証用が 5678)
- ホール端末の端末の画面は、タブの画面のヘッダの右のアイコンから開く
- ホール端末の端末の画面では、端末の設定と一時的な解除のときに PIN を入力する
- 外す: `emu.py owner clear` (Debug は testOnly なので外せる。Release は外せない)
- `owner clear` は、画面を点けたままの設定とホームの役割も元に戻す

## 外部の EMM と管理対象の構成

- 外部の EMM で配布したときの動き (EMM が許すロックタスク、管理対象の構成) は、adb (dumpsys) から操作できる DPC を EMM の代わりにして確認する
- 使う DPC の APK とレシーバの名前は、このフォルダの `__` で始まる控え (Git の管理の外) にある
- レシーバは `--dpc <パッケージ/レシーバ>` か環境変数 `EMU_DPC` で渡す
- アプリが Device Owner のときは、先に `emu.py owner clear` で外す
- DPC のインストール: `emu.py emm set <DPC の APK>` (DPC を Device Owner にし、アプリのロックタスクを許可する)
- 構成の配信: `emu.py emm config apiEndPoint=https://... enrollmentToken=...` (値を並べないと構成を消す)
- アプリは通知を受けて構成を再読み込みし、スタッフメニュー (ホール端末は端末の画面) の「EMM の設定」に出す
- 状態: `emu.py emm status` (Device Owner、ロックタスクの許可と状態)
- 配信した構成は、アプリのログ (`emu.py logcat --grep "Managed configuration"`) で見る
- 外す: `emu.py emm clear` (構成とロックタスクの許可を消し、DPC を Device Owner から外してアンインストールする)

## クリーンアップ

- `emu.py stop` でアプリを止める (Device Owner のときは `emu.py kill`)
- 並べるために起動したエミュレータは、使い終わったら `emu.py --avd <名前> poweroff` で止める
- Device Owner にしたら、`emu.py owner clear` で外す
- EMM の代わりの DPC をインストールしたら、`emu.py emm clear` で外す
- 変更した設定は、控えた値に戻す
- アプリの言語を変更したら、`emu.py locale` で端末の言語に戻す
- ブラウザでキッチン端末を開いたら、`emu.py browse --close` でブラウザを止め、ポートの転送を解除する
- ブラウザの言語を変更したら、`emu.py browse --locale default` で端末の言語に戻す
