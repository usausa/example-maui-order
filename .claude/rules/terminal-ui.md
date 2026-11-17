---
paths:
  - "**/Modules/**"
  - "**/Markup/**"
  - "**/Behaviors/**"
  - "**/Extender/**"
  - "**/Messaging/**"
  - "**/App.xaml*"
  - "**/MainPage*"
  - "**/MauiProgram.cs"
  - "**/Platforms/Android/MainActivity.cs"
  - "table/src/TableOrder.TableApp/Controls/**"
  - "hall/src/TableOrder.HallApp/Controls/**"
  - "table/src/TableOrder.TableApp/Models/**"
  - "hall/src/TableOrder.HallApp/Models/**"
  - "terminal/src/TableOrder.Terminal/Models/**"
  - "table/src/TableOrder.TableApp/Resources/**"
  - "hall/src/TableOrder.HallApp/Resources/**"
  - "reception/src/TableOrder.ReceptionApp/Resources/**"
  - "terminal/src/TableOrder.Terminal/Resources/**"
  - "terminal/src/TableOrder.Terminal/Components/ThemeManager.cs"
---
# 端末の画面

## デザイン

### 面とバー

- 角丸・影・グラデーション・すりガラスを使わない。タブ、カード、パネル、ボタン、ポップアップ、バッジは四角い面にする
- 面は罫線 (`HorizontalRule` / `VerticalRule`、または区切り線の色を間隔から見せる Grid) と色の面で区切る
- バー (ヘッダ、タブ、下部の操作) は画面の端まで通し、隣の列 (注文リスト) のバーと高さをそろえる
- ヘッダとカテゴリのタブは別のバーにする (1 本にまとめない)

### 絵文字とアイコン

- 明るい背景 (白いボタン、タイル、タブ、メッセージ、空の状態) で文言に添えるアイコンは、色付きの絵文字にする
- 標準でテキスト表示になる文字 (🛎、🍽、↩、⏸、▶ など) を絵文字にするときは U+FE0F を付ける
- 色で塗ったボタンと濃い面 (待受、注意のメッセージ) のアイコンは、色付きの絵文字にせず白い単色のグリフ (Material Icons) にする (絵文字は背景に溶ける)
- 部品のアイコン (アイコンだけのボタン、送りのボタン、-/+、選んだ印、料理の写真の代わりのアイコン) は Material Icons のグリフにする
- 画面を移ったり操作を始めたりする白いボタンは、文言の前に絵文字を付ける (`{tmarkup:Emoji 🧾, Text=...}`)。値を選ぶボタンと、ポップアップを閉じる・戻るボタンには付けない
- 絵文字は言語によらないので resx に入れず、XAML (データで選ぶものは `ViewHelper`) に書く
- アイコン (`FontImageSource`) は `Markup/AppIcons` にグリフと色の役割の名前で足し、色の値を持たない
- `AppIcons` のアイコンは、XAML から `{x:Static markup:AppIcons.Xxx}` で使う

### 写真とチェーンの印

- 料理の写真は端末に保存したもの (`ImageCache.PathOf`) を出す (料理の絵を端末に同梱しない)
- 写真を保存していない間は、写真の場所に面の色とチェーンのロゴ (`PhotoPlaceholderImage`) を出し、ロゴもなければアイコン (`PhotoPlaceholderLabel`) を出す
- ヘッダと待受のチェーンの印は `ViewHelper.Brand` (`BrandMark`) から出し、ロゴ (`BrandLogoImage`) を保存していなければ名前の頭の文字 (`BrandInitialLabel`) を出す (チェーンの名前・ロゴ・グリフを画面に書かない)

### メニューのカードと注文の内容

- メニューのカードは 4 列 3 段を 1 画面に見せる。高さが足りないときはカードの間隔とバーを詰め、バーはタップ領域の高さより低くしない
- カードの文字は 2 行の高さでそろえる
- カードの名前の 1 行目は価格の上まで使い、価格は最後の行の右に置く (名前の後ろに価格と同じ文字を面の色で続けて幅を確保し、見える価格を重ねる)
- カードの価格は名前と同じ行の高さにして 1 行の高さだけ下げ、名前の 2 行目にそろえる
- 注文の内容 (選んだオプションなど) は省略 (…) せずに折り返して全部見せる

### ホール端末

