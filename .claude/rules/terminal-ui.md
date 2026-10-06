---
paths:
  - "src/TableOrder.Terminal.*/MainPage.xaml"
  - "src/TableOrder.Terminal.*/Modules/**"
  - "src/TableOrder.Terminal.*/Controls/**"
  - "src/TableOrder.Terminal.*/Extender/**"
  - "src/TableOrder.Terminal.*/Markup/**"
  - "src/TableOrder.Terminal.*/Resources/**"
---
# 端末の画面

## デザイン

- 角丸・影・グラデーション・すりガラスを使わない。タブ、カード、パネル、ボタン、ポップアップ、バッジは四角い面にする
- 面は罫線 (`HorizontalRule` / `VerticalRule`、または区切り線の色を間隔から見せる Grid) と色の面で区切る
- ヘッダ、タブ、下部の操作の帯は画面の端まで通し、隣の列 (注文リスト) の帯と高さを揃える
- 絵文字を使わない。記号は Material Icons のグリフにする
- 料理の写真がない間 (モック) は、料理の絵 (`Resources/Images/food_*.svg`、4:3) を写真の代わりに置く
- ヘッダとカテゴリのタブは別の帯にする (1 本にまとめない)
- メニューのカードは 4 列 3 段を 1 画面に見せる。高さが足りないときはカードの間隔と帯を詰め、帯は押せる面の高さより低くしない
- カードの文字は 2 行の高さで揃える。名前の 1 行目は価格の上まで使い、価格は最後の行の右に置く (名前の後ろに価格と同じ文字を面の色で続けて幅を確保し、見える価格を重ねる)
- 起動・端末の設定・電卓はシステムの画面とし、チェーンの色ではなく System の役割の色とシステムのロゴで組む

## 色

- 色は `Resources/Styles/Colors.xaml` の役割の名前 (`PrimaryColor`、`SecondaryColor`、`SurfaceColor`、`OnSurfaceColor`、`OutlineColor`、`ErrorColor` など) だけを使い、XAML と C# に色の値を直接書かない
- 新しい用途の色が要るときは既存の役割で足りないかを先に考え、足りなければ役割として Colors.xaml に足す (画面ごとの色の名前は作らない)
- 面の色と文字の色は対 (`Xxx` と `OnXxx`) で使う
- チェーンのイメージカラーは Colors.xaml の Brand の節で替える。替えたら面と文字のコントラスト比 4.5 以上を確かめる
- 記号 (`FontImageSource`) は `Markup/AppIcons` にグリフと色の役割の名前で足し、色の値を持たない。XAML からは `{x:Static markup:AppIcons.Xxx}` で使う
- チェーンの印は `AppIcons.BrandGlyph` の 1 か所に置き、ヘッダ (`AppIcons.Brand`) と待受の印はそこから引く (グリフを画面に直接書かない)
- 部品の色の既定値も色の値にせず、スタイルで役割の色を渡す (`QrCodeView.ForegroundColor` など)

## 文言

- 画面の文言は `Resources/Strings/AppResources.resx` (日本語) と `AppResources.en.resx` (英語) に置き、XAML は `{x:Static strings:AppResources.Xxx}`、C# は `AppResources.Xxx` で引く
- resx を変えたら `AppResources.Designer.cs` も合わせる (Visual Studio で保存すると作り直される)
- 値を埋め込む文言は `{0}` を使う書式にし、`ViewHelper.Format` で組み立てる
- 言語を切り替えたら表示中の画面を作り直して文言を引き直す (画面をまたぐ内容は State に置く)
- メニューの名前など、サーバから受ける文字は `LocalizedText.Get(language)` で選ぶ
- チェーンの名前は resx の `BrandName` に置いて `{x:Static strings:AppResources.BrandName}` で引き、サーバの店舗の名前 (`storeName`) と混ぜない

## 画面と遷移

- MainPage には帯を置かない。タブや操作の帯は各 View に置く
- 画面は `ViewId` に足して View に `[View(ViewId.Xxx)]` を、ポップアップは `DialogId` に足して `[Popup(DialogId.Xxx)]` を付ける
- XAML で画面 ID を渡すときは `{markup:ViewId Xxx}` と書く (`x:Static` にしない)
- 端末の戻るは各画面で `OnNotifyBackAsync` (抽象) を実装して決める。お客様の画面では何もしない (アプリの外へ出さない)
- ViewModel のプロパティは `[ObservableProperty] public partial`、コマンドは `MakeAsyncCommand` / `MakeDelegateCommand` で作る
- 一覧の項目の操作は ViewModel の引数つきコマンドにし、項目からは `RelativeSource AncestorType` と `x:DataType` を付けたバインドで呼ぶ (前後を `ReSharper disable Xaml.BindingWithContextNotResolved` で挟む)
- ポップアップの ViewModel が読み直しを続けるときは、`CancellationTokenSource` を `Dispose` で止める (閉じると ViewModel が破棄される)
- コマンドの中で長く待たない (実行中は処理中の覆いで画面を止める)。支払の完了などを待つ繰り返しは、コマンドの外のタスクにする

## 配置

- 折り返して並べるタイルは `FlexLayout` を使わず、行に分けて `HorizontalStackLayout` で並べる (`FlexLayout` は縦の大きさを誤る)
- `GridItemsLayout` の間隔は端の項目の外側と見出し・末尾にも半分ずつ入る。外側の余白は `CollectionView` の `Margin` と見出しの高さからその分を引いて決める
- 画面全体のタッチ (待受など) は一番下の面に付け、上に重ねるボタンを含む親に `TapGestureRecognizer` を付けない (子のボタンのタップも拾う)
- 全画面にしている間は、ポップアップの窓のシステムバーも隠す (`Extender/FullscreenPopupPlugin`)。ステータスバーの色の指定は全画面と合わせない

## 操作

- 物理キーボードを前提にしない。数値は電卓のポップアップで入力し、`PopupNavigatorExtensions` に入力の種類ごとのメソッドを置く
- 押せる面は高さ 64dp (約 10mm) 以上、主な操作は 80dp 以上にする
- 受ける / 断るのボタンは同じ大きさにする (提案を断りにくくしない)
