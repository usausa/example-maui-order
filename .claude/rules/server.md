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
- 管理画面のサインインと、利用者が自分の資格情報を読み書きするのは `AccountAccessor` (利用者の `Id` かメールアドレスで引く) だけにし、テナントの利用者の一覧と管理はテナントで絞る
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
- 店舗の状態を変える書き込み (来店の確認の記録のような、ほかの端末が判断に使う記録も含む) は `EventService.WriteAsync` の中で行い、変えた内容の通知を同じトランザクションで書く (通知の送る先は `EventRoutes` に足す。答え直しや同じ品切れの送り直しのように変わらなかったときは書かない)
- 書き込みの中で作る応答 (来店の応答) は、トランザクションを受ける版で読む (`VisitService.ToResponseAsync(tx, ...)`、`LoadResponseAsync`)
- 会計の明細は、商品・オプション・単価・税率が同じものだけをまとめる (税率を直したメニューの前後の明細を 1 つの税率で計算しない)
- 時間帯やキッズで出し分ける品は、メニューのタグと出せる条件のルール (`Availability`) で表し、サーバが注文を受けた時刻 (店舗のタイムゾーン) と来店の子どもの人数で確かめる (端末の時刻を信じない。時間帯の終わりは店舗の設定の猶予の分数まで受ける)
- 書く経路のない列を持たない (スタッフを記録するのは、要求に `staffId` を受ける操作の表だけ)
- 入力の誤りと業務の失敗は Service が `ServiceError` で返し、値を返す処理は `ServiceResult<T>` にする (同じ Id の送り直しで既にあったものは `Created` を付けない)
- 要求の一覧の要素の null は JSON の読み込みを通るので、ほかの確かめの前に入力の誤りにする (500 にしない)
- 版つきの更新が 0 件のときは、店舗の行でなければ `NOT_FOUND` (ほかの店舗の id も含む)、店舗の行なら `VERSION_MISMATCH` にする。済んでいる操作の送り直し (取り消したトークンの取り消し) は成功にする
- 来店を終える書き込み (レジで閉じる、取りやめ、テーブルで払い終える) は、同じトランザクションでその来店の終わっていない呼び出しを終え (`call.updated`)、会計中なら待っている支払をやめる (`payment.updated`。あとで届いた結果で払い終えない)
- 会計を始める書き込みは、同じトランザクションでお願いしていない食後の品をお願いする (`OrderService.ReleaseHeldAsync`。払い終えると来店は閉じ、あとからお願いできない)。レジで払ったとして閉じる操作では自動でお願いせず、閉じる確認で作らないことを知らせる (ホール端末、管理画面の店内の今)
- 提供の一覧と提供は、払い終えて閉じた来店の品もその営業日のうちは扱う (キッチンのチケットは来店を閉じても残るので、できあがった品を運べなくしない)
- 来店のテーブルを移したら、開いているチケットも `ticket.updated` で持ち場に知らせる (キッチン端末はチケットの通知で一覧を読み直し、来店の今のテーブルを出す)
- 決済サービスは `IPaymentProvider` の実装で替え、Service から決済サービスを直接呼ばない
- Core が入口の側に持たせるもの (通知を送る先、すぐに拒む一覧) は Core にインタフェース (`IEventPublisher`、`IRevocationList`) を置いて Web で実装し、Service は替えたあとに知らせるだけにする
- 画像の置き場は `IImageStore` の実装で替え (開発とテストはファイル、本番は Amazon S3)、テナントごとに分ける。名前は `ImageNames.IsValid` で確かめてから渡す (置き場の経路に使うため)
- サンプルの画像は `Assets/Images` に `{名前}.{内容の SHA-256 の先頭 8 桁}.png` で置き、中身を替えたら名前も替えて `Menu.json` の `imageName` を直す (端末は名前で保存して取り直さない)
- 開発の環境の自動の進行は `SimulationService` に置き、業務の処理と同じ条件付きの更新と通知で進める (設定 `Simulation:Enabled` で動かし、テストのサーバでは止める)
- 秘密 (登録トークン、クライアントの秘密) は平文で持たず SHA-256 で持つ。ペアリングコードのような短命の値だけそのまま持つ
- 受付機の来店の開始は、ラストオーダーを過ぎたら断る (`LAST_ORDER_PASSED`。同じ Id の送り直しは開いた来店を返す)。ラストオーダーの判定は `StoreHours.IsAfterLastOrder` を使う
- 端末の登録は、要求のアプリの端末の種類がコードやトークンの種類と違えば、台数を使う前に断る (`DEVICE_KIND_MISMATCH`)
- 端末の登録と置き場所の変更は店舗の書き込み (`EventService.WriteAsync`) の中で行い、店舗とテーブルが使われていることをその中で確かめる (外で確かめたあとに使わなくしたものに置かない。使わなくしたテーブルのコードは置き場所なしで登録する)
- 同じ鍵の前の登録は、鍵を持つ端末が新しい登録でトークンを受け取るときに無効にし、前の店舗に `device.updated` を送る (登録のときには無効にしない。応答が届かずに前の登録を使い続ける端末と、ほかの端末の公開鍵を送って作った登録で、前の登録を壊さない)
- スタッフの PIN は桁が少ないので、`StaffPins` (PBKDF2、塩と回数つき) のハッシュで持ち、端末にもハッシュだけを渡す。ハッシュは PIN を使う端末 (テーブル・ホール・受付) にだけ返す (キッチン端末はブラウザで読めるので渡さない)
- 増えていく設定の項目 (機能の有無) は列にせず json に持ち、ない項目は既定の値にする (`Features`)
- ASP.NET Core に依存しない基盤 (データの型変換、JSON) は Core の `Infrastructure/`、依存するもの (例外処理、ログ、セキュリティ) は Web の `Infrastructure/` に置く