- ホール端末の下のバーに 3 つ並べるボタンは、絵文字を付けても 1 行に収まる短い名詞の文言にする (人数変更、席移動、会計)
- ホール端末のポップアップで半分の幅に並べるボタンには短い文言だけを置き、変わる値 (選んだものの合計の金額など) はタイトルの側に出す (縦向きのスマートフォンでは、値を足した文言が 1 行に収まらない)

### 受付機

- 受付機は入口に縦に置くタブレット (1200x1920) の、受付機に固有の画面にする (テーブル端末の画面を流用しない)
- 受付機の Activity は縦向き (`SensorPortrait`) にする
- 受付機のお客様の画面は、上にチェーンのバー (印、名前、店舗の名前) を通し、人数と案内の画面はその下に受付の段階のバーを置く
- 受付機のお客様の画面の主な操作は、画面の幅いっぱいのボタンにして下に置く
- 受付機のお客様の画面は中央にそろえ、離れても読める大きさにする (案内のテーブルは主色の札にする)
- 受付機のお客様の画面には、待受に戻る時間を出す (人数は触らずに戻るまでの時間、案内は残りの秒)

## 色

### 役割の色

- 色は `Resources/Styles/Colors.xaml` の役割の名前 (`PrimaryColor`、`SecondaryColor`、`SurfaceColor`、`OnSurfaceColor`、`OutlineColor`、`ErrorColor` など) だけを使い、XAML と C# に色の値を直接書かない (リップルの既定の色は除く)
- 新しい用途の色が要るときは既存の役割で足りないかを先に考え、足りなければ役割として Colors.xaml に足す (画面ごとの色の名前は作らない)
- 面の色と文字の色は対 (`Xxx` と `OnXxx`) で使う
- Colors.xaml の値は既定の色にする
- 既定の色を変更したら、面と文字のコントラスト比が 4.5 以上かを確認する
- 色の役割を足したり名前を変更したりしたら、`TableOrder.Domain.ThemeRoles` (サーバの検証と管理画面のチェーンの設定が使う) も合わせる
- 部品の色の既定値も色の値にせず、スタイルで役割の色を渡す (`QrCodeView.ForegroundColor` など)
- リップルの色 (`TouchOption.RippleColor`) だけは既定値に色の値 (黒に近い色を薄くしたもの。明るい面の上で薄い灰色に見え、チェーンの色によらない) を持ち、役割の色を渡さずに既定のまま使う
- C# でリソース (色など) を引くときは `FindResource` (`TryGetValue`) を使い、`ContainsKey` で探さない (`Source` で入れた辞書の中身は `ContainsKey` では見つからない)

### System の色と共通のスタイル

- 起動・端末の設定・電卓はシステムの画面とし、チェーンの色ではなく System の役割の色とシステムのロゴで組む (電卓のタイトルは `KeypadTitleLabel`、罫線は `SystemRule`)
- System の役割の色は `TableOrder.Terminal` の `SystemColors` に、どの端末でも同じスタイルは `TerminalStyles` に置く
- アプリの `App.xaml` では、`TerminalStyles` をアプリの色の辞書とアプリのスタイルの辞書の間に入れる (`TerminalStyles` は `SystemColors` を中に入れる)
- `TerminalStyles` は、アプリが値を持つ役割の色を `DynamicResource` で引く

### チェーンの色

- チェーンの色はチェーンの設定 (`brand.theme`) で受け取り、起動の画面で `ThemeManager` が Brand・Neutral・Status の役割の色を切り替える (System の役割は切り替えない)
- ホール端末はチェーンの色に切り替えず、`Colors.xaml` の Brand・Neutral・Status の役割に System の色にそろえた値を持つ
- スタイルで Brand・Neutral・Status の役割の色を引くときは `DynamicResource` にする (色はスタイルを作ったあとに変わる)。System の役割は `StaticResource` のままでよい

## 文言

### resx と書式

- 画面の文言は `Resources/Strings/AppResources.resx` (日本語) と `AppResources.en.resx` (英語) に置き、XAML は `{x:Static strings:AppResources.Xxx}`、C# は `AppResources.Xxx` で引く
- 共通の部品 (`TableOrder.Terminal`) が使う文言は `TerminalResources` (アプリの XAML から引けるように public) に置き、アプリの resx に重ねて持たない
- resx を変更したら `AppResources.Designer.cs` も合わせる (Visual Studio で保存すると作り直される)
- 値を埋め込む文言は `{0}` を使う書式にし、`ViewHelper.Format` で組み立てる
- 数を埋め込む英語の文言は、1 でも 2 以上でも読める形にする (`Seats {0}`、`{0} pax`。数で文言を切り替えない)
- 時刻は店舗のタイムゾーン (`StoreHours.LocalDateTime`) で出し、端末のタイムゾーンの設定によらない

