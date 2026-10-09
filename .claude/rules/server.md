---
paths:
  - "server/src/**"
---
# サーバ (ASP.NET Core)

Accessor の書き方は accessor.md、SQL は sql.md に置く。

## テナント

- テナントと店舗は、確かめたアクセストークンのクレームから作った `ServiceContext` で受け取り、要求のパス・ヘッダ・本文の値から決めない
- Service は `ServiceContextProvider.Current` の `RequireTenantId()` / `RequireStoreId()` を Accessor の引数に渡し、すべての読み書きをテナントで絞る
- 要求の中の `id` はテナントと店舗の条件と一緒に引き、ほかのテナント・店舗のものは見つからないとして `404` (`NOT_FOUND`) にする
- テナントのわからない要求 (端末の登録、トークンの要求、決済の通知、電子レシート) で引くのは `DirectoryAccessor` だけにし、引いた行のテナントをその後の文脈にする
- テナントをまたいで読むのは運営者の画面と裏の処理だけにし、そのための Accessor (運営者の画面は `TenantAccessor`、裏の処理は `BackgroundAccessor`) を分ける
- 裏の処理が店舗のものを読み書きするときは、テナントと店舗で `ServiceContext` を始めてから Service を呼ぶ
- アクセストークンのクレームの値は、Service が端末の記録から作った `DeviceIdentity` から入れ、端末が送った値を入れない

## Core

- エンドポイントと Blazor のページは `Services/` の `XxxService` を呼び、Service が Accessor と `TableOrder.Domain` を使う
- Service は個別に DI 登録しない (`AddCoreServices()` が自動で登録する)
- Service は `TableOrder.Contract` の Request を受けて Response を返してよい (入口は渡して返すだけにする)
- 表の行は `Models/Entity` の `XxxEntity`、Service が組み立てる文脈は `Models` の record (`DeviceIdentity`) にする
- 1 つの Service だけが返す結果型 (`XxxResult`) と状態 (`XxxStatus`) は、その Service のファイルの先頭で定義する
- 業務で使う現在時刻は、要求の中は `ServiceContext.Now`、要求の外 (起動、管理画面からの発行) は `TimeProvider` から読む
- 複数の文にまたがる書き込みは Service の中で `IDbProvider.UsingTxAsync` を使い、競合は条件付きの更新の件数 (0 件) で判定する
- 店舗の状態を変える書き込み (来店の確認の記録のような、ほかの端末が判断に使う記録も含む) は `EventService.WriteAsync` の中で行い、変えた内容の通知を同じトランザクションで書く (通知の送る先は `EventRoutes` に足す。答え直しのように変わらなかったときは書かない)
- 入力の誤りと業務の失敗は Service が `ServiceError` で返し、値を返す処理は `ServiceResult<T>` にする (同じ Id の送り直しで既にあったものは `Created` を付けない)
- 決済サービスは `IPaymentProvider` の実装で替え、Service から決済サービスを直接呼ばない
- 画像の置き場は `IImageStore` の実装で替え (開発とテストはファイル、本番は Amazon S3)、テナントごとに分ける。名前は `ImageNames.IsValid` で確かめてから渡す (置き場の経路に使うため)
- サンプルの画像は `Assets/Images` に `{名前}.{内容の SHA-256 の先頭 8 桁}.png` で置き、中身を替えたら名前も替えて `Menu.json` の `imageName` を直す (端末は名前で保存して取り直さない)
- 開発の環境の自動の進行は `SimulationService` に置き、業務の処理と同じ条件付きの更新と通知で進める (設定 `Simulation:Enabled` で動かし、テストのサーバでは止める)
- 秘密 (登録トークン、クライアントの秘密) は平文で持たず SHA-256 で持つ。ペアリングコードのような短命の値だけそのまま持つ
- スタッフの PIN は桁が少ないので、`StaffPins` (PBKDF2、塩と回数つき) のハッシュで持ち、端末にもハッシュだけを渡す
- 増えていく設定の項目 (機能の有無) は列にせず json に持ち、ない項目は既定の値にする (`Features`)
- ASP.NET Core に依存しない基盤 (データの型変換、JSON) は Core の `Infrastructure/`、依存するもの (例外処理、ログ、セキュリティ) は Web の `Infrastructure/` に置く

