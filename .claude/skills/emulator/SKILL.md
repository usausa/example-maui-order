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

## 操作

- 画面を見る: `emu.py shot <一時フォルダ>/xxx.png` で撮って画像を読む。今の画面の名前は `emu.py screen` (logcat の `Navigated:`。Debug だけ出る)
- 押す: `emu.py tap <x> <y>`。座標は撮った画像 (1920x1200) の画素で、縮小して表示された画像から読むときは倍率を戻す
- 文字: `emu.py text <ASCII>` (日本語は送れない)。キーは `emu.py key BACK` / `ENTER` / `DEL` (入力欄を消すときは `emu.py key DEL --repeat 30`)
- なぞる: `emu.py swipe <x1> <y1> <x2> <y2> [ms]` (一覧やタブのスクロール)
- 例外の確認: `emu.py logcat --grep "Exception|FATAL"`
- アプリの設定: `emu.py pref get <キー>` / `emu.py pref set <キー> <値>` (アプリを止めてから書き換わる)

## 後片付け

- `emu.py stop` でアプリを止める
- 変えた設定は控えた値に戻す