### サーバの文字

- メニューの名前など、サーバから受ける文字は `LocalizedText.Get(language)` で選ぶ
- チェーンの名前はチェーンの設定 (`MenuState.BrandName`) から選んだ言語で出し、サーバの店舗の名前 (`storeName`) と混ぜない

### 言語

- 言語を切り替えたら表示中の画面を作り直して文言を引き直す (画面をまたぐ内容は State に置く)
- 選べる言語は店舗の設定の言語 (`LanguageState.Available`) にし、1 つなら言語のボタンを出さない
- 言語のボタンには今の言語を出し、押したら選ぶポップアップ (`PopupNavigatorExtensions.LanguageAsync`) を開く (切り替え先を出すトグルにしない)
- 言語の名前はその言語で書く (`Language.NativeName`。どの言語の画面でも読めるように)
- お客様が変わるとき (テーブル端末は来店の終わり、受付機は案内を閉じたときと入れかけて離れたとき) は、言語を店舗の初めの言語に戻す (`LanguageState.Reset`)。同じお客様が続ける操作 (人数の画面の戻る) では戻さない
- 受付機の待受で言語を選んで受付せずに離れたら、しばらくして店舗の初めの言語に戻す
- ホール端末は言語を切り替える操作を持たず、文言とサーバの文字を端末の言語で選ぶ (`ViewHelper.Text`)

### エラー

- お客様の画面の失敗の文言は、errorCode を `ErrorCodes` の定数で resx の文言に割り当て、割り当てのないコードは共通の文言 (`ErrorGeneric`) にする (サーバが返す `Detail` の文言は担当者向けの日本語なので出さない)
- errorCode は `ErrorCodes` の定数で比べ、文字列で書かない
- 同じ errorCode でも操作で理由が違うときは、その操作の文言を出す (明細の取消の `LINE_STATUS_INVALID` は、提供済みか取消済み)
- お客様が頼んだ操作が通らなかったときは、ログだけにせず画面に失敗を出す (頼めたと思わせない)

## 画面と遷移

### 画面の構成

- `MainPage` は、画面を切り替えるコンテナと、全体に重ねるビジーオーバーレイだけを持つ
- `MainPage` にはバーを置かない。上部のタブや下部の操作のバーは各 View の中に置く
- 起動の進み具合は、起動の画面 (`ViewId.Startup`) が出す
- 画面は `ViewId` に足して View に `[View(ViewId.Xxx)]` を、ポップアップは `DialogId` に足して `[Popup(DialogId.Xxx)]` を付ける (登録は自動)
- XAML で画面 ID を渡すときは `{markup:ViewId Xxx}` と書く (`x:Static` にしない。マークアップ拡張の `Markup/ViewIdExtension` は使うアプリだけに置く)
- どの端末でも同じポップアップ (電卓、メッセージ、確認) は `TableOrder.Terminal` の `TerminalDialogId` に足し、開くメソッドは `TableOrder.Terminal` の `PopupNavigatorExtensions` に置く
- アプリのポップアップの登録には `TerminalModules.DialogSource` も足す
- ホール端末の下部のタブは、タブごとに画面 (`ViewId`) を分け、ヘッダとタブバーの部品 (`Controls/TabHeader`、`Controls/TabFooter`) を各画面に置く
- ホール端末のヘッダとタブバーの部品は、画面の ViewModel の基底クラス (`TabViewModelBase`) にバインドする

### ViewModel とコマンド

- ViewModel のプロパティは `[ObservableProperty] public partial`、コマンドは `MakeAsyncCommand` / `MakeDelegateCommand` で作る
- ViewModel (ポップアップと画面の枠の `MainPageViewModel` も) のクラスの前に「画面名: 役割」のコメントを書く (続く行に意図と制約を書いてよい)
- コマンドは行や欄の操作 (`-`、`+`、選ぶ) を先、閉じる (`CloseCommand`) と確定 (`CommitCommand`) を後にして、宣言と代入を同じ順に並べる
- 一覧の項目の操作は ViewModel の引数付きコマンドにし、項目からは `RelativeSource AncestorType` と `x:DataType` を付けたバインドで呼ぶ (前後を `ReSharper disable Xaml.BindingWithContextNotResolved` で挟む)
- ボタンを押せなくするのは `MakeXxxCommand` の `canExecute` (画面の状態) で行う。実行中と遷移中は `BusyState` で止まる
- `AcceptsCommand` は、遷移の間と上にポップアップを開いている間 (`Extender/PopupActivationPlugin`) を無効として基底クラスが切り替える。各画面では書き換えず、止める必要があれば画面のフラグを用意する
- コマンドの中で長く待たない (実行中はビジーオーバーレイで画面を止める)。支払の完了などを待つ繰り返しは、コマンドの外のタスクにする
- コマンドの外で待つ処理 (端末の戻るで API を呼ぶ・確認する、通知で再取得するなど) は `using (BusyState.Begin())` で囲み、画面のボタンと重ならないようにする

