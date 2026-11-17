---
paths:
  - "server/src/TableOrder.Server.Core/Accessors/**"
---
# Accessor (Smart.Data.Accessor)

## 名前

- 2-way SQL のファイル名 (`{Accessor}.{Method}.sql`) は、メソッド名に合わせる
- 業務の動詞 (登録、認可) は Service の名前にする
- メソッド名は DB の操作で付ける

| 操作 | 名前 (末尾の `Async` は省略) | 例 |
| --- | --- | --- |
| 件数 | `CountXxx` | `Count` |
| 1 件取得 | `QueryXxx` (キー以外の条件は `QueryXxxBy{条件}`) | `Query`、`QueryTable`、`QueryEnrollmentByPairingCode` |
| 複数件取得 | `QueryXxxList` (使わなくした行も含める全件と、テナントや店舗をまたぐ全件は `QueryXxxAll`) | `QueryCallReasonList`、`QueryCallReasonAll`、`QueryStoreAll` |
| 登録 | `InsertXxx` | `Insert`、`InsertStation` |
| 更新 | `UpdateXxx` (状態を変える更新は結果の状態で `UpdateXxxed`) | `Update` |
| 加減算 | `AddXxx` | `AddUsedCount` |
| 登録または更新 | `UpsertXxx` | `UpsertStatus` |
| 削除 | `DeleteXxx` | `Delete` |
| スクリプトの実行 | `ExecuteXxx` | `ExecuteSchema`、`ExecuteScript` |

- `By{…}` は、キーでない条件を名前に出すときに付け (`QueryEnrollmentByPairingCode`、`QueryVacantTableByGuests`、`QueryDoneTicketListByStation`)、親の Id で選ぶ子の一覧 (`QueryStationList`) には付けない

## クラス

- テナントのわからないリクエストで引くものは `DirectoryAccessor` に置く
- テナントの表 (`Tenants`) は `TenantAccessor` に置く
- 管理画面のサインインと自分の資格情報は `AccountAccessor` に置く
- 表に紐付かない処理は `GenericAccessor` に置く
- テナントをまたぐバックグラウンド処理 (通知のディスパッチャ、古いデータのクリーンアップ、失効リスト) は `BackgroundAccessor` に置く
- テナントの条件を調べるテスト (`SqlTenantConditionTests`) から外すのは、この 5 つの Accessor だけにする

## メソッド

- テナントを持つ表を扱うメソッドは、最初の引数 (トランザクションがあればその次) に `Guid tenantId` を受ける
- 書き込みの中で読むものは、同じ名前でトランザクションを最初の引数に受けるオーバーロードを足す (別の接続で読むと、まだコミットしていない変更が見えないため)
- トランザクションを受けるオーバーロードは、受けないものと SQL のファイルを共有する
- 書き換えない行 (公開したメニュー) だけは、トランザクションの外で読んでよい
- 使わなくなったメソッドは削除する (トランザクションを受けるオーバーロードだけを使うなら、受けないものも削除する)
- 更新の引数は列ごとに渡す (`/*@ entity.Prop */` にはコンバータが効かないため)
- 戻り値の 0 や null が何を意味するか (見つからない、先に変更された) は、メソッドのコメントに書く (同じ意味のメソッドが多い Accessor は、クラスのコメントにまとめてよい)

## 読み書き

- SQL はすべて 2-way SQL のファイルに書き、Builder 属性 (`[SelectSingle]`、`[Insert]`、`[Delete]`) は使わない (テナントの条件をファイルで確認するため)
- 一覧を作るときは項目ごとに読まず (N+1)、親の条件で子をまとめて引く (`IN (SELECT ...)`)
- 状態を条件にした `[Execute]` の更新は戻り値 (更新した件数) を判定し、0 件なら続きの書き込みをせずに失敗を返す (コミットせず、通知も書かない)

## 型

- 型の変換は `DataProfile` に登録する (GUID は `GuidTextConverter`、日時は `DateTimeOffsetTextConverter`、日付は `DateOnlyTextConverter`、列挙型は `EnumTextConverter<T>`、言語ごとの文字列は `LocalizedTextConverter`)
- 新しい列挙型も `DataProfile` に登録する
- 読み出さない列 (登録トークンのハッシュ) は行の型に持たない
