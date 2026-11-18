# General Constraints

- **General:** Follow the rules defined in `.editorconfig`.
- **Warnings:** Ensure there are no build warnings.
- **Suppress warnings:** If warning suppression is needed, ask before applying the fix.
- **Line endings:** Preserve existing line endings; use CRLF for newly created text files.
- **Comments:** Do not write comments in the code unless instructed.
- **Documentation:** Keep documentation minimal and concise. Do not write background context or rationale unless explicitly asked.

# プロジェクトの規則

## コーディングスタイル

- XAML は `Settings.XamlStyler` の規則に従う。`.editorconfig` と `Settings.XamlStyler` は変更しない
- クラスの DI は従来型のコンストラクタで受ける (プライマリコンストラクタは使わない)
- コンストラクタの引数とフィールドは `ILogger<T>` → Component (`INavigator`、`IDialog`、`IPopupNavigator`、Essentials) → State → Service の順に並べる

## 構成

- モノレポにし、区分ごとのフォルダ (`shared/`、`server/`、`table/`、`hall/`、`reception/`、`terminal/`) の `src/` にプロジェクト、`tests/` にテストを置く (構成は `docs/architecture.md`)
- ソリューションは、サーバ (`server/TableOrder.Server.slnx`) と端末のアプリごと (`table/TableOrder.TableApp.slnx` など) に分ける
- 端末のアプリはアプリごとの区分 (`table/`、`hall/`、`reception/`) に置き、用途の違うアプリを 1 つの区分に混ぜない
- 端末の共通部品は `terminal/` に置く (画面 (`ViewId`) を持たない .NET MAUI の Android のライブラリ。どの端末でも同じポップアップは持つ)
- 共有 (`shared/`) と端末の共通部品 (`terminal/`) は単独のソリューションを持たず、使う区分のソリューションに入れて検証する (単独にすると、アプリだけが使う型を InspectCode が未使用と指摘するため)
- 名前は `TableOrder.{Domain|Contract|Client}` (共有)、`TableOrder.Server.{Core|Web|AppHost}` (サーバ)、`TableOrder.{Table|Hall|Reception|Kitchen}App` (端末とブラウザで動くアプリ)、`TableOrder.Terminal` (端末の共通部品) にする
- 名前空間は、アセンブリの名前にフォルダを続ける
- 端末のアプリは Android だけを対象にする
- サーバとクライアントの業務の処理は、通信のエンドポイント (Minimal API / gRPC / SignalR) に依存させない。エンドポイントは受けたリクエストを業務の処理に渡すだけにする
- 業務の特例 (ドリンクバー、お酒、キッズなど) をコードで分岐せず、メニューのタグとルールの設定で表す
- 物理キーボードを前提にしない (キー入力の仕組みとフォーカスの移動は持たない)

## 検証

- 作業の単位ごとに `/verify` を通し、すべて OK にする
- テストは `dotnet run --project` で実行する。`dotnet test` は使わない
- UI の変更は `/emulator` でエミュレータで動作を確認し、画面を撮って見る。実機には入れない

## 進め方

- 実装の計画は `docs/plan.md` に置く。フェーズを終えたらチェックを付けて README の進捗の表も直し、計画と違う形にしたら計画を直す
- レビューの指摘は「どうあるべきか」の形で該当する規則ファイル (`.claude/rules/`) に足す
- コミットとプッシュは指示があったときだけ行う
- コミットのメッセージは日本語で、要約 1 行 (「。」を付けない) と変更点の箇条書きにする
- 実在の企業名・製品名・サービス名は Git に載せるファイル (文書、画面の文言、サンプルのデータ、コメント) に書かない (本番の環境に使う AWS のサービス名は除く)
- 公開しない情報は、名前が `__` で始まるファイル (Git の管理の外) に置く
- パッケージの追加・更新は事前に確認する
- パッケージのバージョンは csproj に直接書いて固定する (CPM や浮動バージョンは使わない)
- NuGet の脆弱性警告 (NU1902 / NU1903) は `NoWarn` で消さず、推移的依存を直接ピンして GHSA 番号をコメントに書く