### 遷移と戻る

- 端末の戻るは各画面で `OnNotifyBackAsync` (abstract) を実装して決める。お客様の画面では何もしない (アプリの外へ出さない)
- 端末の戻るは画面の戻るのボタンと同じ条件で行い、ボタンを押せないとき (`CanBack` が false) は何もしない
- 通知は別の画面にいる間にも届くので、画面に入ったとき (`OnNavigatedToAsync`) にも状態を確認する (来店が閉じていたら待受に戻すなど)
- `OnNavigatedToAsync` は遷移の途中で呼ばれ、その中では遷移できない (例外になる)。入ったときの処理の結果でほかの画面に移るときは、`Navigator.PostForwardAsync` (移るだけ) / `Navigator.PostActionAsync` (処理ごと) で遷移を終えてから行う (終えるまでに別の画面に移っていたら行わない)
- 読み込みの結果で移る処理 (来店が終わっていたら戻るなど) は、開いたときの読み込みと通知での再取得が重なることがあるので、待ったあとに表示中の画面のままか (`IsCurrent`) を確認し、ほかの画面に移っていたら何もしない (`PostForwardAsync` は呼んだ画面を確認しない。移るのは表示中の画面からの 1 回だけにする)

### ポップアップ

- ポップアップの ViewModel は `AppDialogViewModelBase` を継承し、基底クラスの `CloseAsync` で閉じる (コンストラクタで `IPopupNavigator` を受け取らない。閉じ始めたら基底クラスがコマンドを止める)
- ポップアップの結果を値型で返すときは、開く側と同じ型 (Nullable も含めて) で `CloseAsync<T>` を呼ぶ (例: `CloseAsync<Language?>(x)`。型が違うと結果が渡らず null になる)
- ポップアップの下段の取りやめは `CloseCommand` にして結果を渡さない `CloseAsync()` で閉じ (開く側には null か既定の値が返る)、確定は `CommitCommand` にする
- ポップアップの ViewModel が定期的に再取得するときは、`CancellationTokenSource` を `Dispose` で止める (閉じると ViewModel が破棄される)
- 来店が閉じたときと起動からやり直すときのポップアップは、`PopupCloseMessage` を受けた `Extender/PopupClosePlugin` が閉じる (処理の途中は終わってから、そのポップアップだけ)。各ポップアップに閉じる処理を持たせない
- ポップアップを外から閉じるときは、そのポップアップの `Popup.CloseAsync` で閉じる (`IPopupNavigator.CloseAsync` は一番上を閉じるので、先に閉じていると別のポップアップを閉じる)
- 外から閉じるポップアップが、上に重なったポップアップのために閉じられないときは、間を置いて再確認する (その場で再確認すると、Busy の解除の通知から同期で入り直して繰り返す)

### 通知

- 通知で変わる値 (人数、会計中、売り切れ) は ViewModel を作るときに読むだけにせず、通知 (`OnVisitUpdatedAsync`、`OnStockUpdatedAsync`) で出し直す
- 結果を出している画面 (受付機の案内) は、起動からやり直すイベントで結果を消さず、閉じたときに起動へ移る

### 再取得と一覧

- 再取得の結果は、リクエストしたあとに操作で内容を反映していたら使わない (古い内容で上書きしない)
- ホール端末の一覧 (席、呼び出し、提供、品切れ) は、`ItemsHelper.Sync` で再取得した並びに合わせ、残る項目を作り直さない (スクロールの位置を保つ)
- `ItemsHelper.Sync` で残す項目も、出している値 (数量、時刻) はすべて更新する (並びが同じでも、一部の取消は同じ明細の数量を減らす)
- 一覧を作り直すとき (カテゴリを切り替えたときなど) は、`ObservableCollection` を差し替えずに `Clear` してから入れ直す (コレクションのプロパティは読み取り専用にする。CA2227)

