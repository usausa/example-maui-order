---
paths:
  - "tests/**"
---
# テスト

- xunit v3 (Microsoft Testing Platform)。実行は `dotnet run --project`、キャンセルは `TestContext.Current.CancellationToken`
- テストのプロジェクトは `tests/{対象のプロジェクト名}.Tests` にし、テストは対象と同じフォルダ構成と名前空間に置く
- テスト名はアンダースコアなしの PascalCase (`SplitPaymentClosesVisitWhenFullyPaid`) にし、目的はメソッドの上に日本語のコメントで書く
- 本文は `// Arrange` / `// Act` / `// Assert` で区切る。準備がなければ `// Arrange` を省き、段階を追うシナリオは `// Act / Assert: 確かめること` を重ねる
- テストは実行順に依存させない。モックはテストごとに作り、テストの間で状態を共有しない
- 計算のテストは、例の値と境界 (割り切れない、ちょうど、日をまたぐ、開店前) で確かめる
- 共有のプロジェクト (`Domain`、`Client`) が画面・端末・サーバに依存しないことは、各テストの `DependencyTests` で確かめる
- モックのテストは待ち時間を 0 (`Latency`、`PaymentAfter`、`EventDelay`) にして作り、API の結果は状態とエラーコード (`"ORDERING_PAUSED"` のような文字列) で確かめる
- 期待する額はモックのデータの値 (価格) を書かず、明細と `Pricing` から求める (モックのデータを変えてもテストを壊さない)
- 通知は別のスレッドで届くので、届いた順に読んで時間を区切って待つ。送っていないことは、後から起こした通知が次に届くことで確かめる (通知は seq の順に届く)
