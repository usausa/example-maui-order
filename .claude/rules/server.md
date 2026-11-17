---
paths:
  - "server/src/TableOrder.Server.Core/**"
  - "server/src/TableOrder.Server.Web/**"
  - "server/src/TableOrder.Server.AppHost/**"
---
# サーバ (ASP.NET Core)

## テナント

- テナントと店舗は、検証したアクセストークンのクレームから作った `ServiceContext` で受け取り、リクエストのパス・ヘッダ・本文の値から決めない
- Service は `ServiceContextProvider.Current` の `RequireTenantId()` / `RequireStoreId()` を Accessor の引数に渡し、すべての読み書きをテナントで絞る
- リクエストの中の `id` はテナントと店舗の条件と一緒に引き、ほかのテナント・店舗のものは見つからないとして `404` (`NOT_FOUND`) にする
- テナントのわからないリクエスト (端末の登録、トークンのリクエスト、決済の通知、電子レシート) で引くのは `DirectoryAccessor` だけにし、引いた行のテナントをその後のコンテキストにする
- 管理画面のサインインと、利用者が自分の資格情報を読み書きするのは `AccountAccessor` (利用者の `Id` かメールアドレスで引く) だけにする
- テナントの利用者の一覧と管理は、選んだテナントで絞る (`AdminUserService`)
- テナントをまたいで読むのは運営者の画面とバックグラウンド処理だけにし、そのための Accessor (運営者の画面は `TenantAccessor`、バックグラウンド処理は `BackgroundAccessor`) を分ける
- バックグラウンド処理が店舗のデータを読み書きするときは、テナントと店舗で `ServiceContext` を始めてから Service を呼ぶ
- アクセストークンのクレームの値は、Service が端末の記録から作った `DeviceIdentity` から入れ、端末が送った値を入れない

## Core

### 構成

- エンドポイントと Blazor のページは `Services/` の `XxxService` を呼び、Service が Accessor と `TableOrder.Domain` を使う
- Service は個別に DI 登録しない (`AddCoreServices()` が自動で登録する)
- Service は `TableOrder.Contract` の Request を受けて Response を返してよい (エンドポイントは渡して返すだけにする)
- 表の行は `Models/Entity` の `XxxEntity` にする
- Service が組み立てるコンテキストは `Models` の record (`DeviceIdentity`) にする
- 1 つの Service だけが返す結果型 (`XxxResult`) と状態 (`XxxStatus`) は、その Service のファイルの先頭で定義する
- 業務で使う現在時刻は、リクエストの中は `ServiceContext.Now`、リクエストの外 (起動、管理画面からの発行) は `TimeProvider` から読む
- Core が Web に持たせるもの (通知の送信、失効リスト、トークンのリクエストの署名の検証、アクセストークンの発行) は、Core にインタフェース (`IEventPublisher`、`IRevocationList`、`IDeviceAssertionValidator`、`IAccessTokenIssuer`) を置いて Web で実装する
- Service は、変更したあとに `IEventPublisher` と `IRevocationList` へ伝えるだけにする
- 決済サービスは `IPaymentProvider` の実装で切り替え、Service から決済サービスを直接呼ばない
- ASP.NET Core に依存しない基盤 (データの型変換、JSON) は Core の `Infrastructure/`、依存するもの (例外処理、ログ、セキュリティ) は Web の `Infrastructure/` に置く

### メソッド名

Service のメソッド名は次の形にする。

| 操作 | 名前 |
| --- | --- |
| 1 件の取得 | `GetAsync` |
| 一覧の取得 | `GetListAsync` (数を添えた一覧は `GetSummaryListAsync`) |
| テナントや店舗をまたぐ全件の取得 | `GetAllAsync` |
| ないことを null で返す取得 (サインインの利用者) | `FindAsync` |
| 業務の登録 (来店、注文、呼び出し、支払、テナント) | `CreateAsync` |
| 管理画面で一覧に足す行 (利用者、運営者、店舗、テーブル) | `AddAsync` |
| 更新 | `UpdateAsync` (一部の更新は `UpdateBrandAsync` のように対象を入れる) |
| 有無の切り替え | `SetXxxAsync` (`SetActiveAsync`、`SetOrderingAsync`) |
| クリーンアップ | `DeleteXxxAsync` |
| 業務の操作と状態の遷移 | 業務の動詞 (`CancelAsync`、`MoveAsync`、`ServeAsync`、`StartCheckoutAsync`) |