### タイマー

- タイマーやバックグラウンドのタスクと操作の両方から起きる処理 (支払の完了、待受に戻すなど) は、処理済みか (画面の状態、フラグ) を見て 2 回目を行わない。処理済みにするときは、API を待つ前に画面の状態を変える
- タイマーで画面を動かす処理は、操作の途中 (`BusyState.IsBusy`) なら行わず、操作が終わるのを待ってから行う (やめたままにしない。お礼が出たまま残る)
- お客様が入れかけて離れることのある画面 (受付機の人数) は、しばらく触らなければ待受に戻し、触るたびに時間を数え直す
- 結果を出す画面 (受付機の案内) も、しばらくたったら待受に戻す
- 失敗のメッセージを開いたままお客様が離れることもあるので、触らない時間が来たらメッセージを閉じて (`PopupCloseMessage`) から待受に戻し、閉じたあとに時間を数え直さない

### 起動

- 起動の失敗 (通信できない、テーブルの割り当て待ち、テナントの停止) は、もう一度試すボタンのほかに、時間を置いて自動でもリトライする (店の端末は人が触らずに戻れるように)
- テナントの停止で拒否された起動は、自動のリトライの間隔を延ばし (5 分)、案内にその間隔を出す (再開まで長く続くので、止めている間にリクエストを送り続けない。もう一度試すボタンはすぐに確認する)
- 拒否された登録トークン (種類の違い、期限切れや取り消し) は待っても通らないので、起動の失敗でも自動ではリトライせず、自動のリトライのメッセージ (`IsAutoRetry`) も出さない (登録のレート制限を消費し続けない)
- 起動からやり直したときは、読んだ来店が前と同じときだけカートと言語を残す (来店が変わっていたら、前のお客様のものを引き継がない)

### 来店

- テーブル端末が来店を開くのは、来店の開き方が席の店 (`MenuState.Features.VisitOpening`) だけにする (待受で人数を入れて始め、テーブルは送らない)。ほかの店は、スタッフや受付機が来店を開いた通知 (`OnVisitOpenedAsync`) で注文の画面に進む
- 待受の文言は来店の開き方で切り替える (スタッフは案内を待つ、受付機は受付機で受け付ける、席は人数を入れて始める)

### 店舗と端末の設定

- 使わない機能の操作は出さない
- チェーンと店舗の設定の変更 (設定のバージョン) は待受で確認し (入ったときと `store.updated`)、起動からやり直して反映する。来店の途中の画面では切り替えない
- ホール端末は設定のバージョンをタブの画面で確認し (入ったときと `store.updated`)、起動からやり直して反映する。来店の詳細と代わりの注文の画面では切り替えない (入れかけの操作を捨てない)

### スタッフメニューと専用端末

- スタッフメニューの入口は、チェーンの印の長押し (`AppGestures.StaffLongPress`) と PIN (`PopupNavigatorExtensions.VerifyStaffAsync`) にする。お客様の画面に入口のボタンを置かない
- スタッフメニューは、しばらく操作しなければ、専用端末に戻してからお客様の画面に戻る (開いたまま離れると、お客様が端末の設定や Android の設定に触れる)
- 全画面の間は、ポップアップのウィンドウのシステムバーも隠す (`Extender/FullscreenPopupPlugin`)
- 全画面と一緒にステータスバーの色を指定しない

## 配置