## Web

- `Program.cs` は拡張メソッドの呼び出しの列挙だけにし、実体は `Application/ApplicationExtensions.cs` に機能ごとの節で書く
- 設定は `Settings/XxxSetting` (DataAnnotations で制約) を `AddSetting<T>` で登録し、値を Singleton で使う。業務の部品に `IOptions<T>` を渡さない
- 部品の登録を設定の値で分けない (テストのサーバの設定は登録のあとに効く)。動かすかどうかは、動き始めてから設定で決める
- ログは `Application/Log.cs` の `[LoggerMessage]` に集約する (Info~ / Warn~ / Error~ の命名、`key=[{value}]` の書式)
- 全行のログに付ける値 (接続元、テナント、店舗、主体) は `CallbackEnricher` で付け、専用のミドルウェアを置かない。要求の外は始めている業務の文脈 (`ApplicationServiceContextProvider.Peek`) から付ける
- 店舗ごとの裏の処理の失敗のログには、テナントと店舗を引数で入れる (捕まえたときには店舗の文脈を閉じている)
- ログに秘密と個人の情報を出さない。HTTP のログの本文 (`Log:HttpDump`) から秘密を運ぶ API (登録、トークン、端末の設定) を外し (`BodyExcludingHttpLoggingInterceptor`)、利用者はメールアドレスではなく Id で出す
- 登録したあとの設定で決める部品の設定 (HTTP のログの項目) は `AddOptions<T>().Configure<TSetting>` で組み立て、構成を登録の前に直接読まない
- 使わない計測の部品 (作るだけの `ActivitySource`、出どころの重なった `AddSource`) を置かない
- 管理画面はどの環境でも開き、ページはサインインを求める (`MapRazorComponents` に `AdminPolicies.SignedIn`)。サインインの前に開く画面 (サインイン、多要素のコード、エラー) だけ `[AllowAnonymous]` を付ける
- 管理画面の確かめ方は Cookie に限り (ポリシーは `IdentityConstants.ApplicationScheme`)、端末の API と通知のハブはアクセストークンに限る (既定の確かめ方は経路で選ぶ)
- 役割で絞る画面は `[Authorize(Policy = AdminPolicies.Xxx)]` を付け、メニュー (`NavMenu`) にも出さない
- Cookie を書く画面 (サインイン、パスワード、多要素) は `[ExcludeFromInteractiveRouting]` の静的な画面にして `AccountLayout` で出し、入力は対話しない素の部品 (`InputText`) にする。フォームの値 (`[SupplyParameterFromForm]`) は初期化子を付けず `OnInitialized` で作る
- 静的な画面から移るときは `NavigationManager.NavigateTo` のあとに処理を続けない (`return` する)。戻る先は `AccountPaths.LocalReturnPath` で管理画面の中の経路に限る
- 利用者の扱える範囲は `AdminScope` (サインインのクレームの役割、テナント、受け持つ店舗) で決め、`StoreSelection` は範囲の外を選ばない (画面で選んだ値をそのまま Service の文脈にしない)
- 利用者の資格情報 (パスワード、役割、受け持つ店舗、多要素、止める) を替えたら `SecurityStamp` を替え、開いている管理画面をサインインからやり直させる
- サインインできるか (止めた利用者、止めたテナント) は、サインインのほか Cookie の確かめ直し (`ValidateSecurityStampAsync`) と出し直し (`RefreshSignInAsync`) でも `AdminSignInManager` で確かめ、止めた利用者の資格情報は書かない (SQL の条件に `IsActive`。止める前の Cookie のままパスワードを替えて、新しい印の Cookie を受け取らせない)
- 仮のパスワードは管理画面で作って (`TemporaryPassword`) ハッシュにし、Service には平文を渡さない。平文は出した画面で一度だけ見せる
- 仮のパスワードの間は、パスワードの画面のほか (アカウント、多要素) をすべてパスワードの画面に移し、今と同じパスワードへの変更は断る
- 資格の印は扱える範囲と資格情報 (役割、受け持つ店舗、パスワード、多要素、止める) を替えたときだけ替え、名前だけの変更では替えない
- 開いている回線の確かめ直し (`AdminAuthenticationStateProvider`) は、回線を始めてから Cookie の期限を過ぎたら false にする (回線の操作は Cookie を延ばさない)
- 回復用のコードは認証アプリの鍵と同じくデータ保護で暗号にして持つ (桁の少ないコードを塩のないハッシュで持たない)
- 利用者の管理は、テナントの利用者を `AdminUserService` (選んだテナントで絞る)、運営者を `OperatorService` で行い、操作した利用者の id を渡して自分の役割の変更と自分を止めることを断る
- サーバが配る Web アプリ (キッチン端末) の、内容で名前の替わらないファイル (`_framework` の外。index.html、JavaScript の部品、スタイル) は `Cache-Control: no-cache` で返す (ないとブラウザが推して残し、更新しても古いファイルを使う)
- 管理画面のページは `AppPageBase` を継ぎ、選んだテナントと店舗 (`StoreSelection`) の文脈で Service を呼ぶ。ほかの部品の知らせ (店舗の選び直し、店舗の通知) で読み直すときは `AppPageBase.ReloadAsync` で行う (文脈を始め、失敗を画面の `ErrorBoundary` に渡す。知らせの中の例外を捨てない)
- 店舗の選択 (`StoreSelector`) もページと同じ `ErrorBoundary` の中に置く (外の失敗は回線ごと終わり、選択と入力を失う)
- 発行や追加のように重ねて押すと 2 回行うボタンは、`AppPageBase.RunOnceAsync` で 1 回にし、処理中は `Busy` で押せなくする
- テナントを選び直したら、前のテナントの店舗を指す入力 (受け持つ店舗、設定を写す店舗) を消す
- 店舗の設定の値 (電子レシートなど) は管理画面の店舗の設定で替えられるようにし、店舗を足すときに写すだけの値を残さない
- 失敗の文言は `AdminNames.ErrorMessage` で作り、画面ごとの文言は errorCode から返す関数で渡す (骨組みを画面ごとに写さない)。どの画面でも同じもの (利用者の状態、出した仮のパスワード、確かめのダイアログ) は `Components` にまとめる
- 管理画面に列挙の値を出すときは `AdminNames` の名前にする (列挙の名前をそのまま出さない)。時刻は 1 つに決めたタイムゾーンで出し、見出しにタイムゾーンを書く
- 管理画面の部品のログも `Application/Log.cs` に置き、`.razor` の `@code` のコメントも日本語にする
- 管理画面で端末を替えたとき (置き場所、名前、無効化) は `device.updated` を送り、端末に起動からやり直させる (トークンの置き場所と無効化をすぐに反映する)
- 無効にした端末と止めたテナントのアクセストークンは、JwtBearer が確かめたあとに `RevocationList` (すぐに拒む一覧) を引いて断り、要求ごとにデータベースを引かない
- 端末を無効にする・テナントを止める・戻す Service は、替えたあとに `IRevocationList.RefreshAsync` で一覧を読み直させる (ほかのサーバの一覧は `RevocationWorker` が `Token:RevocationSweepSeconds` ごとに読み直す)
- 登録トークンの平文は、出した画面で管理対象の構成のキー (`apiEndPoint`、`enrollmentToken`) と並べて一度だけ見せ、一覧には出さない
- チェーンと店舗の設定・店舗の基本・テーブルを替えたときは、設定の版 (`Stores.SettingsVersion`) を上げて `store.updated` を送る (チェーンの設定はテナントのすべての店舗)。版は店舗の行の編集の楽観ロック (`Version`) と分け、`Version` は注文の一時停止では上げない (営業中の一時停止で、編集中の店舗の設定を保存できなくしない)
- 店舗とテーブルを使わなくする前に、使っているもの (端末、開いている来店) がないかを確かめ、あれば入力の誤りで断る (行は消さない)
- 古いデータを消す処理は `CleanupWorker` に足し、通知の送り手 (`EventDispatcher`) には送ること (ハブと、店舗を見ている画面への `StoreActivity`) 以外を置かない
- 店舗のデータの片付け (閉じた来店、メニューの公開) は、店舗ごとに文脈を始めて `CleanupService` で行い、件数を限ったトランザクションで子の表から消す (ほかの書き込みを長く止めない。開いている来店と今のメニューは消さない。今の状態は変わらないので通知は書かない)
- 閉じた来店を残す期間は営業日 (店舗のタイムゾーンと開店の時刻) で数える (同じ営業日の来店をまとめて消す)
- 店舗をまとめて回す裏の処理 (送り手、開発の環境の自動の進行) は、店舗ごとに失敗を捕まえてほかの店舗を止めない。間隔ごとの見回りは知らせの合間でなく前の見回りからの時間で行う (知らせが続いても見回る)
- テナントのすべての店舗に知らせる管理画面の操作 (チェーンの設定) も、店舗ごとの書き込みの失敗で止めずに続け、知らせられなかった店舗を返して画面に出す (もう一度保存すると知らせ直す)
- ハブの `OnConnectedAsync` で覚えた接続は、途中の失敗で外す (SignalR は `OnConnectedAsync` が失敗したら `OnDisconnectedAsync` を呼ばない)
- SQLite の接続文字列に `Cache=Shared` を付けない (表の単位でロックし、WAL でも書き込みの間は読み取りが待たされる)
- 重なりの判定 (`IDialect.IsDuplicate`) は主キーと一意の違反だけにする (外部キーや NOT NULL の違反を、重なりや入力の誤りに見せない)
- 管理画面の利用者の間違えた回数と止める時刻は、資格情報の版を見ずに 1 文で書き (`AdminUserStore`)、資格情報の書き込み (`UpdateCredential`) には含めない (同時に間違えたサインインを数え漏らさず、止めたことを消さない)
- 管理画面のサインインと多要素のコードの送信は、接続元ごとに流量を限る (`RateLimits.SignIn`)。流量の区分の名前は、限り方の違う区分 (送信と画面) で分ける (同じ名前は先に作った限り方を使い回す)
- 利用者のメールアドレスは足すときに形を確かめ (`AdminUserService.IsEmail`)、Identity の利用者名の文字の制限は使わない (足せたアドレスで資格情報を替えられなくならないように)
- Identity の更新は結果 (`IdentityResult`、回復用のコードの null) を確かめ、書けなかったら Cookie を出し直さずに知らせる。続けて書く更新は 1 回にまとめるか、途中で書けなくても食い違わない順にする
- 店舗の通知で読み直す管理画面は、`StoreActivity.Watch` で選んだ店舗を見て (店舗を選び直したら見直す)、送り手のスレッドから呼ばれたら少し待って 1 回だけ、画面の文脈 (`InvokeAsync` と `BeginServiceScope`) で読み直す。読み直しで選んだものと入力の途中の値を消さない

