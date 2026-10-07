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
- パラメータは `/*@ name */` (メソッドの引数名)。後ろの仮の値は、SQL を単体で実行できるように型に合わせる (文字列・GUID・日時は `''`、数値と真偽は `0` / `1`、BLOB と NULL を渡す値は `NULL`、列挙値は列挙名の文字列)
- 状態を変える更新は、遷移元の状態を `AND Status = '...'` で条件にする
- 数を超えないように数える更新は、上限を条件に入れた 1 文にする (`AND UsedCount < MaxUses`)

## スキーマとサンプルのデータ

- スキーマは `CREATE TABLE IF NOT EXISTS` で列名・型・NOT NULL を桁揃えし、主キー・一意・外部キーは表の末尾に制約として書く
- `Tenants` のほかの表は `TenantId` を先頭の列に持ち、主キー・一意・索引・外部キーの先頭にも置く
- テナントのわからない要求で引く列と、テナントをまたぐ裏の処理の索引だけは `TenantId` を付けず、使い道をコメントに書く
- 索引は表の直後に空行なしで `CREATE INDEX IF NOT EXISTS IX_表_列` (一意は `UX_表_列`) と書く
- 互いに指す外部キー (店舗と今のメニュー) は `DEFERRABLE INITIALLY DEFERRED` にする
- サンプルのデータは `INSERT INTO` / 表 / (列) / `VALUES` の行に分けて 1 行 1 レコードで書き、日時は `@now`、ID は表の番号を入れた固定値 (`00000000-0000-0000-0002-…`) にする