- 取得は上の名前を基本にし、対象を入れるとき (`GetStoreAsync`、`GetTokenListAsync`、`GetStoreAllAsync`) と内容の名前にするとき (`GetConfigAsync`、`GetCatalogAsync`、`GetReceiptAsync`) はその形にする

### 結果とエラー

- 入力の誤りと業務の失敗は Service が `ServiceError` で返し、値を返す処理は `ServiceResult<T>` にする
- 同じ Id の再送で既にあったものは、`ServiceResult<T>` に `Created` を付けない
- リクエストの一覧の要素の null は JSON の読み込みを通るので、ほかの検証の前に入力の誤りにする (500 にしない)
- バージョンを条件にした更新が 0 件のときは、行が店舗になければ `NOT_FOUND` (ほかの店舗の id も含む)、店舗にあれば `VERSION_MISMATCH` にする
- 済んでいる操作の再送 (取り消したトークンの取り消し) は成功にする

### 書き込み

- 複数の文にまたがる書き込みは、Service の中で `IDbProvider.UsingTxAsync` を使う
- 競合は、条件付きの更新の件数 (0 件) で判定する
- 店舗の状態を変える書き込み (来店の確認の記録のような、ほかの端末が判断に使う記録も含む) は `EventService.WriteAsync` の中で行い、変えた内容の通知を同じトランザクションで書く
- 通知の送信先は `EventRoutes` に足す
- 変わらなかったとき (確認の答え直し、同じ品切れの再送) は通知を書かない
- 書き込みの中で作るレスポンス (来店のレスポンス) は、トランザクションを受けるメソッドで読む (`VisitService.ToResponseAsync(tx, ...)`、`LoadResponseAsync`)

### データの持ち方

- 書き込む経路のない列を持たない (スタッフを記録するのは、リクエストに `staffId` を受ける操作の表だけ)
- 増えていく設定の項目 (機能の有無) は列にせず JSON に持ち、ない項目は既定の値にする (`Features`)
- シークレット (登録トークン、クライアントシークレット) は平文で持たず SHA-256 のハッシュで持ち、そのまま持つのはペアリングコードのような短命の値だけにする
- スタッフの PIN は桁が少ないので、`StaffPins` (PBKDF2、ソルトと反復回数つき) のハッシュで持ち、端末にもハッシュだけを渡す
- PIN のハッシュは PIN を使う端末 (テーブル、ホール、受付) にだけ返す (キッチン端末はブラウザで読めるので渡さない)

### 業務

- 時間帯やキッズで出し分ける品は、メニューのタグと出せる条件のルール (`Availability`) で表す
- 出せる条件は、サーバが注文を受けた時刻 (店舗のタイムゾーン) と来店の子どもの人数で検証する (端末の時刻を信じない)
- 時間帯の終わりは、店舗の設定の猶予の分数まで注文を受ける
- 会計の明細は、商品・オプション・単価・税率が同じものだけをまとめる (税率を変更したメニューの前後の明細を 1 つの税率で計算しない)
- 来店を終える書き込み (レジで閉じる、取りやめ、テーブルで払い終える) は、同じトランザクションでその来店の終わっていない呼び出しを終える (`call.updated`)
- 来店を終える書き込みは、会計中なら同じトランザクションで待っている支払をやめる (`payment.updated`。あとで届いた結果で払い終えない)
- 会計を始める書き込みは、同じトランザクションでお願いしていない食後の品をお願いする (`OrderService.ReleaseHeldAsync`。払い終えると来店は閉じ、あとからお願いできない)
- レジで払ったとして閉じる操作では食後の品を自動でお願いせず、閉じる確認で作らないことを伝える (ホール端末、管理画面の店内状況)
- 提供の一覧と提供は、払い終えて閉じた来店の品もその営業日のうちは扱う (キッチンのチケットは来店を閉じても残るので、できあがった品を運べなくしない)
- 来店のテーブルを移動したら、開いているチケットも `ticket.updated` で持ち場に通知する (キッチン端末はチケットの通知で一覧を再取得し、来店の今のテーブルを出す)
- 電子レシートには作ったときの店舗の事業者 (名前と登録番号) をコピーし、あとで店舗の設定を変更しても出したレシートを変えない
- 電子レシートの明細はコピーせず、閉じた来店から会計と同じ計算 (`BillCalculator`) で作る
- 受付機の来店の開始は、ラストオーダーを過ぎたら拒否する (`LAST_ORDER_PASSED`。同じ Id の再送は開いた来店を返す)
- ラストオーダーの判定は `StoreHours.IsAfterLastOrder` を使う

