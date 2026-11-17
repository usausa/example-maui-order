---
name: verify
description: 作業の単位の検証 (ビルドの警告 0、テスト、InspectCode の指摘 0、改行コード、文書の改行) を 1 回で行う。作業を閉じる前とコミットの前に使う
---

# 検証

AGENTS.md の「検証」を 1 回で確認する。
スクリプトはリポジトリのルートで実行する (Windows では `python`、他では `python3`)。

```bash
python .claude/skills/verify/scripts/verify.py
```

## 対象

- 対象を絞るときは、`table`、`hall`、`reception`、`server`、`files` から選んで並べる (例: `verify.py table files`)
- 対象を省くと、すべてを検証する
- `table`、`hall`、`reception`、`server` は、下の表のソリューションで次を行う
  - Release と Debug のビルド (リビルド)
  - テストの `dotnet run --project` (Release)
  - InspectCode

| 対象 | ソリューション | テスト |
| --- | --- | --- |
| `table` | `table/TableOrder.TableApp.slnx` | `shared/tests/TableOrder.Domain.Tests`、`shared/tests/TableOrder.Client.Tests` |
| `hall` | `hall/TableOrder.HallApp.slnx` | なし |
| `reception` | `reception/TableOrder.ReceptionApp.slnx` | なし |
| `server` | `server/TableOrder.Server.slnx` | `server/tests/TableOrder.Server.Core.Tests`、`server/tests/TableOrder.Server.Web.Tests` |

- 共有のプロジェクトとテストは、`table` のソリューションで検証する
- `files` は、変更したファイル (git の未コミット分) の改行コードを検証する (新しいファイルは CRLF、既存のファイルは元のまま)
- `files` は、`docs/*.md` と README の改行も検証する (1 行 1 文、「。」の後に 2 スペース)

## オプション

- `--fix`: 改行コードと文書の改行を直す
- `--all-docs`: 変更のない文書も検証する
- `--no-inspect`: InspectCode を省く (途中の確認用)
- 作業の単位の検証では、InspectCode を省かない
- `--out`: ログを残すフォルダ (既定は一時フォルダの `tableorder-verify`)

## 時間の目安

- 端末とサーバのビルド (Release と Debug) は、それぞれ数分かかる
- InspectCode も数分かかる
- テストは数秒かかる (サーバの API のテストは十数秒)
- 時間がかかるので、Claude はバックグラウンドで実行し、終了の通知を待つ

## 前提

- .NET SDK 11 と Android のワークロード
- InspectCode は `jb` を使う (JetBrains.ReSharper.GlobalTools を `dotnet tool install -g` でインストールする)
- テストは `dotnet test` ではなく `dotnet run --project` で実行する (xunit v3 は実行形式のため)

## 結果の見方

- 最後の行が「すべて OK」なら完了
- NG の項目には、名前、件数、先頭の指摘が出る
- NG のときは、ログを読んで直す
- テストの NG には、失敗したテストの名前が出る
- テストの失敗の理由は、ログ (`test-{プロジェクト}.log`) を読んで確認する
- 警告と InspectCode の指摘は、抑止する前に確認する (AGENTS.md)
- 改行コードの NG は、新しく作ったファイルが LF のままのことが多い (Claude の Write は LF で書く)
- `--fix` で改行コードを直してから、もう一度 `files` で検証する
