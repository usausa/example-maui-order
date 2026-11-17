---
paths:
  - "*/tests/**"
  - "server/src/TableOrder.Server.Web/Assets/Data/SampleData.sql"
  - "server/src/TableOrder.Server.Web/Assets/Data/Menu.json"
  - "server/src/TableOrder.Server.Web/Assets/Data/MenuWashoku.json"
---
# テスト

## 実行

- テストは xunit v3 (Microsoft Testing Platform) で書く
- キャンセルには `TestContext.Current.CancellationToken` を使う
- テストは実行順に依存させない
- テストの間で状態を共有しない

## 配置と名前

- テストのプロジェクトは、対象の区分の `tests/` に `{対象のプロジェクト名}.Tests` で置く
- テストは、対象と同じフォルダ構成と名前空間に置く
- テスト名は、アンダースコアなしの PascalCase (`SplitPaymentClosesVisitWhenFullyPaid`) にする
- テストの目的は、メソッドの上に日本語のコメントで書く

## Arrange / Act / Assert

- テストの本体は `// Arrange`、`// Act`、`// Assert` で区切る
- 準備がなければ `// Arrange` を省く
- `// Arrange`、`// Act`、`// Assert` には説明を付けない
- 準備の意図は、その処理の行のコメントに書く
- 段階を追うシナリオは `// Act / Assert: 確認すること` を重ね、段階の間に `// Arrange` を挟まない
- 段階を追うシナリオの準備と変更前の値の記録は、最初の `// Arrange` か段階の中に書く
- 段階のないテストで、対象の呼び出しをすべて Assert の式の中に書くとき (複数行でもよい) は、説明のない `// Act / Assert` にする
- `// Act` と `// Assert` を分けるテストでは、レスポンスの読み取り (`ReadErrorCodeAsync`、`ReadPageAsync`) を `// Assert` に置く (リクエストと一体の `GetAsync<T>` は `// Act` でよい)
- 例外として、続きの前提 (書き込みの成否、次のリクエストや呼び出しに使う値) は、準備と `// Act` の途中で確認してよい

## 観点

- 計算のテストは、例の値と境界 (割り切れない、ちょうど、日をまたぐ、開店前) で検証する
- 共有のプロジェクトの依存の範囲は、各テストの `DependencyTests` で検証する (`Client` は画面・端末・サーバに依存しない。`Domain` はさらに `Contract`、`Client`、ASP.NET Core にも依存しない)
- テナントで分けられていることは、サンプルの 2 つのテナント (同じ店舗コード) の端末で、それぞれの店舗の値だけが返ることで検証する

## API

- API の結果は、ステータスコードとエラーコード (`"ORDERING_PAUSED"` のような文字列) で検証する
- errorCode のないレスポンス (204、304、401、429、画面のリダイレクト) は、ステータスコードで検証する
- ポリシーで拒否した 403 は、`DEVICE_SCOPE` まで検証する
- 端末の API は `TestDevice` で呼ぶ (JSON を送るリクエストは `PostAsync`、`PutAsync`、`PatchAsync`、成功の JSON を読む GET は `GetAsync<T>`)
- 例外として、本文のない POST、失敗やステータスコードだけを検証する GET、ヘッダの要るリクエストは `TestDevice.Client` を直接呼ぶ
- API と画面の経路は `ApiRoutes` で書き (`$"{ApiRoutes.Visits}/{visit.Id}/bill"`)、直書きしない (経路を変更すると、テストもビルドで気づくため)

## 端末の API クライアント

- 端末のアプリの API クライアント (`TableOrder.Client` の REST と SignalR) は、サーバのテストで `TestTerminal` を使い、テストのサーバの中のハンドラと Long Polling で接続して検証する
- API クライアントのテストは、サーバのテストの `Client/` の下に、API クライアントと同じフォルダ (`Rest/`、`SignalR/`) で置く
- 端末の鍵の値の変換 (`DeviceCredentials`) は、Keystore と同じ形を返すテストの鍵で検証する
- サーバの API のテストでも、`DeviceCredentials` の変換で登録とトークンが通ることを検証する
- サーバのテストから `TableOrder.Client` を参照しても、`TableOrder.Client` はサーバのソリューションに入れない (端末だけが使う型を、サーバの InspectCode が未使用と指摘するため)

## テストのサーバ