### 端末の登録

- 端末の登録は、リクエストのアプリの端末の種類がコードやトークンの種類と違えば、台数を使う前に拒否する (`DEVICE_KIND_MISMATCH`)
- 端末の登録と置き場所の変更は店舗の書き込み (`EventService.WriteAsync`) の中で行い、店舗とテーブルが使われていることをその中で確認する (外で確認したあとに使わなくしたものに置かない)
- 使わなくしたテーブルを指すコードでは、置き場所なしで登録する
- 同じ鍵の前の登録は、鍵を持つ端末が新しい登録でトークンを受け取るときに無効にし、前の店舗に `device.updated` を送る
- 前の登録は、登録のときには無効にしない (レスポンスが届かずに前の登録を使い続ける端末と、ほかの端末の公開鍵を送って作った登録で、前の登録を壊さないため)

### 画像

- 画像の保存先は `IImageStore` の実装で切り替える (開発とテストはファイル、本番は Amazon S3)
- 画像の保存先はテナントごとに分ける
- 画像の名前は `ImageNames.IsValid` で検証してから渡す (保存先のパスに使うため)
- サンプルの画像は `Assets/Images` に `{名前}.{内容の SHA-256 の先頭 8 桁}.png` で置く
- サンプルの画像の中身を変えたら名前も変え、`Menu.json` の `imageName` を直す (端末は名前で保存して再取得しないため)

### 開発の環境のシミュレーション

- 開発の環境のシミュレーションは `SimulationService` に置き、業務の処理と同じ条件付きの更新と通知で進める
- シミュレーションは設定 `Simulation:Enabled` で動かし、テストのサーバでは止める

## Web

### 登録と設定

- `Program.cs` は拡張メソッドの呼び出しの列挙だけにし、実体は `Application/ApplicationExtensions.cs` に機能ごとの区切りで書く
- 設定は `Settings/XxxSetting` (DataAnnotations で制約) を `AddSetting<T>` で登録し、値を Singleton で使う
- 業務のクラスに `IOptions<T>` を渡さない
- DI の登録を設定の値で分けず、動かすかどうかは動き始めてから設定で決める (テストのサーバの設定は登録のあとに効くため)
- 登録のあとの設定で決めるオプション (HTTP のログの項目) は `AddOptions<T>().Configure<TSetting>` で組み立て、構成を登録の前に直接読まない
- 使わない計測 (作るだけの `ActivitySource`、同じソースを重ねて足す `AddSource`) を置かない

### ログ

- ログは、管理画面の部品のものも `Application/Log.cs` の `[LoggerMessage]` に集約する (Info~ / Warn~ / Error~ の命名、`key=[{value}]` の書式)
- ログの全行に付ける値 (接続元、テナント、店舗、主体) は `CallbackEnricher` で付け、専用のミドルウェアを置かない
- リクエストの外では、テナントと店舗を始めている業務のコンテキスト (`ApplicationServiceContextProvider.Peek`) から付ける
- 店舗ごとのバックグラウンド処理の失敗のログには、テナントと店舗を引数で入れる (捕まえたときには店舗のコンテキストを閉じているため)
- ログにシークレットと個人情報を出さない
- HTTP のログの本文 (`Log:HttpDump`) から、シークレットを運ぶ API (登録、トークン、端末の設定) を外す (`BodyExcludingHttpLoggingInterceptor`)
- 利用者は、メールアドレスではなく Id でログに出す

### データベース

- SQLite の接続文字列に `Cache=Shared` を付けない (表の単位でロックし、WAL でも書き込みの間は読み取りが待たされるため)
- 重複の判定 (`IDialect.IsDuplicate`) は主キーと一意制約の違反だけにする (外部キーや NOT NULL の違反を、重複や入力の誤りに見せない)

### 端末の認証

- 端末の公開鍵は、読んだ座標から書き直した JWK で持つ (`DevicePublicKeys`。同じ鍵を同じ文字列で引くため)
- 端末の認証の失敗 (署名、期限、端末がない、jti の再利用、失効リスト) はどれも `401` にし、理由を見せない (JwtBearer の `IncludeErrorDetails = false`。端末はトークンを再取得し、トークンのリクエストの `DEVICE_REVOKED` / `TENANT_SUSPENDED` で理由を知る)
- 無効にした端末と止めたテナントのアクセストークンは、JwtBearer が検証したあとに失効リスト (`RevocationList`) を引いて拒否し、リクエストごとにデータベースを引かない
- 端末を無効にする・テナントを止める・戻す Service は、変更したあとに `IRevocationList.RefreshAsync` で失効リストを再読み込みさせる
- ほかのサーバの失効リストは、`RevocationWorker` が `Token:RevocationSweepSeconds` ごとに再読み込みする

