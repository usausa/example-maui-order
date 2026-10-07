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
- テナントをまたいで読むのは運営者の画面と裏の処理だけにし、そのための Accessor (`TenantAccessor`) を分ける
- アクセストークンのクレームの値は、Service が端末の記録から作った `DeviceIdentity` から入れ、端末が送った値を入れない

## Core

- エンドポイントと Blazor のページは `Services/` の `XxxService` を呼び、Service が Accessor と `TableOrder.Domain` を使う
- Service は個別に DI 登録しない (`AddCoreServices()` が自動で登録する)
- Service は `TableOrder.Contract` の Request を受けて Response を返してよい (入口は渡して返すだけにする)
- 表の行は `Models/Entity` の `XxxEntity`、Service が組み立てる文脈は `Models` の record (`DeviceIdentity`) にする
- 1 つの Service だけが返す結果型 (`XxxResult`) と状態 (`XxxStatus`) は、その Service のファイルの先頭で定義する
- 業務で使う現在時刻は、要求の中は `ServiceContext.Now`、要求の外 (起動、管理画面からの発行) は `TimeProvider` から読む
- 複数の文にまたがる書き込みは Service の中で `IDbProvider.UsingTxAsync` を使い、競合は条件付きの更新の件数 (0 件) で判定する
- 秘密 (登録トークン、クライアントの秘密) は平文で持たず SHA-256 で持つ。ペアリングコードのような短命の値だけそのまま持つ
- ASP.NET Core に依存しない基盤 (データの型変換、JSON) は Core の `Infrastructure/`、依存するもの (例外処理、ログ、セキュリティ) は Web の `Infrastructure/` に置く

## Web

- `Program.cs` は拡張メソッドの呼び出しの列挙だけにし、実体は `Application/ApplicationExtensions.cs` に機能ごとの節で書く
- 設定は `Settings/XxxSetting` (DataAnnotations で制約) を `AddSetting<T>` で登録し、値を Singleton で使う。業務の部品に `IOptions<T>` を渡さない
- ログは `Application/Log.cs` の `[LoggerMessage]` に集約する (Info~ / Warn~ / Error~ の命名、`key=[{value}]` の書式)
- 全行のログに付ける値 (接続元、テナント、店舗、主体) は `CallbackEnricher` で付け、専用のミドルウェアを置かない
- 管理画面は、サインインを作るまで開発の環境だけで開く

## エンドポイント

- API は `Endpoints/XxxEndpoints` に `// Mapping` と処理ごとの区切りで書き、経路は `ApiRoutes` の定数にし、`MapXxxEndpoints` を `MapEndpoints` に足す
- API のグループは `MapApiGroup` で作る (既定で認証を求め、計測と文脈のフィルターが付く)。匿名で受ける入口は `AllowAnonymous()` を付け、推測できる値を受ける入口 (登録) は流量を限る
- 端末の種類で使える API は `Policies` で絞り、範囲の外は `ApiAuthorizationResultHandler` が `403` (`DEVICE_SCOPE`) にする
- 失敗は `ApiProblems` で Problem Details にし (`errorCode` は `ErrorCodes`)、新しい失敗は両方に足す
- 端末の認証の失敗 (署名、期限、端末がない、使い捨ての値の使い回し) はどれも `401` にし、理由を見せない
