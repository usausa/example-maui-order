# 注文 API 設計 (想定)

注文サーバの API の想定。  
店内の注文に関わる端末 (テーブル、ホール、キッチン、受付。どれもこのリポジトリのアプリ) と、外部のシステムが使う。  
作った API は [architecture.md](architecture.md#-10-サーバの実装) に挙げる。  
まだ作っていない API は、端末とこれから作るサーバがこの文書に合わせる。

| 内容 | 文書 |
| --- | --- |
| 業務の前提と流れ (端末と業務、来店、金額と税、扱わないもの) | [business.md](business.md) |
| データベース | [database.md](database.md) |
| 実装の計画 | [plan.md](plan.md) |

- [1. 共通仕様](#-1-共通仕様)
- [2. リソース別 API](#-2-リソース別-api)
- [3. リアルタイム通知](#-3-リアルタイム通知)
- [4. 端末ごとの API](#-4-端末ごとの-api)
- [5. エラーコード](#-5-エラーコード)
- [6. 参考にした考え方](#-6-参考にした考え方)

---

## 🔗 1. 共通仕様

### 1.1 URL・形式

| 項目 | 仕様 |
| --- | --- |
| ベースパス | `/api/v1` (URL のパスでバージョンを分ける) |
| JSON | camelCase。`null` のプロパティは省く。列挙型は文字列 (`"Open"`) |
| 日時 | UTC の `yyyy-MM-ddTHH:mm:ss.fffZ` |
| 営業日 | `yyyy-MM-dd` |
| 営業時間と時間帯 | 店舗の現地時刻の `HH:mm` |
| 金額 (`money`) | `decimal` (円の整数)。価格は税込 |
| 率 (`rate`) | `decimal` (`0.10` = 10%) |
| ID | GUID (すべてのテナントで一意)。端末で起きる登録 (来店、注文、明細、呼び出し、支払) は、端末が GUID v7 を採番する (再送で重複しない) |
| 多言語の文字列 (`LocalizedText`) | `{ "ja": "ハンバーグ", "en": "Hamburg Steak" }`。`ja` は必須。ない言語は `ja` を出す |
| 通信データ | `XxxRequest` / `XxxResponse` (一覧は `XxxListResponse`、要素は `XxxListResponseItem`)。共有のプロジェクト (`TableOrder.Contract`) に置く |
| エンドポイント | ASP.NET Core。リクエストは REST (Minimal API)、通知は SignalR。gRPC は必要になってから足す |

- エンドポイントが違っても、業務の処理は同じにする
- 本書のフィールド名は JSON (camelCase) で書く。  
  C# のプロパティ名は PascalCase (`tableId` → `TableId`)

### 1.2 一覧

- 一覧は `{ "items": [ ... ] }` で返す
- 店内の業務の一覧 (テーブル、開いているチケット、呼び出し) は件数が少ないので、ページングしない
- 並びはリソースごとに決める (テーブルは表示順、チケットと呼び出しは古い順)

### 1.3 書き込み

| 項目 | 仕様 |
| --- | --- |
| 作成 | `POST` (本文に端末が採番した `id`) → `201 Created`。同じ `id` が既にあれば `200 OK` で既存を返し、主な項目が違えば `409` (`DUPLICATE_ID_MISMATCH`) |
| 状態の変更 | `POST /resources/{id}/{動詞}` (例: `/visits/{id}/move`、`/calls/{id}/acknowledge`)。状態に合わなければ `422` |
| 楽観ロック | 来店の変更 (人数、移動、会計の開始と終了) は、本文の `version` で確認する。違えば `409` (`VERSION_MISMATCH`) |
| 検証 | 入力の誤りは `400` (`VALIDATION_ERROR`。`errors` に項目ごと)。業務のルールの違反は `422` |

### 1.4 エラーレスポンス

RFC 9457 の Problem Details に `errorCode` を足す (コードは [§5](#-5-エラーコード))。

- リクエストの値を読めないとき (クエリの値や JSON の形式の誤り、知らない項目) も `400` (`VALIDATION_ERROR`) にする
- `title` は日本語の文言にする (リクエストの値を読めないときと `500` も)
- 端末は `title` ではなく `errorCode` で扱いを決める
- 注文の検証で明細ごとの違反があるときは、`errors` のキーを明細の `id` にする

```jsonc
{
  "title": "売り切れの商品があります",
  "status": 422,
  "errorCode": "ITEM_SOLD_OUT",
  "errors": { "0192c3e0-...": ["売り切れ"] },   // 明細の id ごと
  "traceId": "00-..."
}
```

### 1.5 認証・認可

| 利用者 | 方式 | 使える範囲 |
| --- | --- | --- |
| テーブル端末 | `Authorization: Bearer {アクセストークン}` | 自分のテーブルの来店と、その注文・呼び出し・会計。来店の開き方が席の店では、自分のテーブルの来店の開始。メニュー・品切れ・店舗の読み取り |
| ホール端末 | アクセストークン | 店舗のすべてのテーブルと来店、注文、提供、呼び出し、品切れ、注文の一時停止 |
| キッチン端末 | アクセストークン | 受け持つ持ち場のチケット、品切れ、メニューと店舗の読み取り |
| 受付機 | アクセストークン | 来店の開始 (来店の開き方が受付機の店。席はサーバが決める)、空いているテーブルの参照 |
| 外部 (本部、POS) | アクセストークン (OAuth 2.0 の client credentials で受け取る) | 本部はメニューの公開と写真。POS はテーブルと会計の参照、来店の終了 (レジで払ったとき) |
| 外部 (決済サービス) | 決済サービスの署名 | 決済の完了の通知 |

端末は自分の鍵で署名して、短命のアクセストークン (JWT) を受け取る。

1. 登録: 端末は、エクスポートできない鍵 (P-256。Android は Keystore、キッチン端末はブラウザの鍵) を作る。  
   `POST /devices/pair` で、ペアリングコードか登録トークン、アプリの端末の種類、公開鍵 (JWK) を送る ([§2.1](#21-端末-devices))。  
   サーバは端末 (テナント、店舗、種類、置き場所、公開鍵) を記録する。  
   コードやトークンの種類とアプリの種類が違えば、コードを使わずに断る
2. トークン: 端末は自分の鍵で署名したワンタイムの JWT (期限 5 分以内) を `POST /devices/token` に送り、アクセストークン (JWT、30 分) を受け取る。  
   アクセストークンには、端末、テナント、店舗、種類、置き場所 (テーブル、持ち場) を入れる。  
   端末は期限の前にトークンを再取得する。  
   同じ鍵で再登録した端末が新しい登録でトークンを受け取ると、前の登録を無効にする (鍵を持つことを確認してから)
3. リクエスト: アクセストークンを `Authorization: Bearer` で送る。  
   サーバは署名と期限を検証するだけで、リクエストごとに端末を照会しない。  
   通知のハブ (`/hubs/store`) にも同じトークンで接続する
4. 取り消し: 管理画面で端末を無効にすると、次のトークンを発行しない (`403` `DEVICE_REVOKED`)。  
   発行したトークンも期限を待たずに拒み (`401`)、ハブの接続も切る。  
   テナントを止めたときも同じ

外部のシステムは、管理画面で発行したクライアント (`client_id` と `client_secret`) で `POST /oauth/token` (OAuth 2.0 の client credentials) を呼び、アクセストークン (JWT、30 分) を受け取る。

- リクエストとレスポンスは、OAuth の標準の形式 (フォームの本文、`access_token`、`expires_in`) にする
- クライアントは 1 つのテナントに属する。  
  POS のように、1 つの店舗に限ることもできる
- クライアントごとに範囲 (`scope`) を決める

| 項目 | ルール |
| --- | --- |
| リクエスト | `grant_type=client_credentials`、`client_id`、`client_secret`、任意の `scope` (空白で区切る。省けばクライアントの範囲すべて) |
| レスポンス | `{ access_token, token_type: "Bearer", expires_in, scope }` |
| 失敗 | OAuth の形式 (`{ error, error_description }`)。理由ごとのコードは下の表 |
| 取り消し | 取り消したクライアントのトークンも、端末と同じく期限を待たずに拒む (`401`) |

| 失敗の理由 | レスポンス |
| --- | --- |
| 知らないクライアント、違うシークレット、取り消したクライアント | `401` `invalid_client` (理由を見せない) |
| 止めたテナント | `400` `unauthorized_client` |
| 範囲の外 | `400` `invalid_scope` |
| ほかの `grant_type` | `400` `unsupported_grant_type` |

| 範囲 (`scope`) | 使える API |
| --- | --- |
| `menu.publish` | `PUT /images/{name}`、`POST /menu/publications` |
| `visits.read` | `GET /tables`、`GET /visits/{id}`、`GET /visits/{id}/bill` |
| `visits.close` | `POST /visits/{id}/close` |

アクセストークンのクレーム:

| クレーム | 端末 | 外部 | 中身 |
| --- | :---: | :---: | --- |
| `iss` / `aud` | ✅ | ✅ | 発行したサーバと、この API |
| `sub` | ✅ | ✅ | 端末の `id`。外部はクライアントの `id` |
| `exp` / `iat` | ✅ | ✅ | 期限 (30 分) と発行した時刻 |
| `tenant_id` | ✅ | ✅ | テナント ([§1.6](#16-テナント)) |
| `store_id` | ✅ | 店舗に限るクライアント | 店舗 |
| `device_kind` | ✅ | | `Table` / `Hall` / `Kitchen` / `Reception` |
| `table_id` / `station_ids` | テーブル端末 / キッチン端末 | | 置き場所 (テーブル、持ち場) |
| `scope` | | ✅ | 使える範囲 (`menu.publish`、`visits.read`、`visits.close` など) |

- クレームの値は、サーバが端末とクライアントの記録から入れる。  
  端末が送った値は使わない
- 署名の鍵は環境ごとに持つ (テナントごとには分けない)。  
  鍵を切り替えられるように、トークンに `kid` を付ける
- 範囲の外のリクエストは `403` にする
  - 端末の種類 (`Table` / `Hall` / `Kitchen` / `Reception`) と置き場所の範囲の外: `DEVICE_SCOPE`
  - 外部のクライアントの範囲 (`scope`) の外: `CLIENT_SCOPE`
- `401` を受けた端末は、トークンを再取得して 1 回だけ再送する
- 端末が初期設定に戻るのは、トークンのリクエストが `DEVICE_REVOKED` で断られたときだけにする (期限切れや一時的な不具合で、店の端末が外れないように)
- 置き場所 (テーブル、持ち場) は端末の記録に持ち、席替えで再登録しない。  
  変更は次のトークンと `GET /devices/me/config` に出る
- 取消や来店の終了などスタッフの操作は、任意で `staffId` を付けて記録する (スタッフの管理は扱わない。[扱わないもの](business.md#-6-扱わないもの))

### 1.6 テナント

注文サーバは、複数の会社 (テナント) が契約して使う。

- テナントは契約の単位 (チェーンを運営する会社・グループ) で、その中に店舗を持つ
- 端末は 1 つの店舗に属する
- 外部のクライアントは、1 つのテナント (か、その中の 1 つの店舗) に属する

```
テナント ─┬─ 店舗 ─┬─ テーブル、持ち場
          │        └─ 端末
          └─ 外部のクライアント (テナント全体か 1 つの店舗)
```

| 項目 | ルール |
| --- | --- |
| 見分け方 | テナントと店舗は、アクセストークンのクレーム (`tenant_id`、`store_id`) で決める |
| URL・ヘッダ・本文 | テナントは入れない。店舗を指すのは、テナント全体のクライアントだけ (店舗コードで指す) |
| 接続先 | すべてのテナントで同じ (Web の画面のキッチン端末と管理画面も同じ) |
| 認証のあと | 署名・発行者 (`iss`)・対象 (`aud`)・期限を検証したトークンのテナント・店舗・端末の種類・置き場所は、そのリクエストの間は信用する。リクエストごとにデータベースで引き直さない |
| リクエストの中の `id` | パス・クエリ・本文の `id` (来店、注文、チケット、支払など) は、トークンのテナントと店舗の中で引く。ほかのテナントや店舗のものは `404` (`NOT_FOUND`) にし、あるかどうかも見せない |
| 店舗の中の範囲 | テーブル端末は自分のテーブル、キッチン端末は受け持つ持ち場の範囲だけを使える。範囲の外は `403` (`DEVICE_SCOPE`) |
| 一意の範囲 | `id` (GUID) はすべてのテナントで一意。店舗コードと商品コードはテナントの中、テーブルの名前は店舗の中で一意 |
| 匿名のリクエスト | 端末の登録 (`POST /devices/pair`) はトークンがない。ペアリングコードと登録トークンをすべてのテナントで一意にし、そこからテナントと店舗を決める |
| 外部のクライアント | 複数のテナントと連携する相手は、テナントごとにクライアントを持つ。テナント全体のクライアントは、店舗をテナントの中の店舗コードで指す (メニューの公開の `storeCodes`) |
| 決済サービスの通知 | 取引番号で支払を引き、その支払のテナントの決済サービスの設定で署名を検証する。リクエストの中のテナントの値は使わない |
| ハブの通知 | トークンの店舗の通知だけを送る。どの通知を受けるかを端末に選ばせない |
| Webhook | テナントごとに送り先を登録し、本文にテナントと店舗を付ける |
| 管理画面 | 利用者はテナントに属し、自分のテナントだけを扱う (店舗担当者は受け持つ店舗だけ)。サービスの運営者は、テナントを選んで扱う ([管理画面](business.md#管理画面)) |
| レート制限 | 端末・クライアントごとと、テナントごとに適用する (`429`)。1 つのテナントの負荷で、ほかのテナントを遅くしない |
| ログ・メトリクス・トレース | テナントと店舗を付ける (問い合わせの調査と、テナントごとの使用量のため) |
| 停止 | 契約を止めたテナントにはトークンを発行しない (`403` `TENANT_SUSPENDED`)。発行したトークンも拒む (`401`) |
| 専用の環境 | 大きなテナントを別の環境に分けるときも、API は変えない。接続先 (EMM で配布する `apiEndPoint`) を切り替える |

トークンのテナントを信用するために、次のことを守る。

- トークンは 30 分で切れる。  
  置き場所の変更 (席替え、持ち場) と契約の変更は、次のトークンで反映する
- すぐに止めるもの (端末の無効化、テナントの停止) は、サーバがメモリに持つ一覧でリクエストごとに拒む (データベースは引かない)。  
  一覧は数秒ごとに再読み込みし、サーバを複数台にしても同じものを持つ
- テナントがあって止まっていないかは、トークンを発行するときに確認する
- トークンを信用していても、リクエストの中の `id` は毎回テナントと店舗で絞って引く (オブジェクト単位の認可)
- 業務の処理は、テナントと店舗をリクエストのコンテキストで受け取り、データの読み書きで必ず絞る (データベースはすべての表に `TenantId` を持つ。[database.md](database.md#テナントの持ち方))。  
  絞り忘れは、SQL がテナントで絞っているかを調べるテストで見つける

---

## 🌐 2. リソース別 API

各表の「利用者」は、テーブル / ホール / キッチン / 受付 / 外部 ([§1.5](#15-認証認可))。  
フィールドの表は、レスポンスの項目。

### 2.1 端末 (Devices)

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `storeId` | guid | |
| `kind` | enum | `Table` / `Hall` / `Kitchen` / `Reception` |
| `name` | string(50) | 例: `T12`、`ハンディ 1`、`キッチン 1` |
| `tableId` | guid? | テーブル端末の置き場所 (管理画面で変更する) |
| `stationIds` | guid[] | キッチン端末が受け持つ持ち場 |
| `appVersion` | string(50)? | 端末が送る (登録と状態の報告) |
| `batteryLevel` | rate? | 電池の残量 (`0`〜`1`)。充電が切れそうなテーブル端末に気付くため |
| `isCharging` | bool? | |
| `lastSeenAt` | datetime? | 最後に通信した時刻 (サーバが付ける) |
| `isActive` | bool | |

| Method | Path | 利用者 | 概要 |
| --- | --- | --- | --- |
| POST | `/devices/pair` | 全端末 (匿名) | 端末の登録 `DevicePairRequest { pairingCode か enrollmentToken, kind, publicKey, deviceName, appVersion }` → `201` `DevicePairResponse { deviceId, kind, storeId }` |
| POST | `/devices/token` | 全端末 (端末の鍵の署名) | アクセストークンの取得 `DeviceTokenRequest { assertion }` → `200` `DeviceTokenResponse { accessToken, expiresIn }` |
| POST | `/devices/me/heartbeat` | 全端末 | 端末の状態 `DeviceHeartbeatRequest { appVersion, batteryLevel, isCharging }` → `204`。1 分ごと |
| GET | `/devices/me/config` | 全端末 | 端末の設定 (`DeviceConfigResponse`) |

- `POST /devices/pair`
  - `kind` は、登録するアプリの端末の種類
  - コードの不一致・期限切れ・使用済みは `422` (`PAIRING_CODE_INVALID`)
  - コードやトークンと端末の種類が違えば `422` (`DEVICE_KIND_MISMATCH`)。  
    このときコードは使わない
  - 使わなくしたテーブルのコードは、置き場所なしで登録する
  - 接続元ごとに 1 分 10 回まで
- `POST /devices/token`
  - `assertion` は、端末の鍵で署名した JWT
  - 無効にした端末は `403` (`DEVICE_REVOKED`)
  - 同じ鍵の前の登録 (再登録する前の端末) は無効にする
- `GET /devices/me/config`
  - 起動のときに読む
  - 設定のバージョン (`settingsVersion`) が変わったら、起動からやり直して再取得する

端末の登録と変更:

- ペアリングコード (6 桁、10 分、一度だけ) は管理画面で発行する。  
  発行するときに、端末の種類と置き場所 (テーブル、持ち場) を決める
- 管理画面で端末の置き場所・名前を変更したときと、無効にしたときは、その端末に `device.updated` で通知する。  
  端末は起動からやり直して、新しいトークンと設定を受け取る
- 専用端末として EMM から配布するときは、管理対象の構成 (Managed configurations) で接続先と登録トークンを渡す。  
  端末は、起動したときに自分で登録する
- 登録トークンは、店舗と種類に限り、台数と期限 (1~30 日) を決めたもの
- 登録トークンで登録した端末の置き場所は、管理画面で割り当てる

設定の変更:

| 設定 | 内容 |
| --- | --- |
| チェーン (テナント) の設定 | チェーンの名前・ロゴ・色 |
| 店舗の設定 | 機能の有無・言語・支払方法・呼び出しの用件・スタッフの PIN |

- どちらも管理画面で変更する
- 変更したら設定のバージョンを上げて、`store.updated` を送る (チェーンの設定はテナントのすべての店舗に)
- テーブル端末は、来店のないとき (待受) に起動からやり直して反映する

`DeviceConfigResponse` の項目:

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `storeName` | LocalizedText | 店舗の名前 (チェーンの名前は `brand.name`) |
| `languages` | string[] | 画面で選べる言語 (`["ja", "en"]`)。1 つだけなら、端末は言語のボタンを出さない |
| `orderRules` | object | `maxQuantityPerLine` (1 明細の数量の上限)、`maxLinesPerOrder` (1 回の注文の明細の上限) |
| `paymentMethods` | enum[] | テーブルで使える支払方法 (`QrCode` / `CreditCard`)。空ならテーブルでは会計せず、レジに案内する |
| `callReasons` | object[] | 呼び出しの用件 `{ code, name (LocalizedText), sortOrder }` (店舗で選べる。[§2.9](#29-呼び出し-calls))。空なら端末は店員呼出を出さない |
| `electronicReceipt` | bool | 電子レシートを出すか |
| `taxRounding` | enum | 税額の端数 (§2.2) |
| `device` | object | 端末 `{ id, kind, name, tableId, tableName, stationIds }` (§2.1)。置き場所はここで受け取る |
| `brand` | object | チェーンの設定 `{ name (LocalizedText), logoImageName (string?), theme }`。ロゴは正方形の画像 (背景色を含む。[§2.3](#23-メニュー-menu) の画像) |
| `brand.theme` | object[] | 変更する色 `[{ role, color }]` (例: `{ "role": "PrimaryColor", "color": "#1E5FA8" }`)。ない `role` は端末の既定のまま |
| `features` | object | 機能の有無 `{ registerCheckout, splitPayment, lastOrderNoticeMinutes, finishSeconds, visitOpening, kitchenAlertMinutes, daypartGraceMinutes }` (下の表)。ない項目は既定の値 |
| `staffPin` | object? | スタッフの PIN のハッシュ `{ iterations, salt, hash }` (PBKDF2-HMAC-SHA256。`salt` と `hash` は Base64) |
| `settingsVersion` | int | チェーンと店舗の設定のバージョン (`store.updated` の店舗の `settingsVersion` と比べる) |

- `brand`: ロゴがなければ、端末はロゴの代わりに名前の頭文字を出す
- `brand.theme`: `role` は Brand・Neutral・Status の色 (`ThemeRoles`)。  
  `color` は `#RRGGBB` か `#AARRGGBB`
- `staffPin`: 端末は、入力した PIN を同じ計算で検証し、平文を持たない。  
  PIN を使う端末 (テーブル端末、ホール端末、受付機) だけに返し、キッチン端末は null

機能の有無 (`features`):

| フィールド | 型 | 既定 | 説明 |
| --- | --- | --- | --- |
| `registerCheckout` | bool | `true` | お会計で「レジで払う」を出す。支払方法が空の店は必ず `true` |
| `splitPayment` | bool | `true` | 割り勘で 1 人分ずつ払える |
| `lastOrderNoticeMinutes` | int | `30` | ラストオーダーの何分前から表示するか (`0` は表示しない) |
| `finishSeconds` | int | `30` | お礼の画面から待受に戻るまでの秒数 |
| `visitOpening` | enum | `Hall` | 来店の開き方 (下の表) |
| `kitchenAlertMinutes` | int | `15` | キッチン端末で、チケットができてから何分で注意の色にするか (`0` は色を変えない) |
| `daypartGraceMinutes` | int | `2` | 時間帯の終わりから何分までに届いた注文を受けるか (`0` は受けない。`10` まで) |

| `visitOpening` | 来店の開き方 |
| --- | --- |
| `Hall` | スタッフがホール端末で開く |
| `Reception` | 受付機でお客様が人数を入れ、サーバが席を決める |
| `Table` | お客様がテーブル端末で始める |

- ホール端末は、どの来店の開き方でも来店を開ける
- `daypartGraceMinutes` は、確定を押したあとの通信の遅れの猶予。  
  サーバだけが使い、端末は時間帯の終わりの時刻で品を隠す
- お酒の年齢の確認やドリンクバーの人数分の提案は、店舗の設定ではなくメニューのルール ([§2.3](#23-メニュー-menu)) で決める

### 2.2 店舗とテーブル (Store / Tables)

店舗:

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(16) | 店舗コード (POS と合わせる) |
| `name` | LocalizedText | |
| `timeZone` | string(50) | `Asia/Tokyo` |
| `businessDate` | date | 今の営業日 |
| `openTime` / `closeTime` | string | `HH:mm`。開店の時刻を営業日の区切りにし、閉店が開店より前なら日をまたぐ |
| `lastOrderTime` | string? | `HH:mm`。過ぎたら注文を受け付けない (`422` `LAST_ORDER_PASSED`)。ラストオーダーのない店は null |
| `orderingPaused` | bool | 注文の一時停止 (厨房が追いつかないときなど) |
| `pausedMessage` | LocalizedText? | 一時停止の間にテーブル端末に出す文言 |
| `taxRounding` | enum | `Floor` / `Round` / `Ceiling`。税額の端数 (既定 `Floor`) |
| `settingsVersion` | int | チェーンと店舗の設定のバージョン。端末の設定の `settingsVersion` と違えば、テーブル端末は待受のときに起動からやり直す |

テーブル:

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `name` | string(20) | 例: `12` |
| `area` | string(20)? | 例: `窓側`、`2F` |
| `capacity` | int | 席の数 |
| `sortOrder` | int | |
| `visit` | object? | 今の来店の要約 `{ visitId, adults, children, status, openedAt, lastOrderedAt, unservedCount, openCallCount, version }` (ホールの席の一覧のため) |

- `visit` の `unservedCount` は、まだ出していない明細 (取消と食後の品を除く)
- `visit` の `version` は、来店の操作に使う

| Method | Path | 利用者 | 概要 |
| --- | --- | --- | --- |
| GET | `/store` | 全端末 | 店舗 (`StoreResponse`) |
| PUT | `/store/ordering` | ホール | 注文の一時停止と再開 `StoreOrderingRequest { paused, message? }` → `204` (再開では文言を消す)。通知 `store.updated` |
| GET | `/tables?status` | ホール / 受付 / 外部 (POS) | テーブルと今の来店の要約 (`TableListResponse`。表示順)。`status` で絞る (下の表) |

| `status` | 対象のテーブル |
| --- | --- |
| `Vacant` | 来店なし |
| `Occupied` | 会計の前の来店 |
| `Paying` | 会計中 |

### 2.3 メニュー (Menu)

店舗で出すメニュー全体を 1 回で返す。  
メニューの編集は本部の管理システムで行い、公開した結果をこの API で配信する ([扱わないもの](business.md#-6-扱わないもの))。

| Method | Path | 利用者 | 概要 |
| --- | --- | --- | --- |
| GET | `/menu` | テーブル / ホール / キッチン | メニュー (`MenuResponse`)。`If-None-Match` に `menuVersion` を付けると、変わっていなければ `304` |
| GET | `/images/{name}` | 全端末 | 画像 (料理の写真、チェーンのロゴ)。ない名前は `404`、使えない文字の名前は `400` |
| PUT | `/images/{name}` | 外部 (本部) | 画像のアップロード → `204`。同じ名前で中身が違えば `409` (`IMAGE_CONFLICT`) |
| POST | `/menu/publications` | 外部 (本部) | 本部で編集したメニューの公開 `MenuPublishRequest { storeCodes, menu }` → `200` `{ menuVersion, storeCodes }`。通知 `menu.published` |

画像:

- テナントごとに保存する
- 名前は英小文字・数字・`-`・`_`・`.` にし、拡張子は `png` / `jpg` / `jpeg` / `webp` にする
- 内容を変えるときは、名前も変える (内容のハッシュを入れる。例: `hamburg.3f2a9c1e.png`)。  
  名前は内容が変わると変わるので、端末はキャッシュして使い回す (`Cache-Control: private, max-age=31536000, immutable`)
- 形式は `image/png` / `image/jpeg` / `image/webp` で、2 MB まで。  
  形式は中身の先頭で検証する
- 写真は端末の画面に合う大きさ (1280x960 ほど) にする
- 公開の前にアップロードし、商品の `imageName` で指す

公開 (`POST /menu/publications`):

- `menu` は、`MenuResponse` から `menuVersion` を除いた形
- 受け取ったら、すぐに今のメニューを切り替える
- 店舗に限るクライアントは、自分の店舗だけに公開する
- 公開の内容は、次のことを検証する。  
  誤りは `400` (`VALIDATION_ERROR`) で、項目の場所 (`menu.items[3].optionGroupIds[0]`) ごとに返す
  - `id` の重複
  - 指すもの (カテゴリの商品、商品のオプションの組と持ち場、ルールの提案する商品)
  - 価格と差額 (0 以上)
  - 選ぶ数
  - 写真の名前の画像があること
- 知らない店舗コードがあれば、全体を断る (複数の店舗への公開は 1 つのトランザクション)
- 公開で消えた商品とオプションの品切れは消す (`stock.updated`)
- 持ち場の `id` は、再公開しても変えない

`MenuResponse` の項目:

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `menuVersion` | string | 公開のたびに変わる |
| `categories` | object[] | カテゴリ (タブ) `{ id, name (LocalizedText), sortOrder, tags, itemIds }`。`itemIds` は表示順。1 つの商品が複数のカテゴリ (おすすめと本来のカテゴリ) に入ってよい |
| `items` | object[] | 商品 (下の表) |
| `optionGroups` | object[] | オプションの組 (下の表) |
| `tags` | object[] | タグ `{ code, name (LocalizedText) }` (`drink-bar`、`alcohol`、`kids`、`dessert`、`one-per-guest` など) |
| `rules` | object[] | タグに対するルール (下の表) |
| `allergens` | object[] | アレルギーの表示に使う原材料 `{ code, name (LocalizedText), isMandatory }`。特定原材料の 8 品目 (えび、かに、くるみ、小麦、そば、卵、乳、落花生) は `isMandatory` |
| `stations` | object[] | 持ち場 `{ id, name, sortOrder }` (キッチン、デザート、ドリンク) |
| `dayparts` | object[] | 時間帯 `{ code, name (LocalizedText), start, end }` (朝、ランチなど)。出せる条件のルールがコードで指す |

- `dayparts` の `start` と `end` は、店舗の現地時刻の `HH:mm`。  
  `start` ≤ 時刻 < `end` で、`end` が `start` より前なら日をまたぐ
- 時間帯で出す品と、子どもがいる来店だけに出す品は、タグと出せる条件のルール (`Availability`) で表す。  
  時刻に合わせてメニューを再公開しない (一日じゅう同じバージョン)

商品 (`MenuResponseItem`):

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(20) | 商品コード (POS と合わせる) |
| `name` / `description` | LocalizedText | |
| `price` | money | 税込 |
| `taxRate` | rate | `0.10` |
| `imageName` | string? | 写真 (`/images/{name}`) |
| `badges` | enum[] | `Recommended` / `Popular` / `New` / `Limited` |
| `spiceLevel` | int | 辛さ (`0`〜`3`) |
| `allergenCodes` | string[] | 含む原材料 |
| `calories` | int? | kcal |
| `tags` | string[] | タグ (ルールの対象を決める。`alcohol`、`drink-bar` など) |
| `stationId` | guid? | 作る持ち場。`null` は作らない品 |
| `servedBy` | enum | `Staff` (スタッフが運ぶ) / `Guest` (お客様が自分でとる。ドリンクバーなど) |
| `optionGroupIds` | guid[] | 選べるオプションの組 (表示順) |
| `maxQuantity` | int? | 1 明細の数量の上限 (店舗の上限より厳しくするとき) |
| `defaultTiming` | enum | 出すタイミングの既定 `Now` / `AfterMeal` (デザートは食後) |
| `timingSelectable` | bool | お客様が出すタイミング (すぐに / 食後に) を選べるか |

オプションの組 (`MenuResponseOptionGroup`):

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `name` | LocalizedText | 例: ソース、セット、焼き加減 |
| `minSelect` / `maxSelect` | int | 選ぶ数。`0` / `1` は任意の 1 つ、`1` / `1` は必須の 1 つ |
| `options` | object[] | `{ id, name (LocalizedText), priceDelta, isDefault, tags, allergenCodes }`。`priceDelta` は税込の差額 (`0` 以上) |

ルール (`MenuResponseRule`):

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `kind` | enum | `Suggestion` (提案) / `Confirmation` (確認) / `Limit` (上限) / `Availability` (出せる条件) |
| `targetTag` | string | 対象のタグ。商品かオプションにタグがあれば対象になる (出せる条件は、カテゴリのタグにも適用する) |
| `basis` | enum? | 提案で比べる人数 `Guests` / `Adults` / `Children` |
| `suggestItemIds` | guid[]? | 提案する商品 |
| `scope` | enum? | 数える範囲 `Order` (注文ごと) / `Visit` (来店で 1 回、来店の合計) / `Guest` (1 人あたり × 人数) |
| `max` | int? | 上限の数 |
| `message` | LocalizedText? | お客様に出す文言 (提案と確認) |
| `dayparts` | string[]? | 出せる時間帯のコード (出せる条件)。どれかの中なら出せる |
| `requiresChildren` | bool? | 子ども (小学生以下) が 1 人以上の来店だけに出す (出せる条件) |

| ルール | テーブル端末 | サーバ |
| --- | --- | --- |
| 提案 (`Suggestion`) | 注文の確認で、タグの品を誰かが頼んでいて人数より少なければ、提案する商品を 1 枠で出す | 何もしない |
| 確認 (`Confirmation`) | タグの品を入れる前に `message` で確認し、答えを記録する (`POST /visits/{id}/confirmations`) | 記録のない来店の注文は `422` (`CONFIRMATION_REQUIRED`) |
| 上限 (`Limit`) | 来店の注文とカートを合わせて上限を超えたら入れない | 超える注文は `422` (`LIMIT_EXCEEDED`) |
| 出せる条件 (`Availability`) | 条件を満たさない品とカテゴリを出さない。カートの品が満たさなくなったら、外すまで確定させない | 満たさない明細は `422` (`ITEM_UNAVAILABLE`)。時間帯の終わりから店舗の設定の分数 (`daypartGraceMinutes`) のうちに届いた注文は受ける |

- テーブル端末は、時刻の見直しと人数の変更で、出せる条件に合う品とカテゴリを出し直す。  
  カートに入れるときと確定するときにも、条件を確認する
- 数え方 (人数の取り方、足りない数、上限) は、端末とサーバで同じ計算 (`TableOrder.Domain.TagRules`) を使う

ルールの使い方の例:

| 例 | 表し方 |
| --- | --- |
| ドリンクバー | 単品の商品 (ドリンクバー、キッズドリンクバー) と料理のセットのオプション (セットドリンクバー) に `drink-bar` のタグを付ける。提案のルール (`basis` = `Guests`) で人数分を提案する |
| お酒 | `alcohol` のタグと確認のルール (`scope` = `Visit`) で、来店で 1 回だけ年齢を確認する |
| ランチ | `lunch` のタグと出せる条件のルール (`dayparts` = `["lunch"]`) |
| キッズメニュー | `kids` のタグと出せる条件のルール (`requiresChildren` = `true`) |

```jsonc
{
  "menuVersion": "2026-10-01T02:00:00.000Z-17",
  "categories": [
    { "id": "...", "name": { "ja": "ハンバーグ・ステーキ", "en": "Hamburg & Steak" }, "sortOrder": 2, "tags": [], "itemIds": ["..."] }
  ],
  "items": [
    {
      "id": "...", "code": "1012",
      "name": { "ja": "チーズインハンバーグ", "en": "Cheese-filled Hamburg Steak" },
      "price": 999, "taxRate": 0.10, "badges": ["Popular"],
      "allergenCodes": ["wheat", "egg", "milk"], "calories": 820, "tags": [],
      "stationId": "...", "servedBy": "Staff", "optionGroupIds": ["... (ソース)", "... (セット)", "... (ドリンク)"],
      "defaultTiming": "Now", "timingSelectable": false
    },
    {
      "id": "...", "code": "6001", "name": { "ja": "ドリンクバー", "en": "Drink Bar" }, "price": 459, "taxRate": 0.10,
      "tags": ["drink-bar"], "stationId": null, "servedBy": "Guest", "defaultTiming": "Now"
    }
  ],
  "optionGroups": [
    {
      "id": "...", "name": { "ja": "ドリンク", "en": "Drink" }, "minSelect": 0, "maxSelect": 1,
      "options": [
        { "id": "...", "name": { "ja": "セットドリンクバー", "en": "Drink Bar (set)" }, "priceDelta": 299, "tags": ["drink-bar"] }
      ]
    }
  ],
  "tags": [ { "code": "drink-bar", "name": { "ja": "ドリンクバー", "en": "Drink bar" } } ],
  "rules": [
    {
      "id": "...", "kind": "Suggestion", "targetTag": "drink-bar", "basis": "Guests", "suggestItemIds": ["... (ドリンクバー)", "... (キッズドリンクバー)"],
      "message": { "ja": "ドリンクバーを人数分にしますか？", "en": "Would you like drink bar for everyone?" }
    },
    { "id": "...", "kind": "Confirmation", "targetTag": "alcohol", "scope": "Visit", "message": { "ja": "20 歳以上で、お車を運転されない方のご注文ですか？" } },
    { "id": "...", "kind": "Limit", "targetTag": "one-per-guest", "scope": "Guest", "max": 1 }
  ],
  "allergens": [ { "code": "wheat", "name": { "ja": "小麦", "en": "Wheat" }, "isMandatory": true } ],
  "stations": [ { "id": "...", "name": "キッチン", "sortOrder": 1 } ]
}
```

### 2.4 品切れ (Stock)

商品とオプションごとに、売れるかどうかと残りの数を持つ。

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `targetId` | guid | 商品かオプションの `id` |
| `targetKind` | enum | `Item` / `Option` |
| `status` | enum | `Available` / `Limited` (残りの数がある) / `SoldOut` |
| `remaining` | int? | 残りの数 (`Limited` のときだけ) |
| `updatedAt` | datetime | |

| Method | Path | 利用者 | 概要 |
| --- | --- | --- | --- |
| GET | `/stock` | テーブル / ホール / キッチン | `Available` でないものの一覧 (`StockListResponse`) |
| PUT | `/stock/{targetId}` | ホール / キッチン | 品切れ・残りの数の設定 `StockUpdateRequest { targetKind, status, remaining? }` → `204`。通知 `stock.updated` |
| POST | `/stock/reset` | ホール | すべて `Available` に戻す (営業日の始めなど) → `204`。通知 `stock.updated` (戻した品) |

- `PUT /stock/{targetId}`
  - `Limited` の残りの数は `0` から `9999`。  
    `0` は `SoldOut` にする
  - 今のメニューにない品は `404`
  - 同じ設定の再送では、`stock.updated` を送らない
- `Limited` の残りの数は、注文を受けるたびにサーバが減らす。  
  `0` になったら `SoldOut` にする
- 品切れの品の注文は `422` (`ITEM_SOLD_OUT`)、残りの数を超える注文は `422` (`STOCK_INSUFFICIENT`)

### 2.5 来店 (Visits)

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | 来店を開いた端末 (管理画面の案内はサーバ) が採番 |
| `tableId` / `tableName` | guid / string | |
| `adults` / `children` | int | 大人 / 子ども (小学生以下)。合わせて 1 以上 |
| `status` | enum | `Open` / `Paying` / `Closed` / `Cancelled` ([来店の状態](business.md#32-来店の状態)) |
| `openedBy` | enum | `Hall` (スタッフ。ホール端末と管理画面の案内) / `Reception` (受付機) / `Table` (テーブル端末) |
| `openedAt` / `closedAt` | datetime | |
| `closedBy` | enum? | `TablePayment` (テーブルで払った) / `Register` (レジで払った) / `Hall` |
| `businessDate` | date | |
| `confirmedRuleIds` | guid[] | 答えた確認のルール (お酒の年齢の確認など) |
| `orderTotal` | money | 注文の合計 (取消を除く) |
| `version` | int | |

| Method | Path | 利用者 | 概要 |
| --- | --- | --- | --- |
| POST | `/visits` | ホール / 受付 / テーブル | 来店の開始 `VisitCreateRequest { id, tableId?, adults, children }` → `201`。通知 `visit.opened` |
| GET | `/visits/{id}` | テーブル (自分の来店) / ホール / 外部 (POS) | 来店。テーブル端末からほかのテーブルの来店は `403` (`DEVICE_SCOPE`) |
| GET | `/devices/me/visit` | テーブル | 自分のテーブルの今の来店。なければ `204` (待受にする) |
| PATCH | `/visits/{id}` | ホール | 人数の変更 `VisitUpdateRequest { adults, children, version }` (会計中も変更できる)。通知 `visit.updated` |
| POST | `/visits/{id}/move` | ホール | テーブルの移動 `{ toTableId, version }`。通知 `visit.moved` と、開いているチケットの `ticket.updated` |
| POST | `/visits/{id}/confirmations` | テーブル / ホール | 確認のルールに答えた記録 `VisitConfirmationRequest { ruleId }` → `200` (来店)。通知 `visit.updated` (新しく記録したとき) |
| POST | `/visits/{id}/close` | ホール / 外部 (POS) | テーブルの外で会計した来店 (レジで払ったなど) を終える `{ closedBy, version, staffId? }`。通知 `visit.closed`、`payment.updated`、`call.updated` |
| POST | `/visits/{id}/cancel` | ホール | 注文のないまま帰った来店の取りやめ `{ version }`。通知 `visit.closed` (`status` は `Cancelled`)、`call.updated` |

来店の開始 (`POST /visits`) は、端末によって次のように扱う。

| 端末 | 開けるとき | テーブル |
| --- | --- | --- |
| ホール端末 | いつでも | `tableId` を送る |
| 受付機 | 来店の開き方 (`features.visitOpening`) で許したときだけ | `tableId` を送らない (送ると `400`)。サーバが選ぶ |
| テーブル端末 | 来店の開き方で許したときだけ | 自分のテーブル (トークンで決まる)。ほかのテーブルを送ると `403` `DEVICE_SCOPE` |

- 来店の開き方で許していない端末は `403` (`VISIT_OPENING_DISABLED`)
- 受付機の来店では、サーバが人数の入る空席のうち、定員の小さいテーブルを選ぶ (同じなら並びの順)。  
  空席がなければ `409` (`NO_VACANT_TABLE`)、ラストオーダーを過ぎていれば `422` (`LAST_ORDER_PASSED`)
- テーブルに `Open` / `Paying` の来店があれば `409` (`TABLE_OCCUPIED`)
- 同じ `id` の再送は、同じテーブル (受付機は同じ端末) なら開いた来店を返す
- `visit.opened` で、テーブル端末は待受から注文の画面になる

ほかの操作:

- `POST /visits/{id}/move`
  - 移動先に来店があれば `409` (`TABLE_OCCUPIED`)、会計中は `422` (`CHECKOUT_IN_PROGRESS`)
  - `visit.moved` で、元のテーブル端末は待受に、移動先は注文の画面になる
  - `ticket.updated` で、キッチン端末は今のテーブルを出す
- `POST /visits/{id}/confirmations`
  - ホール端末は、代わりの注文でスタッフがお客様に確認したときに記録する
  - 来店で 1 回だけ記録する。  
    確認のルールでない `ruleId` は `400`
  - `visit.updated` は、ほかの端末が同じ来店で聞き直さないように送る
- `POST /visits/{id}/close`
  - `closedBy` は `Register` / `Hall`。  
    `TablePayment` は、テーブルで払い終えたときにサーバが付ける
  - 会計中の来店の待っている支払はやめる (あとで届いた結果で払い終えない)
  - 終わっていない呼び出しは終える
- `POST /visits/{id}/cancel`
  - 取消を除いた明細があれば `422` (`VISIT_HAS_ORDERS`)、会計中は `422` (`CHECKOUT_IN_PROGRESS`)
  - 終わっていない呼び出しは終える

### 2.6 注文 (Orders)

1 回の「注文を確定する」で送る明細の束を注文とする。  
明細は調理と提供の状態を持つ。

注文 (`OrderListResponseItem`):

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | 送った端末が採番 |
| `visitId` | guid | |
| `orderNo` | int | 来店の中の通し番号 (1、2、...) |
| `source` | enum | `Table` / `Hall` (スタッフが代わりに入れた) |
| `deviceId` | guid | |
| `orderedAt` | datetime | |
| `amount` | money | 明細の合計 (取消を除く) |
| `lines` | object[] | 明細 (下の表) |

明細 (`OrderListResponseLine`):

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | 送った端末が採番 |
| `itemId` | guid | |
| `name` | LocalizedText | 注文したときの名前 (メニューが変わっても履歴の表示を変えない) |
| `options` | object[] | `{ optionGroupId, optionId, name (LocalizedText), priceDelta }` |
| `quantity` | int | |
| `unitPrice` | money | 税込。商品の価格 + オプションの差額 |
| `amount` | money | `unitPrice × quantity` |
| `taxRate` | rate | |
| `timing` | enum | `Now` / `AfterMeal` (食後) |
| `status` | enum | `Held` / `Ordered` / `Cooking` / `Ready` / `Served` / `Cancelled` (下の図) |
| `stationId` | guid? | |
| `servedAt` / `cancelledAt` | datetime? | |
| `cancelReason` | string(100)? | |

```
Held (食後まで止めている) --お願いする--> Ordered --作り始め--> Cooking --できあがり--> Ready --提供--> Served
お客様がとる品 (ドリンクバー) は注文を受けたときに Served、作らない品 (持ち場なし) でスタッフが運ぶものは Ready にする
Held は、お客様かホールがお願いするほか、会計を始めたときに残りをすべてお願いする
Held / Ordered / Cooking / Ready --取消 (ホール)--> Cancelled
```

`OrderCreateRequest`:

```jsonc
{
  "id": "0192c3e0-...",                  // 端末が採番 (GUID v7)。再送しても重複しない
  "menuVersion": "2026-10-01T02:00:00.000Z-17",   // 表示していたメニュー
  "lines": [
    { "id": "0192c3e1-...", "itemId": "... (チーズインハンバーグ)", "optionIds": ["... (デミグラス)", "... (ライス・スープ)", "... (セットドリンクバー)"], "quantity": 2, "unitPrice": 1628, "timing": "Now" },
    { "id": "0192c3e2-...", "itemId": "... (いちごパフェ)", "optionIds": [], "quantity": 1, "unitPrice": 699, "timing": "AfterMeal" }
  ]
}
```

| Method | Path | 利用者 | 概要 |
| --- | --- | --- | --- |
| POST | `/visits/{visitId}/orders` | テーブル / ホール | 注文の送信 `OrderCreateRequest` → `201` (`OrderListResponseItem`)。同じ `id` の再送は `200` (内容が違えば `409` `DUPLICATE_ID_MISMATCH`)。通知 `order.created`、`ticket.created`、`stock.updated` (残りの数を減らした品) |
| GET | `/visits/{visitId}/orders` | テーブル / ホール | 来店の注文 (`OrderListResponse`)。注文履歴と明細の状態 |
| POST | `/visits/{visitId}/orders/release` | テーブル / ホール | 食後の品をお願いする `OrderReleaseRequest { lineIds }` (空ならすべて) → `200` (`OrderListResponse`)。`Held` を `Ordered` にする。通知 `order.lines.updated`、`ticket.created` |
| POST | `/orders/{orderId}/lines/{lineId}/cancel` | ホール | 取消 `OrderLineCancelRequest { quantity, reason?, staffId? }` → `200` (`OrderListResponseItem`)。通知 `order.lines.updated`、`ticket.updated` |

- `POST /orders/{orderId}/lines/{lineId}/cancel`
  - 数量の一部の取消は、明細を分けて取り消す
  - `Served` の明細は `422` (`LINE_STATUS_INVALID`)、会計中は `422` (`CHECKOUT_IN_PROGRESS`)
  - 取消で残りの数は戻さない
- 注文の取消はお客様にはさせない (テーブル端末からは呼び出しでスタッフに頼む)

注文を受けるときに、サーバは次のことを検証する (エラーはどれも `422`)。

| 検証すること | `errorCode` |
| --- | --- |
| 来店が `Open` か | `Paying` は `CHECKOUT_IN_PROGRESS`、終わった来店は `VISIT_NOT_OPEN` |
| 店舗が一時停止していないか | `ORDERING_PAUSED` |
| ラストオーダーを過ぎていないか | `LAST_ORDER_PASSED` |
| 表示していた単価が今のメニューと同じか、商品・オプションが今のメニューにあるか | `MENU_CHANGED` |
| オプションの数と組み合わせ | `OPTION_INVALID` |
| 数量と明細の数の上限 | `QUANTITY_EXCEEDED` |
| 出せる条件のルールの対象の明細: 受けた時刻 (店舗のタイムゾーン) が時間帯のどれかの中か、来店に子どもがいるか | `ITEM_UNAVAILABLE` |
| 品切れ・残りの数 | `ITEM_SOLD_OUT` / `STOCK_INSUFFICIENT` |
| 確認のルールの対象の品: 来店の記録があるか | `CONFIRMATION_REQUIRED` |
| 上限のルール: 来店のこれまでの注文と合わせた数 | `LIMIT_EXCEEDED` |

- `MENU_CHANGED` のときは、端末がメニューを再取得して、確認し直してもらう。  
  `menuVersion` が違っても、価格と内容が同じなら受ける
- 時間帯は、終わりから `daypartGraceMinutes` の分数のうちは受ける。  
  同じ `id` の再送は、時間帯によらず受けた注文を返す
- 確認のルールは、`scope` によらず来店の記録で確認する。  
  注文ごとに確認するのは端末
- 上限のルールは、`scope` が `Order` なら注文の中だけで数える
- 検証の順は、メニュー (`MENU_CHANGED`、`OPTION_INVALID`、`QUANTITY_EXCEEDED`)、出せる条件 (`ITEM_UNAVAILABLE`)、品切れ、ルール。  
  先に見つけた種類の誤りを、`errors` に明細の `id` ごとに返す

### 2.7 キッチン (Kitchen)

注文の明細を持ち場ごとに分けたものをチケットとし、キッチン端末に出す。

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `stationId` | guid | |
| `orderId` / `visitId` | guid | |
| `tableName` | string | 来店の今のテーブル |
| `orderNo` | int | |
| `createdAt` | datetime | チケットができた時刻 (食後の品はお願いされた時刻) |
| `status` | enum | `Open` / `Done` (下げた) |
| `lines` | object[] | `{ lineId, name (日本語), options (日本語), quantity, status }` |

| Method | Path | 利用者 | 概要 |
| --- | --- | --- | --- |
| GET | `/kitchen/tickets?stationId&status` | キッチン | チケットの一覧 (`KitchenTicketListResponse`) |
| POST | `/kitchen/tickets/{id}/lines/{lineId}/start` | キッチン | 作り始め (`Cooking`) → `200` (チケット) |
| POST | `/kitchen/tickets/{id}/lines/{lineId}/ready` | キッチン | できあがり (`Ready`) → `200` (チケット) |
| POST | `/kitchen/tickets/{id}/bump` | キッチン | すべての明細をできあがりにして下げる (`Done`) → `200` (チケット) |
| POST | `/kitchen/tickets/{id}/recall` | キッチン | 下げたチケットを戻す (押し間違い) → `200` (チケット)。まだ出していない明細は `Cooking` に戻す |

- `GET /kitchen/tickets`
  - `status` の既定は `Open` で、古い順。  
    `Done` は、下げた新しい順に直近のもの
  - `stationId` を省くと、受け持つすべての持ち場。  
    受け持たない持ち場は `403` (`DEVICE_SCOPE`)
- 明細の状態が変わると、`order.lines.updated` を送る。  
  テーブル端末の注文履歴と、ホール端末の提供の一覧に出す
- チケットが変わると、`ticket.updated` を送る
- すでにその状態の操作 (再送) は、変えずにチケットを返す
- 状態に合わない操作 (できあがった明細の作り始め) は `422` (`LINE_STATUS_INVALID`)

### 2.8 提供 (Serving)

| Method | Path | 利用者 | 概要 |
| --- | --- | --- | --- |
| GET | `/serving?status` | ホール | 提供を待つ明細 (`ServingListResponse`)。テーブルごとに、できあがりの古い順 |
| POST | `/serving/serve` | ホール | 提供した `ServeRequest { lineIds, staffId? }` → `204` (`Served`)。通知 `order.lines.updated`、`ticket.updated` |

- `GET /serving`
  - `status` は `Ordered` / `Cooking` / `Ready` で、既定は `Ready`
  - 払い終えて閉じた来店の品も、その営業日のうちは出す (`visitStatus` が `Closed`。ホール端末は「会計済み」を出す)
- `POST /serving/serve`
  - できあがりの前の品と、払い終えて閉じた来店の品も提供済みにできる
  - 食後まで止めている品と取消は `422` (`LINE_STATUS_INVALID`)

### 2.9 呼び出し (Calls)

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | テーブル端末が採番 |
| `visitId` / `tableId` / `tableName` | | 来店と、来店の今のテーブル |
| `reasonCode` | string(20) | 用件 (下の表) |
| `status` | enum | `Open` / `Acknowledged` (向かっている) / `Done` |
| `createdAt` / `acknowledgedAt` / `doneAt` | datetime | |

用件の例 (店舗の設定で選ぶ):

| `reasonCode` | 用件 |
| --- | --- |
| `Staff` | 店員を呼ぶ |
| `Water` | お水 |
| `Plates` | 取り皿 |
| `Cutlery` | スプーン・フォーク |
| `KidsTableware` | 子ども用の食器 |
| `Clear` | お皿を下げる |
| `Payment` | 会計の相談 |

| Method | Path | 利用者 | 概要 |
| --- | --- | --- | --- |
| POST | `/visits/{visitId}/calls` | テーブル | 呼び出し `{ id, reasonCode }` → `201`。通知 `call.created` |
| GET | `/visits/{visitId}/calls` | テーブル | 来店の呼び出しと状態 (古い順) |
| GET | `/calls?status` | ホール | 呼び出しの一覧 (古い順) |
| POST | `/calls/{id}/acknowledge` | ホール | 向かう (`Acknowledged`) → `200` (呼び出し)。テーブル端末に「スタッフが向かっています」と出す。通知 `call.updated` |
| POST | `/calls/{id}/done` | ホール | 対応した (`Done`) → `200` (呼び出し)。通知 `call.updated` |

- `POST /visits/{visitId}/calls`
  - 同じ用件の終わっていない (`Open` / `Acknowledged`) 呼び出しがあれば、`200` でそれを返す (続けて押しても増やさない)
  - 店舗にない用件は `400`、終わった来店は `422` (`VISIT_NOT_OPEN`)
- `GET /calls`
  - `status` の既定は `Open` と `Acknowledged`。  
    `Done` は、終わった新しい順に直近のもの
- すでに進んでいる呼び出しへの操作 (ホール端末どうしで同時に押した) は、変えずに呼び出しを返す

### 2.10 会計 (Bill / Payments)

テーブル端末で明細を確認し、QR コード決済かクレジットカードで払う。  
決済の処理そのものは、決済サービスが行う。  
注文サーバは支払の開始と結果を記録し、支払が揃ったら来店を終える。

会計 (`BillResponse`):

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `visitId` | guid | |
| `billVersion` | string | 明細が変わると変わる |
| `lines` | object[] | `{ name, options, quantity, unitPrice, amount, taxRate }` (同じ商品・オプションはまとめる。取消を除く) |
| `taxes` | object[] | 税率ごとの `{ rate, taxableAmount, taxAmount }` (内税) |
| `total` | money | 税込の合計 |
| `paidAmount` / `balance` | money | 払った額 / 残り |
| `guests` | int | 大人 + 子ども |
| `splitAmounts` | money[] | 人数で割った目安 ([金額と税](business.md#-5-金額と税)) |
| `hasUnservedLines` | bool | まだ出していない品がある (お支払いのあともお持ちすることを出す) |

支払 (`PaymentResponse`):

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | テーブル端末が採番 |
| `method` | enum | `QrCode` / `CreditCard` |
| `amount` | money | |
| `status` | enum | `Pending` / `Completed` / `Failed` / `Cancelled` |
| `qrCode` | string? | 店舗が見せる QR の内容 (`QrCode` のとき。サーバが決済サービスから受け取る) |
| `expiresAt` | datetime? | QR の期限 |
| `completedAt` | datetime? | |

| Method | Path | 利用者 | 概要 |
| --- | --- | --- | --- |
| GET | `/visits/{visitId}/bill` | テーブル / ホール / 外部 (POS) | 会計の明細と合計 (`BillResponse`) |
| POST | `/visits/{visitId}/checkout` | テーブル / ホール | 会計を始める `CheckoutRequest { billVersion, version }` → `200` (来店)。通知 `visit.updated` |
| POST | `/visits/{visitId}/checkout/cancel` | テーブル / ホール | 会計をやめる (本文なし) → `200` (来店)。通知 `visit.updated`、`payment.updated` |
| POST | `/visits/{visitId}/payments` | テーブル | 支払を始める `PaymentCreateRequest { id, method, amount }` → `201` (`PaymentResponse`)。通知 `payment.updated` |
| POST | `/payments/{id}/result` | テーブル | 決済端末で払った結果 `PaymentResultRequest { status, provider, providerReference, failureReason? }` (`CreditCard`) → `200` (`PaymentResponse`) |
| POST | `/payments/callbacks/{provider}` | 外部 (決済サービス) | 決済の結果の通知 (`QrCode`)。署名を検証する |
| GET | `/payments/{id}` | テーブル | 支払の状態 (通知を受信できなかったとき) |
| POST | `/payments/{id}/cancel` | テーブル | 待っている支払をやめる (`Pending` だけ。ほかは変えずに返す) |
| GET | `/visits/{visitId}/receipt` | テーブル | 領収の内容と電子レシートの URL (`ReceiptResponse`。画面に QR で出す) |

- `POST /visits/{visitId}/checkout`
  - 来店を `Paying` にし、お願いしていない食後の品をお願いする。  
    会計中なら、変えずに返す
  - `billVersion` が違えば `422` (`BILL_CHANGED`)
  - `visit.updated` で、ホールの席の一覧に「会計中」を出す
  - 食後の品をお願いしたら、`order.lines.updated`、`ticket.created` も送る
- `POST /visits/{visitId}/checkout/cancel`
  - 待っている支払をやめて、`Open` に戻す
  - 払い終えた支払があれば、会計中のまま返す
- `POST /visits/{visitId}/payments`
  - `QrCode` は `qrCode` を返す。  
    `CreditCard` は、テーブルの決済端末で払う
  - 会計を始める前は `422` (`VISIT_NOT_OPEN`)
- `POST /payments/callbacks/{provider}`
  - 今は仮の決済サービス (`fake`。`PaymentCallbackRequest { providerReference, status }`) だけ。  
    開発の環境だけで受ける
- `GET /visits/{visitId}/receipt`
  - 電子レシートを出す店で、テーブルで払い終えて来店を閉じたあとに返す。  
    ほかは `404`
  - URL は、リクエストを受けた接続先の `/receipts/{token}` (サーバが配信する電子レシートの画面)

支払と来店の終わり:

- 支払は残りを超えない (`422` `PAYMENT_AMOUNT_INVALID`)。  
  残りは、`balance` から期限内の待っている支払も引いた額
- 割り勘は、目安の額で支払を何回かに分けて行う
- 支払が揃う (`balance` = 0) と、来店を `Closed` (`closedBy` = `TablePayment`) にして `visit.closed` を送る。  
  終わっていない呼び出しも終える
- テーブル端末は、お礼と電子レシートの QR を出し、待受に戻る
- 現金はテーブルで扱わない。  
  テーブル端末は「レジでお支払いください」と出す
- レジ (POS) かホール端末が、`POST /visits/{id}/close` で来店を終える

---

## 📡 3. リアルタイム通知

### 3.1 接続

- 端末は、WebSocket (`/hubs/store`。ASP.NET Core SignalR) にアクセストークンで接続する。  
  ヘッダを付けられないブラウザは、`access_token` のクエリで送る
- サーバは、トークンの店舗の通知のうち、端末の種類と置き場所に合うものだけを送る
- サーバはトークンの期限が来た接続を切り、端末は新しいトークンで再接続する。  
  無効にした端末と止めたテナントの接続は、すぐに切る
- リクエストを gRPC にするときは、同じ通知を gRPC のサーバストリームでも出す
- 端末の状態 (電池、アプリのバージョン) は、`POST /devices/me/heartbeat` で送る。  
  接続しているかは、ハブの接続で見る
- テレメトリ (ログ、メトリクス、トレース) は、OpenTelemetry (OTLP) で送る

通知の形と順番:

- 通知は、ハブのメソッド `event` で `{ "seq": 1234, "type": "visit.opened", "occurredAt": "...", "data": { ... } }` (`EventListResponseItem`) を送る
- `seq` は、店舗の中で増える通し番号。  
  通知は `seq` の順に送る
- 同じ通知が 2 回届くことがあるので、端末は最後に受けた `seq` 以前の通知を捨てる

接続したとき:

- サーバは、接続した端末を配信のグループに入れ終えたら、`ready` で店舗の今の `seq` を送る
- はじめて接続した端末は、その `seq` から数える。  
  そのあとに、今の状態 (店舗、メニュー、来店など) を読む
- 再接続した端末は、`ready` を受けてから `GET /events?after={最後に受けた seq}` で欠落した通知 (`EventListResponse { lastSeq, items }`) を受け取る。  
  その間に届いた通知は、欠落した通知のあとに `seq` の順に扱う

`GET /events` は、次のときに `410` (`EVENTS_EXPIRED`) を返す。  
端末は、今の状態を再取得する。

- 通し番号が、店舗の今の番号より先 (データベースを作り直した)
- 残している範囲 (24 時間) より前
- 欠落が 1000 件を超える

### 3.2 通知の種類

| `type` | 送る先 | `data` | 受けた端末の動き |
| --- | --- | --- | --- |
| `visit.opened` | そのテーブル端末、ホール、受付 | 来店 | テーブル端末は待受から注文の画面にする。受付機は空席から外す |
| `visit.updated` | そのテーブル端末、ホール | 来店 | 人数・状態 (会計中)・答えた確認を更新する。テーブル端末は、会計中の間は注文の確定を止める |
| `visit.moved` | 元と移動先のテーブル端末、ホール、受付 | `{ visit, fromTableId, fromTableName }` | 元は待受に、移動先は注文の画面にする。受付機は空席を更新する |
| `visit.closed` | そのテーブル端末、ホール、受付 | 来店 (取りやめは `status` が `Cancelled`) | テーブル端末は待受に戻る (お会計の画面ならお礼を出してから)。受付機は空席に戻す |
| `order.created` | ホール、そのテーブル端末 | 注文 (`OrderListResponseItem`) | 席の一覧と注文履歴に足す |
| `order.lines.updated` | そのテーブル端末、ホール | `{ visitId, orders }` (明細の状態が変わった注文を、すべての明細と一緒に) | 注文履歴 (調理中、お持ちします) と提供の一覧を更新する |
| `ticket.created` / `ticket.updated` | その持ち場のキッチン端末 | チケット (`KitchenTicketListResponseItem`) | キッチンの表示を更新する |
| `call.created` / `call.updated` | ホール、そのテーブル端末 | 呼び出し (`CallListResponseItem`) | ホールは音を鳴らす。テーブル端末は「向かっています」を出す |
| `stock.updated` | テーブル、ホール、キッチン | 変わった品 `{ items }` (売れるように戻した品は `Available`) | 売り切れの表示を更新する |
| `menu.published` | テーブル、ホール、キッチン | `menuVersion` | すぐにメニューと品切れを再取得する。テーブル端末 (来店中も) とホール端末は、カートの使えなくなった品 (なくなった品、価格の変わった品) を外してメッセージを出す |
| `store.updated` | 全端末 | 店舗 | 一時停止の表示とラストオーダーを更新する。設定のバージョンが変わったら、テーブル端末は待受のときに起動からやり直す |
| `device.updated` | 全端末 | `{ deviceId }` | その端末だけが起動からやり直す (置き場所・名前の変更、無効化) |
| `payment.updated` | そのテーブル端末、ホール | 支払 (`PaymentResponse`) | QR の支払の完了を画面に出す |

### 3.3 外部への通知 (Webhook)

本部・POS へは、来店の明細と支払を渡すために Webhook を送る。

| イベント | 内容 |
| --- | --- |
| `order.created` | 注文 (厨房の外の集計や POS の伝票のため) |
| `visit.closed` | 来店、会計の明細、支払 (POS の売上として取り込む) |
| `stock.updated` | 品切れ (他のチャネルの販売を止めるため) |

配信の仕様:

| 項目 | 仕様 |
| --- | --- |
| 送り先 | テナントごとに管理画面で登録する (店舗に限ることもできる) |
| 鍵 | サーバが生成し、登録した画面で一度だけ見せる |
| 本文 | `{ id, type, occurredAt, tenantId, storeId, storeCode, seq, data }`。`id` は配信の Id、`data` は §3.2 の通知の中身 |
| `visit.closed` の本文 | 来店に、会計の明細と支払を足す |
| 署名 | `X-Signature: t={送った時刻の Unix 秒},v1={HMAC-SHA256(鍵, "{t}.{本文}") の 16 進}` を付ける。受け取る側は、時刻が 5 分より古いものを捨てる |
| リトライ | `2xx` を受けるまで、1 分から倍にして 1 時間までの間隔で 24 時間リトライする (1 回のタイムアウトは 10 秒)。過ぎたらあきらめる |
| 再送 | あきらめた配信は、管理画面で再送できる |
| 順番 | 保たない (リトライで前後する)。受け取る側は `seq` で重複を捨てる |

---

## 📲 4. 端末ごとの API

URL はリソースごとに 1 つにし、端末の種類ごとに使える範囲を認可で絞る ([§1.5](#15-認証認可))。

端末のアプリの API クライアント:

| インターフェース | 持つもの |
| --- | --- |
| `ITableApi`、`IHallApi`、`IKitchenApi`、`IReceptionApi` | 端末の種類ごとに分け、それぞれが使う API だけを持つ |
| `IDeviceApi` | すべての端末に共通。端末の登録・トークン・状態の報告・端末の設定 |
| `IOrderEvents` | 通知の受信。すべての端末に共通にし、受ける通知は送る先で絞る ([§3.2](#32-通知の種類)) |

- テナントと店舗はトークンで決まるので、URL に入れない ([§1.6](#16-テナント))
- 端末ごとに URL を分けない (同じ業務の処理をエンドポイントごとに作らない)
- テーブル端末も、来店の `id` をパスに入れる (来店が変わったあとに前の来店の再送が届いても、新しい来店に入れない)

### 4.1 一覧

端末の種類ごとに使える API:

| API | テーブル | ホール | キッチン | 受付 | 外部 |
| --- | :---: | :---: | :---: | :---: | :---: |
| `POST /devices/pair`、`POST /devices/token`、`POST /devices/me/heartbeat`、`GET /devices/me/config` | ✅ | ✅ | ✅ | ✅ | |
| `POST /oauth/token` | | | | | ✅ 本部・POS |
| `GET /store` | ✅ | ✅ | ✅ | ✅ | |
| `PUT /store/ordering` | | ✅ | | | |
| `GET /tables` | | ✅ | | ✅ | ✅ POS |
| `GET /menu`、`GET /stock` | ✅ | ✅ | ✅ | | |
| `GET /images/{name}` | ✅ | ✅ | ✅ | ✅ | |
| `PUT /images/{name}`、`POST /menu/publications` | | | | | ✅ 本部 |
| `PUT /stock/{targetId}` | | ✅ | ✅ | | |
| `POST /stock/reset` | | ✅ | | | |
| `GET /devices/me/visit` | ✅ | | | | |
| `POST /visits/{id}/confirmations` | ✅ | ✅ | | | |
| `POST /visits` | ✅ 来店の開き方が席のとき | ✅ | | ✅ 来店の開き方が受付機のとき | |
| `GET /visits/{id}` | ✅ 自分の来店 | ✅ | | | ✅ POS |
| `PATCH /visits/{id}`、`POST /visits/{id}/move`、`POST /visits/{id}/cancel` | | ✅ | | | |
| `POST /visits/{id}/close` | | ✅ | | | ✅ POS |
| `POST /visits/{id}/orders`、`GET /visits/{id}/orders`、`POST /visits/{id}/orders/release` | ✅ | ✅ | | | |
| `POST /orders/{orderId}/lines/{lineId}/cancel` | | ✅ | | | |
| `GET /kitchen/tickets`、`POST /kitchen/tickets/{id}/...` | | | ✅ | | |
| `GET /serving`、`POST /serving/serve` | | ✅ | | | |
| `POST /visits/{id}/calls`、`GET /visits/{id}/calls` | ✅ | | | | |
| `GET /calls`、`POST /calls/{id}/acknowledge`、`POST /calls/{id}/done` | | ✅ | | | |
| `GET /visits/{id}/bill` | ✅ | ✅ | | | ✅ POS |
| `POST /visits/{id}/checkout`、`POST /visits/{id}/checkout/cancel` | ✅ | ✅ | | | |
| `POST /visits/{id}/payments`、`POST /payments/{id}/result`、`GET /payments/{id}`、`POST /payments/{id}/cancel`、`GET /visits/{id}/receipt` | ✅ | | | | |
| `POST /payments/callbacks/{provider}` | | | | | ✅ 決済 |
| `GET /events`、`/hubs/store` | ✅ | ✅ | ✅ | ✅ | |

テーブル端末とキッチン端末は、自分の置き場所 (テーブル、持ち場) の範囲だけを使える。

### 4.2 テーブル端末 (`ITableApi`)

| 場面 | API | 受ける通知 |
| --- | --- | --- |
| 登録 (一度だけ) | `POST /devices/pair` | |
| 起動 | `POST /devices/token`、`GET /devices/me/config`、`/hubs/store` (`ready` を待つ)、`GET /store`、`GET /menu` (変わっていなければ `304`)、`GET /stock`、`GET /images/{name}` (まだキャッシュしていない写真)、`GET /devices/me/visit`、`GET /visits/{id}/orders` (来店の途中なら) | |
| 待受 | `POST /visits` (来店の開き方が席の店。人数を入れて始める) | `visit.opened` (注文の画面へ)、`store.updated` (設定のバージョンが変わったら起動からやり直す) |
| 注文 | `POST /visits/{id}/confirmations`、`POST /visits/{id}/orders` | `stock.updated`、`menu.published`、`store.updated` (一時停止、ラストオーダー)、`visit.updated` |
| 注文履歴 | `GET /visits/{id}/orders`、`POST /visits/{id}/orders/release` | `order.created` (ホールの代わりの注文)、`order.lines.updated` |
| 店員呼出 | `POST /visits/{id}/calls`、`GET /visits/{id}/calls` | `call.updated` (向かっています) |
| お会計 | `GET /visits/{id}/bill`、`POST /visits/{id}/checkout`、`POST /visits/{id}/checkout/cancel`、`POST /visits/{id}/payments`、`POST /payments/{id}/result` (カード)、`GET /payments/{id}`、`POST /payments/{id}/cancel`、`GET /visits/{id}/receipt` | `payment.updated`、`visit.updated` (人数)、`visit.closed` (お礼と電子レシート) |
| 来店の終わり | | `visit.closed` (待受へ)、`visit.moved` (移った先は注文の画面、元は待受) |
| 定期 | `POST /devices/me/heartbeat` (1 分ごと)、`POST /devices/token` (期限の前) | |
| 端末の管理 | | `device.updated` (置き場所の変更と無効化。起動からやり直す) |
| 再接続 | `GET /events?after=` (欠落した通知) | |

- 注文を送れないときはカートを残し、同じ `id` で再送する (送れたかわからないまま新しい `id` にしない)
- 確認のルールの品は、カートに入れる前に確認し、`POST /visits/{id}/confirmations` で記録する

### 4.3 ホール端末 (`IHallApi`)

| 場面 | API | 受ける通知 |
| --- | --- | --- |
| 登録 (一度だけ) | `POST /devices/pair` | |
| 起動 | `POST /devices/token`、`GET /devices/me/config`、`GET /store`、`GET /menu`、`GET /stock`、`GET /tables`、`GET /calls`、`GET /serving`、`/hubs/store` | |
| 席の一覧 | `GET /tables` | `visit.opened` / `visit.updated` / `visit.moved` / `visit.closed`、`order.created`、`call.created` / `call.updated` |
| 案内 | `POST /visits` (テーブルと人数) | |
| 来店の操作 | `GET /visits/{id}`、`PATCH /visits/{id}` (人数)、`POST /visits/{id}/move`、`POST /visits/{id}/cancel`、`POST /visits/{id}/close` (レジで払った) | |
| 代わりの注文 | `GET /visits/{id}/orders`、`POST /visits/{id}/confirmations` (スタッフがお客様に聞いた確認のルール)、`POST /visits/{id}/orders`、`POST /visits/{id}/orders/release` | `order.created` |
| 取消 | `POST /orders/{orderId}/lines/{lineId}/cancel` | |
| 提供 | `GET /serving`、`POST /serving/serve` | `order.lines.updated` (できあがり) |
| 呼び出し | `GET /calls`、`POST /calls/{id}/acknowledge`、`POST /calls/{id}/done` | `call.created` (音を鳴らす) |
| 会計の手伝い | `GET /visits/{id}/bill`、`POST /visits/{id}/checkout`、`POST /visits/{id}/checkout/cancel` | `payment.updated` |
| 品切れと一時停止 | `PUT /stock/{targetId}`、`POST /stock/reset`、`PUT /store/ordering` | `stock.updated`、`store.updated` |
| 定期と再接続 | `POST /devices/me/heartbeat`、`POST /devices/token`、`GET /events?after=` | |

### 4.4 キッチン端末 (`IKitchenApi`)

キッチン端末は、サーバが配信する Web アプリ (`TableOrder.KitchenApp`)。  
受け持つ持ち場は、端末の記録で決める。

| 場面 | API | 受ける通知 |
| --- | --- | --- |
| 登録 (一度だけ) | `POST /devices/pair` | |
| 起動 | `POST /devices/token`、`GET /devices/me/config` (受け持つ持ち場)、`GET /store`、`GET /menu` (品切れにする品)、`GET /stock`、`GET /kitchen/tickets?stationId=`、`/hubs/store` | |
| 調理 | `POST /kitchen/tickets/{id}/lines/{lineId}/start`、`POST /kitchen/tickets/{id}/lines/{lineId}/ready`、`POST /kitchen/tickets/{id}/bump` | `ticket.created`、`ticket.updated` (取消) |
| 押し間違い | `GET /kitchen/tickets?stationId=&status=Done` (下げたチケット)、`POST /kitchen/tickets/{id}/recall` | |
| 品切れ | `PUT /stock/{targetId}` | `stock.updated` |
| 定期と再接続 | `POST /devices/me/heartbeat`、`POST /devices/token`、`GET /events?after=` | `menu.published`、`store.updated` |

### 4.5 受付機 (`IReceptionApi`)

| 場面 | API | 受ける通知 |
| --- | --- | --- |
| 登録 (一度だけ) | `POST /devices/pair` | |
| 起動 | `POST /devices/token`、`GET /devices/me/config`、`GET /store`、`GET /tables?status=Vacant`、`/hubs/store` | |
| 受付 | `GET /tables?status=Vacant` (空席の有無を出す)、`POST /visits` (テーブルを送らず、サーバが空席を決める。満席は `409` `NO_VACANT_TABLE`) | `visit.opened` / `visit.moved` / `visit.closed` (空席を更新する)、`store.updated` (来店の開き方が受付機でなくなったら受け付けを止める。ラストオーダーの時刻を更新する) |
| 定期と再接続 | `POST /devices/me/heartbeat`、`POST /devices/token`、`GET /events?after=` | |

- 受付機は、ラストオーダーの時刻 (`GET /store`) を過ぎたら受け付けない。  
  サーバも、受付機からの来店の開始を `422` (`LAST_ORDER_PASSED`) で断る

### 4.6 外部

| 相手 | API | 受ける通知 (Webhook。送り先ごとに種類を選ぶ) |
| --- | --- | --- |
| 本部の管理システム | `POST /oauth/token`、`PUT /images/{name}`、`POST /menu/publications` | `order.created`、`stock.updated` |
| POS | `POST /oauth/token`、`GET /tables`、`GET /visits/{id}`、`GET /visits/{id}/bill`、`POST /visits/{id}/close` | `visit.closed` (会計の明細と支払) |
| 決済サービス | `POST /payments/callbacks/{provider}` | |

- POS は、レジで払うお客様のテーブルから来店を探し、会計の明細で精算して来店を終える
- クライアントは管理画面で発行する。  
  本部にはテナント全体、POS には店舗ごとに発行する
- 複数のテナントと連携する相手 (POS を提供する会社など) は、テナントごとにクライアントを持つ

---

## 🚨 5. エラーコード

| HTTP | `errorCode` | 発生箇所 |
| --- | --- | --- |
| 400 | `VALIDATION_ERROR` | 入力の形式・必須の項目 (詳細は `errors`) |
| 401 | (なし) | アクセストークンがない・期限切れ・署名が合わない、無効にした端末か止めたテナントのトークン |
| 403 | `DEVICE_SCOPE` | 端末の種類や置き場所の範囲の外 (テーブル端末から他のテーブルの来店、キッチン端末から来店の開始など) |
| 403 | `CLIENT_SCOPE` | 外部のクライアントの範囲 (`scope`) の外 |
| 403 | `VISIT_OPENING_DISABLED` | 来店の開き方で許していない端末 (受付機、テーブル端末) からの来店の開始 |
| 403 | `DEVICE_REVOKED` | トークンのリクエストで、無効にした端末 (端末は初期設定に戻る) |
| 403 | `TENANT_SUSPENDED` | トークンのリクエストで、契約を止めたテナント (端末は止まっていることを表示し、間をおいて再取得する) |
| 404 | `NOT_FOUND` | 対象がない (ほかのテナントや店舗のものも同じ) |
| 409 | `DUPLICATE_ID_MISMATCH` | 同じ `id` で内容が違う再送 |
| 409 | `IMAGE_CONFLICT` | 同じ名前で中身の違う画像を置く |
| 409 | `VERSION_MISMATCH` | 来店の楽観ロックの失敗 |
| 409 | `TABLE_OCCUPIED` | 来店のあるテーブルでの開始・移動 |
| 409 | `NO_VACANT_TABLE` | 受付機の来店の開始で、人数の入る空席がない |
| 410 | `EVENTS_EXPIRED` | 欠落した通知を受け取れる範囲の外 (残していない、通し番号が先、多すぎる) |
| 422 | `PAIRING_CODE_INVALID` | ペアリングコードの不一致・期限切れ・使用済み |
| 422 | `DEVICE_KIND_MISMATCH` | 登録するアプリの端末の種類が、ペアリングコードや登録トークンの種類と違う (コードは使わない) |
| 422 | `VISIT_NOT_OPEN` | 終わった・取りやめた来店への注文・呼び出し・会計、会計を始める前の支払 |
| 422 | `CHECKOUT_IN_PROGRESS` | 会計中の来店への注文・移動・取りやめ |
| 422 | `ORDERING_PAUSED` | 店舗が注文を一時停止している |
| 422 | `LAST_ORDER_PASSED` | ラストオーダーの後の注文と、受付機の来店の開始 |
| 422 | `MENU_CHANGED` | 表示していたメニューと価格・内容が違う |
| 422 | `ITEM_SOLD_OUT` / `STOCK_INSUFFICIENT` | 品切れ / 残りの数を超える (`errors` に明細の `id`) |
| 422 | `ITEM_UNAVAILABLE` | 出せる条件 (時間帯、子どもがいる) を満たさない商品 |
| 422 | `OPTION_INVALID` | 必須のオプションがない、選べる数を超えた、商品にないオプション |
| 422 | `QUANTITY_EXCEEDED` | 数量・明細の数の上限 |
| 422 | `CONFIRMATION_REQUIRED` | 確認のルールに答えていない来店の、対象の品の注文 |
| 422 | `LIMIT_EXCEEDED` | 上限のルールを超える注文 |
| 422 | `LINE_STATUS_INVALID` | 明細の状態に合わない変更 (提供した品の取消など) |
| 422 | `VISIT_HAS_ORDERS` | 注文のある来店の取りやめ |
| 422 | `BILL_CHANGED` | 会計を始めるときの明細が変わっている |
| 422 | `PAYMENT_AMOUNT_INVALID` | 支払の額が 0 以下・残りを超える |
| 422 | `PAYMENT_METHOD_UNAVAILABLE` | 店舗で使えない支払方法 |
| 429 | (なし) | レート制限 (端末・クライアントごと、テナントごと)、ペアリングの試行の上限。`Retry-After` を付ける |
| 500 | (なし) | 想定していない例外 (Problem Details。`errorCode` は付けない) |

---

## 🔍 6. 参考にした考え方

テーブルオーダー・POS・注文の取り次ぎのクラウドのサービスで広く使われている API の形を参考にした。  
テナントの扱いは、クラウドのマルチテナントの設計の指針も参考にした。

| 考え方 | よくある API の形 | 本書 |
| --- | --- | --- |
| 注文の階層、人数とテーブルを伝票に持つ | 注文 > 会計の単位 > 明細の階層で、注文に客数とテーブルを持つ | 来店 > 注文 > 明細。人数は来店に持つ |
| 明細の調理の段階 | 明細ごとに調理の状態 (新規、保留、送信済み、できあがり) を持つ | 明細の `status` (`Held` / `Ordered` / `Cooking` / `Ready` / `Served`)。食後の品は `Held` |
| 品切れと残りの数 | 商品の在庫の状態 (あり、残りの数、品切れ) | `Available` / `Limited` (残りの数) / `SoldOut` |
| オプションの選ぶ数 | オプションの組に選ぶ数の最小と最大を持つ | `minSelect` / `maxSelect` |
| 再送で重複しない | リクエストに冪等キーを付ける | 端末が採番する `id` |
| 楽観ロック | 注文にバージョン (`version`) を持ち、変更のときに確認する | 来店の `version` |
| 注文の一時停止 | 店舗の受付を一時的に止める (混雑のとき) | 店舗の `orderingPaused` |
| メニューの配信と状態の外部への通知 | メニューをプッシュで配信し、注文の状態を Webhook で通知する | `POST /menu/publications`、Webhook |
| テーブルでの会計 | 客席の端末で QR コード決済、アプリでクレジット | 会計 (QR は店舗が見せ、カードは決済端末) |
| テナントの見分け方 | 契約・加盟店・店舗の `id` をパスかヘッダで指し、使えるテナントと店舗はトークンで限る。指す値は、1 つの資格情報で複数の店舗を扱う連携のため | トークンのテナントと店舗で決め、URL に入れない (端末とクライアントは 1 つのテナントに属するので、選ぶ値が要らない) |
| リクエストの中の `id` の確認 | `id` がトークンの範囲のものかを毎回確認する (オブジェクト単位の認可) | トークンのテナントと店舗の中で引き、ほかは `404` |
| 外部の連携の認証 | OAuth 2.0 の client credentials で、契約・加盟店ごとのトークン | テナントごと (POS は店舗ごと) のクライアント |
