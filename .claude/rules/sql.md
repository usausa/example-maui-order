---
paths:
  - "**/*.sql"
---
# SQL の書き方

Accessor のメソッド名 (= 2-way SQL のファイル名) は accessor.md に置く。

## ファイル

- 2-way SQL は `Accessors/Sql/{Accessor}.{Method}.sql` にする (`StoreAccessor.QueryAsync.sql`)
- スキーマ (`Assets/Data/Schema.sql`) とサンプルのデータ (`Assets/Data/SampleData.sql`) は、起動時に実行する SQL ファイルに書く

## テナント

- テナントを持つ表を読み書きする SQL は、必ず `TenantId = /*@ tenantId */''` で絞る (INSERT は値に `/*@ tenantId */` を入れる)。書き忘れは `SqlTenantConditionTests` が見つける
- 表を結合するときは `TenantId` も合わせる (`ON P.TenantId = S.TenantId AND P.Id = S.MenuPublicationId`)

## 2-way SQL

- 句 (`SELECT` / `FROM` / `WHERE` / `ORDER BY` / `UPDATE` / `SET` / `INSERT INTO` / `VALUES` / `RETURNING`) は行頭に置き、表名・列・条件・値は次の行に 4 桁字下げする。`AND` / `OR` と `JOIN` は字下げした位置の行頭
- `ON CONFLICT (キー) DO UPDATE SET` は 1 行にし、`列 = excluded.列` を次の行から字下げして並べる
- 内部結合は `JOIN` と書き、`INNER` を付けない
- パラメータは `/*@ name */` (メソッドの引数名)。後ろの仮の値は、SQL を単体で実行できるように型に合わせる (文字列・GUID・日時は `''`、数値は `0`、真偽は `1`、`LIMIT` は件数の目安 (`20` など)、BLOB と NULL を渡す値は `NULL`、列挙値は列挙名の文字列)
- 2-way SQL にはコメントを書かず、意図や制約は Accessor のメソッドのコメントに書く
- 状態を変える更新は、遷移元の状態を `AND Status = '...'` で条件にする
- 子の表を数える副問い合わせは、索引のある列 (`StoreId` など) も条件に入れて、テナントのすべての行を読まない
- 数を超えないように数える更新は、上限を条件に入れた 1 文にする (`AND UsedCount < MaxUses`)
- 件数を限る一覧 (`LIMIT`) は、並べる列の後に一意の列 (なければ `Id`) を同じ向きで足す (同じ値の行の順が決まらないと、限りの境目で選ぶ行が問い合わせごとに変わる。下げたチケットと、その明細とオプションを別の問い合わせで引くと食い違う)

## スキーマとサンプルのデータ

- スキーマは `CREATE TABLE IF NOT EXISTS` で列名・型・NOT NULL を桁揃えし、主キー・一意・外部キーは表の末尾に制約として書く
- 入力されるキー (コード、名前、メールアドレス) と採番した番号 (注文の番号) の一意は表の制約 (`UNIQUE (...)`) にし、条件付きの一意 (NULL を除く、状態で絞る) と、テナントを付けずに引く列の一意 (端末の Id、電子レシートのトークン) は一意索引にする
- `Tenants` と `AdminUsers` (運営者はテナントに属さない) のほかの表は `TenantId` を先頭の列に持ち、主キー・一意・索引・外部キーの先頭にも置く
- テナントのわからない要求で引く列と、テナントをまたぐ裏の処理の索引だけは `TenantId` を付けず、使い道をコメントに書く
- 索引は表の直後に空行なしで `CREATE INDEX IF NOT EXISTS IX_表_列` (一意は `UX_表_列`) と書き、名前には索引の列をすべて入れる (先頭の `TenantId` は除く)
- 索引の名前を変えたら、新しい名前で作り、スキーマの末尾で古い名前を `DROP INDEX IF EXISTS` で消す (前の版の DB に残る)
- 互いに指す外部キー (店舗と今のメニュー) は `DEFERRABLE INITIALLY DEFERRED` にする
- サンプルのデータは `INSERT INTO` / 表 / (列) / `VALUES` の行に分けて 1 行 1 レコードで書き、日時は `@now`、ID は表の番号を入れた固定値 (`00000000-0000-0000-0002-…`) にする