### レート制限

- API のレート制限の `429` には `Retry-After` を付ける
- レート制限の接続元は `RateLimits.PartitionOf` で数える (IPv6 は /64。同じネットワークの中でアドレスを変えて試させないため)
- 管理画面のサインインと多要素のコードの送信は、接続元ごとにレート制限する (`RateLimits.SignIn`)
- レート制限のパーティションのキーは、制限の仕方の違うもの (送信と画面) で分ける (同じキーだと、先に作った制限を使い回すため)
- レート制限で拒否した管理画面のフォームの送信は、本文のない 429 ではなく、同じ画面に失敗を示すクエリを付けて戻し、メッセージを出す (`AccountPaths.RateLimitedPath`。戻る先などのクエリは残す)

### バックグラウンド処理

- 古いデータを削除する処理は `CleanupWorker` に足す
- 通知のディスパッチャ (`EventDispatcher`) には、送信 (ハブと、店舗を見ている画面への `StoreActivity`) のほかの処理を置かない
- 店舗のデータのクリーンアップ (閉じた来店、メニューの公開) は、店舗ごとにコンテキストを始めて `CleanupService` で行う
- クリーンアップは、件数を限ったトランザクションで子の表から削除する (ほかの書き込みを長く止めないため)
- クリーンアップでは、開いている来店と今のメニューを削除しない
- クリーンアップでは通知を書かない (今の状態は変わらないため)
- 閉じた来店を残す期間は、営業日 (店舗のタイムゾーンと開店の時刻) で数える (同じ営業日の来店をまとめて削除する)
- 店舗をまとめて回すバックグラウンド処理 (ディスパッチャ、開発の環境のシミュレーション) は、店舗ごとに失敗を捕まえてほかの店舗を止めない
- 間隔ごとのポーリングは、通知の合間ではなく前のポーリングからの時間で行う (通知が続いてもポーリングする)

### Web アプリの配信

- サーバが配信する Web アプリ (キッチン端末) の、内容で名前の変わらないファイル (`_framework` の外。index.html、JavaScript のモジュール、スタイル) は `Cache-Control: no-cache` で返す (ないとブラウザが推測でキャッシュし、更新しても古いファイルを使うため)

### 管理画面の操作と資格情報

- 止めた利用者の資格情報は書き込まない (SQL の条件に `IsActive`。止める前の Cookie のままパスワードを変更して、新しいセキュリティスタンプの Cookie を受け取らせないため)
- 管理画面の利用者の間違えた回数と止める時刻は、資格情報のバージョンを見ずに 1 文で書き (`AdminUserStore`)、資格情報の書き込み (`UpdateCredential`) には含めない (同時に間違えたサインインを数え漏らさず、止めたことを消さないため)
- 仮のパスワードは管理画面で作って (`TemporaryPassword`) ハッシュにし、Service には平文を渡さない
- 利用者のメールアドレスは足すときに形式を検証し (`AdminUserService.IsEmail`)、Identity の利用者名の文字の制限は使わない (足せたアドレスで資格情報を変更できなくならないため)
- 利用者の管理は、テナントの利用者を `AdminUserService`、運営者を `OperatorService` で行う
- 利用者の管理の Service には操作した利用者の id を渡し、自分の役割の変更と自分を止めることを拒否する
- 管理画面で端末を変更したとき (置き場所、名前、無効化) は `device.updated` を送り、端末に起動からやり直させる (トークンの置き場所と無効化をすぐに反映するため)
- 登録トークンの平文は、出した画面で管理対象の構成のキー (`apiEndPoint`、`enrollmentToken`) と並べて一度だけ見せ、一覧には出さない
- 店舗の設定の値 (電子レシートなど) は管理画面の店舗の設定で変更できるようにし、店舗を足すときにコピーするだけの値を残さない
- 電子レシートの事業者の名前と登録番号は店ごとに店舗の設定で入れ、店舗を足すときにコピーしない (フランチャイズの店は事業者が違い、違う登録番号のレシートを出さないため)
- チェーンと店舗の設定・店舗の基本・テーブルを変更したときは、設定のバージョン (`Stores.SettingsVersion`) を上げて `store.updated` を送る (チェーンの設定はテナントのすべての店舗)
- 設定のバージョンは、店舗の行の編集の楽観ロック (`Version`) と分ける
- `Version` は注文の一時停止では上げない (営業中の一時停止で、編集中の店舗の設定を保存できなくしないため)
- テナントのすべての店舗に通知する管理画面の操作 (チェーンの設定) は、店舗ごとの書き込みの失敗で止めずに続け、通知できなかった店舗を返して画面に出す (もう一度保存すると、もう一度通知する)
- 店舗とテーブルを使わなくする前に、使っているもの (端末、開いている来店) がないかを確認し、あれば入力の誤りで拒否する (行は削除しない)