## Web

- `Program.cs` は拡張メソッドの呼び出しの列挙だけにし、実体は `Application/ApplicationExtensions.cs` に機能ごとの節で書く
- 設定は `Settings/XxxSetting` (DataAnnotations で制約) を `AddSetting<T>` で登録し、値を Singleton で使う。業務の部品に `IOptions<T>` を渡さない
- 部品の登録を設定の値で分けない (テストのサーバの設定は登録のあとに効く)。動かすかどうかは、動き始めてから設定で決める
- ログは `Application/Log.cs` の `[LoggerMessage]` に集約する (Info~ / Warn~ / Error~ の命名、`key=[{value}]` の書式)
- 全行のログに付ける値 (接続元、テナント、店舗、主体) は `CallbackEnricher` で付け、専用のミドルウェアを置かない
- 管理画面は、サインインを作るまで開発の環境だけで開く
- サーバが配る Web アプリ (キッチン端末) の、内容で名前の替わらないファイル (`_framework` の外。index.html、JavaScript の部品、スタイル) は `Cache-Control: no-cache` で返す (ないとブラウザが推して残し、更新しても古いファイルを使う)
- 管理画面のページは `AppPageBase` を継ぎ、選んだテナントと店舗 (`StoreSelection`) の文脈で Service を呼ぶ。ほかの部品の知らせ (店舗の選び直し) で読み直すときは `BeginServiceScope` で文脈を始める
- 管理画面で端末を替えたとき (置き場所、名前、無効化) は `device.updated` を送り、端末に起動からやり直させる (トークンの置き場所と無効化をすぐに反映する)
- チェーンと店舗の設定を替えたときは、設定の版 (`Stores.SettingsVersion`) を上げて `store.updated` を送る (チェーンの設定はテナントのすべての店舗)。版は一時停止などで上がる `Version` と分ける
- 古いデータを消す処理は `CleanupWorker` に足し、通知の送り手 (`EventDispatcher`) には送ること以外を置かない

## エンドポイント

- API は `Endpoints/XxxEndpoints` に `// Mapping` と処理ごとの区切りで書き、経路は `ApiRoutes` の定数にし、`MapXxxEndpoints` を `MapEndpoints` に足す
- API のグループは `MapApiGroup` で作る (既定で認証を求め、計測と文脈のフィルターが付く)。匿名で受ける入口は `AllowAnonymous()` を付け、推測できる値を受ける入口 (登録) は流量を限る
- 端末の種類で使える API は `Policies` で絞り、範囲の外は `ApiAuthorizationResultHandler` が `403` (`DEVICE_SCOPE`) にする
- 失敗は `ApiProblems` で Problem Details にし (`errorCode` は端末と共有する `TableOrder.Contract` の `ErrorCodes`)、新しい失敗は `ErrorCodes` と `ApiProblems` の定義の両方に足す
- Service の結果は `ApiResults` で応答にし、エンドポイントで状態や errorCode を組み立てない
- 本文を省ける API (null を許す本文の引数) は作らない。本文のない要求は本文を受ける経路に合わず、知らない経路の 404 になる
- 通知のハブ (`Hubs/StoreHub`) は端末をグループに入れて送るだけにし、グループの名前は `StoreHubGroups` で作る (テナントと店舗を入れる)
- ハブはグループに入れ終えたら `ready` で店舗の今の通し番号を送る (端末ははじめはその番号から数え、つなぎ直したら抜けた通知を読む)
- 端末の認証の失敗 (署名、期限、端末がない、使い捨ての値の使い回し) はどれも `401` にし、理由を見せない
