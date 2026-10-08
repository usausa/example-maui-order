---
paths:
  - "server/src/TableOrder.Server.Core/Accessors/**"
---
# Accessor (Smart.Data.Accessor)

SQL ファイルの書き方は sql.md に置く。

- メソッド名は DB の操作で付け、2-way SQL のファイル名 (`{Accessor}.{Method}.sql`) をメソッド名に合わせる。業務の動詞 (登録、認可) は Service の名前にする

| 操作 | 名前 (末尾の `Async` は省略) | 例 |
| --- | --- | --- |
| 件数 | `CountXxx` | `Count` |
| 1 件取得 | `QueryXxx` (キー以外の条件は `QueryXxxBy{条件}`) | `Query`、`QueryTable`、`QueryEnrollmentByPairingCode` |
| 複数件取得 | `QueryXxxList` (ページングしない全件は `QueryXxxAll`) | `QueryCallReasonList`、`QueryAll` |
| 登録 | `InsertXxx` | `Insert`、`InsertStation` |
| 更新 | `UpdateXxx` (状態を変える更新は結果の状態で `UpdateXxxed`) | `Update` |
| 加減算 | `AddXxx` | `AddUsedCount` |
| 登録または更新 | `UpsertXxx` | `UpsertStatus` |
| 削除 | `DeleteXxx` | `Delete` |
| スクリプトの実行 | `ExecuteXxx` | `ExecuteSchema`、`ExecuteScript` |

- SQL はすべて 2-way SQL のファイルに書き、Builder 属性 (`[SelectSingle]` / `[Insert]` / `[Delete]`) は使わない (テナントの条件をファイルで確かめるため)
- テナントを持つ表を扱うメソッドは、最初の引数 (トランザクションがあればその次) に `Guid tenantId` を受ける
- テナントのわからない要求で引くものは `DirectoryAccessor`、テナントの表は `TenantAccessor`、表に紐付かない処理は `GenericAccessor`、テナントをまたぐ裏の処理 (通知の送り手、古いデータの消去) は `BackgroundAccessor` に置く (この 4 つだけテナントの条件を調べるテストから外す)
- 書き込みの中で読むものは、同じ名前でトランザクションを最初の引数に受ける版を足す (SQL のファイルは共有する。別の接続で読むと、まだコミットしていない変更が見えない)
- 状態を条件にした `[Execute]` の更新は戻り値 (更新した件数) を判定し、0 件なら続きの書き込みをしない
- 型の変換は `DataProfile` に登録する (GUID は `GuidTextConverter`、日時は `DateTimeOffsetTextConverter`、列挙型は `EnumTextConverter<T>`、言語ごとの文字は `LocalizedTextConverter`)。新しい列挙型も登録する
- 更新の引数は列ごとに渡す (`/*@ entity.Prop */` にはコンバータが効かない)
- 読み出さない列 (登録トークンのハッシュ) は行の型に持たない