## エンドポイント

### API

- API は `Endpoints/XxxEndpoints` に、`// Mapping` と処理ごとの区切りで書く
- 経路は `ApiRoutes` の定数にする
- `MapXxxEndpoints` を `MapEndpoints` に足す
- API のグループは `MapApiGroup` で作る (既定で認証を求め、計測とコンテキストのフィルターが付き、401 と 500 のレスポンスを宣言する)
- 匿名で受けるエンドポイントは `AllowAnonymous()` を付ける
- 推測できる値を受けるエンドポイント (登録) はレート制限する
- 端末の種類で使える API は `Policies` で絞り、範囲の外は `ApiAuthorizationResultHandler` が `403` (`DEVICE_SCOPE`) にする
- エンドポイントは返すレスポンスだけを、すべてステータスコードの小さい順に宣言する (401 と 500 はグループが宣言する)
- 本文のあるレスポンスは `Produces<T>`、本文のないレスポンスは `Produces(状態)`、Problem Details は `ProducesProblem(状態)` で宣言し、同じステータスコードは 1 回だけ宣言する
- 端末の種類のポリシーで拒否するエンドポイントは 403 を、レート制限するエンドポイントは 429 を `ProducesProblem` で宣言する (本文のないレスポンスも StatusCodePages で Problem Details になるため)
- 400 は Service が入力を検証するエンドポイントだけ `ProducesValidationProblem()` で宣言し、引数のバインドと JSON の読み取りの失敗は個別に宣言しない
- 通信データは `XxxRequest` / `XxxResponse` にし、一覧は `XxxListResponse` (要素は `XxxListResponseItem`) にする
- 一覧の取得の操作名とハンドラは `XxxList` / `HandleListAsync` にする
- 失敗は `ApiProblems` で Problem Details にする (`errorCode` は端末と共有する `TableOrder.Contract` の `ErrorCodes`)
- 新しい失敗は `ErrorCodes` と `ApiProblems` の定義の両方に足す
- Service の結果は `ApiResults` でレスポンスにする
- エンドポイントで入力の検証、ステータスコード・errorCode の組み立て、複数の Service の組み合わせをしない
- エンドポイントで行うのは形の変換 (公開鍵を JWK に書き直すなど) だけにし、読めなければ null で渡して Service が拒否する
- 本文を省ける API (null を許す本文の引数) は作らない (本文のないリクエストは本文を受ける経路に合わず、知らない経路の 404 になるため)
- リクエストの値を読めない `400` (引数のバインド、JSON の形式の誤り、知らない項目) にも `VALIDATION_ERROR` を付ける (`CustomizeProblemDetails`)
- Problem Details の title は日本語にし (`ApiProblems`)、フレームワークが付ける英語の title (リクエストの読み取りの `400`、`500`) も置き換える (端末は title を出さずに errorCode で扱うが、API のレスポンスの文言をそろえるため)

### 通知のハブ

- 通知のハブ (`Hubs/StoreHub`) は、端末をグループに入れて送るだけにする
- グループの名前は `StoreHubGroups` で作る (テナントと店舗を入れる)
- ハブはグループに入れ終えたら、`ready` で店舗の今の通し番号を送る (端末は初めはその番号から数え、再接続したら欠落した通知を読む)
- ハブの `OnConnectedAsync` で覚えた接続は、途中の失敗で外す (SignalR は `OnConnectedAsync` が失敗したら `OnDisconnectedAsync` を呼ばないため)
- ハブは接続した端末を `StoreHubConnections` に覚え、失効リストに入った接続を切る
- ハブはトークンの期限が来た接続も切る (`CloseOnAuthenticationExpiration`。WebSocket は接続したときにだけトークンを検証するため)