- サーバの API のテストは、一時ファイルの DB とサンプルのデータで起動するクラスごとのサーバ (`ServerFactory`) で行う
- テストごとに端末を登録して (`TestDevice`)、ほかのテストと状態を分ける
- 来店や品切れを変更するテストは、テストごとの店舗 (`ServerFactory.CreateStoreAsync`) で行い、サンプルのデータの店舗を変更しない
- テーブル端末のテストの来店は、ホール端末で開く (`ServerFactory.OpenVisitAsync`)
- テーブル端末と受付機は、来店の開き方を切り替えた店舗 (`CreateStoreAsync(VisitOpening.Xxx)`) でだけ来店を開ける
- テストのサーバの店舗の PIN は `ServerFactory.StaffPin` で、ハッシュの回数を少なくしてある (テストを遅くしないため)
- 端末の無効化・テナントの停止・テスト用のテナントの準備は、`ServerFactory` に用意した SQL で行う
- `ServerFactory` の端末の無効化とテナントの停止は、管理画面の操作と同じく失効リストも再読み込みする
- テナントを停止するテストは、そのテストで作ったテナントを使う
- ほかのサーバでの変更を失効リストの再読み込みで拒否することは、再読み込みの間隔を短くしたテストのサーバ (`new ServerFactory()` の `Settings`) で、失効リストを再読み込みせずに DB だけを変更して (`RevokeDeviceAsync(refresh: false)`) 検証する
- 画像は、サンプルの写真 (起動時に `Assets/Images` からコピーしたもの) か、`IImageStore` に直接置いたもので検証する (テストのサーバの保存先は、クラスごとの一時フォルダ)

## サンプルのデータ

- サンプルのデータ (`SampleData.sql`、`Menu.json`、`MenuWashoku.json`) を変更したら、`SampleData.cs`、`TestMenu.cs` と、値を検証するテストを合わせる
- サンプルのメニューの Id (商品、オプション、ルール、持ち場) は `TestMenu` の定数を使い、テストのクラスで定義し直さない (足りなければ `TestMenu` に足す)
- 期待する額は、メニューの価格を書かずに、明細と `Pricing` から求める (サンプルのメニューを変更してもテストを壊さないため)
- 注文のテストの単価は、メニューの価格を書かずに、`TestMenu` (サーバのメニューと `Pricing`) で求める

## 通知

- 通知は別のスレッドで届くので、届いた順に読み、タイムアウトを付けて待つ
- 通知を送っていないことは、後から起こした通知が次に届くことで検証する (通知は seq の順に届くため)
- 通知のハブのテストは `TestHubConnection` (Long Polling) で接続し、`ready` を受けてから操作する
- `ready` の番号までの通知は、端末と同じく読み飛ばす (ディスパッチャの遅れで、接続したあとに届くことがあるため)
- サーバが通知の接続を切ることは、`TestHubConnection.WaitClosedAsync` で検証する (テストの接続は再接続しない)
- 店舗の通知を送ったことを画面に伝えることは、`StoreActivity.Watch` で受け、タイムアウトを付けて待つ
- 画面に伝えないことは、`StoreActivity` を直接作り、`Notify` を呼んで検証する

## バックグラウンド処理

- 開発の環境のシミュレーションは、`SimulationService` を DI から取り、時間を 0 にして店舗のコンテキストで呼んで検証する (テストのサーバはシミュレーションを止める)
- 古いデータのクリーンアップのテストは、来店の営業日をさかのぼらせて (`ServerFactory.MoveVisitToPreviousDayAsync` の日数)、`CleanupService` を店舗のコンテキストで呼ぶ
- クリーンアップで子の表まで削除されたことは、表ごとの行の数 (`ServerFactory.CountVisitRowsAsync`) で検証する

## 管理画面

- 管理画面の操作 (端末の管理、店内状況、チェーンと店舗の設定) は、Service を DI から取り、管理画面と同じく選んだ店舗のコンテキスト (`ServerFactory.BeginStore`) で呼ぶ
- 管理画面のページ (サインイン、役割で絞ったページ) は、`TestAdmin` (Cookie を持ち、リダイレクトを自動では追わない) で開く
- フォームは、ページの偽造防止トークンと Blazor のフォームの名前を付けて送る
- リダイレクト先は `TestAdmin.LocationOf` で検証する
- ページの文字は `TestAdmin.ReadPageAsync` で検証する (日本語は文字参照で出るため)
- 管理画面のテストの利用者は `ServerFactory.CreateAdminUserAsync` でテストごとに作り、サンプルの利用者の資格情報を変更しない