## エンドポイント

- API は `Endpoints/XxxEndpoints` に `// Mapping` と処理ごとの区切りで書き、経路は `ApiRoutes` の定数にし、`MapXxxEndpoints` を `MapEndpoints` に足す
- API のグループは `MapApiGroup` で作る (既定で認証を求め、計測と文脈のフィルターが付く)。匿名で受ける入口は `AllowAnonymous()` を付け、推測できる値を受ける入口 (登録) は流量を限る
- 端末の種類で使える API は `Policies` で絞り、範囲の外は `ApiAuthorizationResultHandler` が `403` (`DEVICE_SCOPE`) にする
- 失敗は `ApiProblems` で Problem Details にし (`errorCode` は端末と共有する `TableOrder.Contract` の `ErrorCodes`)、新しい失敗は `ErrorCodes` と `ApiProblems` の定義の両方に足す
- Service の結果は `ApiResults` で応答にし、エンドポイントで入力の確かめや状態・errorCode の組み立てをしない (入口で行うのは公開鍵を JWK に書き直すような形の変換だけで、読めなければ null で渡して Service が断る)
- 本文を省ける API (null を許す本文の引数) は作らない。本文のない要求は本文を受ける経路に合わず、知らない経路の 404 になる
- 通知のハブ (`Hubs/StoreHub`) は端末をグループに入れて送るだけにし、グループの名前は `StoreHubGroups` で作る (テナントと店舗を入れる)
- ハブはグループに入れ終えたら `ready` で店舗の今の通し番号を送る (端末ははじめはその番号から数え、つなぎ直したら抜けた通知を読む)
- 端末の公開鍵は読んだ座標から書き直した JWK で持つ (`DevicePublicKeys`。同じ鍵を同じ文字列で引く)
- 端末の認証の失敗 (署名、期限、端末がない、使い捨ての値の使い回し、すぐに拒む一覧) はどれも `401` にし、理由を見せない (JwtBearer の `IncludeErrorDetails = false`。端末はトークンを取り直し、トークンの要求の `DEVICE_REVOKED` / `TENANT_SUSPENDED` で理由を知る)
- 流量の制限の `429` には `Retry-After` を付け、接続元は `RateLimits.PartitionOf` で数える (IPv6 は /64。同じ回線の中でアドレスを替えて試させない)
- 要求の値を読めない `400` (引数の結び付け、JSON の形の誤り、知らない項目) にも `VALIDATION_ERROR` を付ける (`CustomizeProblemDetails`)
- ハブはつないだ端末を `StoreHubConnections` に覚えて、すぐに拒む一覧に入った接続を切り、トークンの期限が来た接続も切る (`CloseOnAuthenticationExpiration`。WebSocket はつないだときにだけトークンを確かめるため)