- 折り返して並べるタイルは `FlexLayout` を使わず、行に分けて `HorizontalStackLayout` で並べる (`FlexLayout` は縦の大きさを誤る)
- 表示を切り替える要素の列がある Grid は `ColumnSpacing` を使わず、要素の `Margin` で間を空ける (見えない列にも間隔が残り、隣の要素がずれる)
- `GridItemsLayout` の間隔は、端の項目の外側とヘッダ・フッタにも半分ずつ入る。外側の余白は `CollectionView` の `Margin` とヘッダの高さからその分を引いて決める
- ホール端末 (スマートフォンの縦向き) と同じ部品を使う面 (ポップアップ、起動と端末の設定の列) に幅を決めるときは、`TerminalSizes` で画面に収める (幅を決めた要素は親の幅を越えて測られ、狭い画面からはみ出す)
- ほかの要素を行の位置にそろえるラベルは `behaviors:LabelOption.FixedLineHeight` で行の高さを固定する (和文は CJK のフォントで行が広がり、英字と行の位置が変わる)
- スクロールの端の効果 (伸びる、光る) はアプリ全体で止める (各アプリの `MainActivity` で適用するテーマの `NoOverScrollTheme` と、`behaviors:Scroll.DisableOverScroll` の既定の true)
- スタイルに `DisableOverScroll` を付けず、端の効果を出す要素だけ false にする
- 横にスクロールするバー (カテゴリのタブなど) は、続きがある向きの端に送りのボタン (`behaviors:ScrollPager`) を重ねる
- タップできる行とタイル (メニューのカード、注文リストの行、選ぶタイル、タブ) は `behaviors:TouchOption.ClickCommand` (と `ClickCommandParameter`) で受け、`behaviors:TouchOption.Ripple` でリップルを出す。`TapGestureRecognizer` は使わない (要素を増やさずにビューの `Foreground` にリップルを描き、上に重ねたボタンのタップは拾わない)
- CommunityToolkit の `TouchBehavior` は付けた要素の BindingContext を受け継がないので、要素に `x:Name` を付けて `BindingContext="{Binding Source={x:Reference Xxx}, Path=BindingContext, x:DataType={x:Type Border}}"` で渡す (前後を `ReSharper disable Xaml.BindingWithContextNotResolved` で挟む)
- 添付プロパティ (`Behaviors`) は MAUI のプロパティと同じ名前にしない (ハンドラのマッパーで同じキーに混ざり、Controls がスキップする処理で一緒にスキップされる)

## 操作

### 入力

- 数値は電卓のポップアップで入力する
- 電卓を開くメソッドは、入力の種類ごとに `PopupNavigatorExtensions` に置く
- 入力の桁は `Length` の上限から求め、別に持たない
- 電卓の引数は、数 (`NumberInputParameter`) と番号・PIN (`DigitsInputParameter`。先頭の 0 を残し、空も許す) で型を分け、画面で作らずに `PopupNavigatorExtensions` から渡す

### ボタンと表示

- タップ領域は高さ 64dp (約 10mm) 以上、主な操作は 80dp 以上にする
- 受ける / 断るのボタンは同じ大きさにする (提案を断りにくくしない)
- 押せない間の見た目がないボタン (アイコン付きのバーのボタンなど) は、押せない間は出さない
- 状態でどちらかしか通らない操作 (レジで払った / 取りやめ) は、ボタンを並べずに、通るほうだけを同じ場所に出す
- サーバが成功として返しても状態の変わらない操作 (払い終えた支払のある会計の取りやめ) は、その操作を出さずに理由を添え、返った状態が変わっていなければメッセージを出す
- EMM が配信している設定は、画面で変更できないようにする。入力欄の代わりに値を出し、変更の操作は出さずに、EMM で配信されていることを添える (例: 「EMM で配られた接続先を使います (この端末では変えられません)」)
- ホール端末の提供の一覧は、払い終えて閉じた来店のカードに会計済みを出す (同じテーブルの次のお客様の品と見分ける)

### 受付と注文の条件

- 時刻で変わる可否 (受付機のラストオーダー) は、画面を間隔ごとに出し直すだけでなく、操作したとき (受付する、席を決める) にも確認する
- 出せる条件 (時間帯、子どもがいる) を満たさない品とカテゴリは隠し、時刻の見直し (30 秒ごと) と人数の変更で出し直す
- タブとカードの並べ直しは、操作の途中 (`BusyState.IsBusy`) なら次の見直しで行う
- 出せる条件は、カートに入れるときと確定するときにも確認する
- 出せる条件を満たさないオプションは、品切れと同じく選べなくする
- カートの品が出せる条件を満たさなくなったら、行に理由 (時間外、お子様のみ) を出し、外すまで注文を確定させない (送れたかわからない注文の再送は止めない。届いていればサーバは受けた注文を返す)
- 端末で確認できる上限は、お客様に確認のルールを尋ねる前に見る (入れられないものに確認を求めない)
- オプションの品切れも商品と同じく出して選べなくする。既定のオプションも品切れなら選ばない
- カートの品を直すときは、選んでいた品切れのオプションを外せるようにし、選んだままでは入れられないようにする
- 受付機の開店前 (ラストオーダーの後のうち、その日の開店より前) は、終わりではなく受付の始まる時刻を出す
- 受付機は注文の一時停止の間も受け付け、待受と案内の画面に一時停止の文言 (店舗の文言、なければ既定の文言) を出す (お客様が席に着いてから知らないように)
