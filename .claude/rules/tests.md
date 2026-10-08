---
paths:
  - "*/tests/**"
---
# テスト

- xunit v3 (Microsoft Testing Platform)。実行は `dotnet run --project`、キャンセルは `TestContext.Current.CancellationToken`
- テストのプロジェクトは対象の区分の `tests/` に `{対象のプロジェクト名}.Tests` で置き、テストは対象と同じフォルダ構成と名前空間に置く
- テスト名はアンダースコアなしの PascalCase (`SplitPaymentClosesVisitWhenFullyPaid`) にし、目的はメソッドの上に日本語のコメントで書く
- 本文は `// Arrange` / `// Act` / `// Assert` で区切る。準備がなければ `// Arrange` を省き、段階を追うシナリオは `// Act / Assert: 確かめること` を重ねる
- テストは実行順に依存させず、テストの間で状態を共有しない
- 計算のテストは、例の値と境界 (割り切れない、ちょうど、日をまたぐ、開店前) で確かめる
- 共有のプロジェクト (`Domain`、`Client`) が画面・端末・サーバに依存しないことは、各テストの `DependencyTests` で確かめる
- 端末の鍵の値の変換 (`DeviceCredentials`) は、Keystore と同じ形を返すテストの鍵で確かめ、サーバの API のテストでもその変換で登録とトークンが通ることを確かめる
- サーバのテストから `TableOrder.Client` を参照しても、サーバのソリューションには入れない (サーバの InspectCode が、端末だけが使う型を使われていないと指摘するため)
- API の結果は状態とエラーコード (`"ORDERING_PAUSED"` のような文字列) で確かめる
- 期待する額はメニューの値 (価格) を書かず、明細と `Pricing` から求める (サンプルのメニューを変えてもテストを壊さない)
- 通知は別のスレッドで届くので、届いた順に読んで時間を区切って待つ。送っていないことは、後から起こした通知が次に届くことで確かめる (通知は seq の順に届く)
- サーバの API のテストは、クラスごとのサーバ (`ServerFactory`。一時ファイルの DB とサンプルのデータ) で行い、テストごとに端末を登録して (`TestDevice`) ほかのテストと状態を分ける
- まだ API のない準備 (端末の無効化、テナントの停止、テスト用のテナント) は `ServerFactory` の決まった SQL と引数で行い、テナントを止めるテストは自分で作ったテナントを使う
- 来店や品切れを変えるテストは、テストごとの店舗 (`ServerFactory.CreateStoreAsync`) で行い、サンプルのデータの店舗を変えない
- 通知のハブのテストは `TestHubConnection` (Long Polling) でつなぎ、`ready` を受けてから操作する。`ready` の番号までの通知は端末と同じく読み飛ばす (送り手の遅れで、つないだあとに届くことがある)
- 端末のアプリの窓口 (`TableOrder.Client` の REST と SignalR) は、サーバのテストで `TestTerminal` (テストのサーバの中のハンドラと Long Polling) につないで確かめ、`Client/` の下に窓口と同じフォルダ (`Rest/`、`SignalR/`) で置く
- 注文のテストの単価は `TestMenu` (サーバのメニューと `Pricing`) で求め、メニューの価格を書かない
- 開発の環境の自動の進行は、`SimulationService` を DI から取り、時間を 0 にして店舗の文脈で呼んで確かめる (テストのサーバは自動の進行を止める)
- 管理画面の操作 (端末の管理、案内、チェーンと店舗の設定) は、Service を DI から取り、管理画面と同じく選んだ店舗の文脈 (`ServerFactory.BeginStore`) で呼ぶ
- テストのサーバの店舗の PIN は `ServerFactory.StaffPin` で、ハッシュの回数を少なくしている (テストを遅くしない)
- テーブル端末のテストの来店は、ホール端末で開く (`ServerFactory.OpenVisitAsync`。テーブル端末からは開けない)
- 画像は、サンプルの写真 (起動で `Assets/Images` から写したもの) か、`IImageStore` に直接置いたもので確かめる (テストのサーバの置き場はクラスごとの一時のフォルダ)
- テナントで分けられていることは、サンプルの 2 つのテナント (同じ店舗コード) の端末で、それぞれの店舗の値だけが返ることで確かめる
