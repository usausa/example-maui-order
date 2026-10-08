---
paths:
  - "table/src/**"
  - "hall/src/**"
  - "reception/src/**"
  - "terminal/src/**"
---
# 端末の画面

## デザイン

- 角丸・影・グラデーション・すりガラスを使わない。タブ、カード、パネル、ボタン、ポップアップ、バッジは四角い面にする
- 面は罫線 (`HorizontalRule` / `VerticalRule`、または区切り線の色を間隔から見せる Grid) と色の面で区切る
- ヘッダ、タブ、下部の操作の帯は画面の端まで通し、隣の列 (注文リスト) の帯と高さを揃える
- 絵文字を使わない。記号は Material Icons のグリフにする
- 料理の写真は端末に保存したもの (`ImageCache.PathOf`) を出し、保存していない間は写真の場所に面の色とチェーンのロゴ (`PhotoPlaceholderImage`)、ロゴもなければ記号 (`PhotoPlaceholderLabel`) を出す (料理の絵を端末に同梱しない)
- ヘッダとカテゴリのタブは別の帯にする (1 本にまとめない)
- メニューのカードは 4 列 3 段を 1 画面に見せる。高さが足りないときはカードの間隔と帯を詰め、帯は押せる面の高さより低くしない
- カードの文字は 2 行の高さで揃える。名前の 1 行目は価格の上まで使い、価格は最後の行の右に置く (名前の後ろに価格と同じ文字を面の色で続けて幅を確保し、見える価格を重ねる)
- カードの価格は名前と同じ行の高さにして 1 行の高さだけ下げ、名前の 2 行目にそろえる
- 注文の内容 (選んだオプションなど) は省略 (…) せずに折り返して全部見せる
- 起動・端末の設定・電卓はシステムの画面とし、チェーンの色ではなく System の役割の色とシステムのロゴで組む

## 色

- 色は `Resources/Styles/Colors.xaml` の役割の名前 (`PrimaryColor`、`SecondaryColor`、`SurfaceColor`、`OnSurfaceColor`、`OutlineColor`、`ErrorColor` など) だけを使い、XAML と C# に色の値を直接書かない
- System の役割の色は `TableOrder.Terminal` の `SystemColors`、どの端末でも同じスタイルは `TerminalStyles` に置き、アプリの `App.xaml` でアプリの色の辞書とアプリのスタイルの辞書の間に入れる (`TerminalStyles` は `SystemColors` を中に入れ、アプリが値を持つ役割の色は `DynamicResource` で引く)
- 新しい用途の色が要るときは既存の役割で足りないかを先に考え、足りなければ役割として Colors.xaml に足す (画面ごとの色の名前は作らない)
- 面の色と文字の色は対 (`Xxx` と `OnXxx`) で使う
- チェーンの色はチェーンの設定 (`brand.theme`) で受け取り、起動の画面で `ThemeManager` が Brand・Neutral・Status の役割を替える (System の役割は替えない)
- スタイルで Brand・Neutral・Status の役割の色を引くときは `DynamicResource` にする (色はスタイルを作ったあとに替わる)。System の役割は `StaticResource` のままでよい
- Colors.xaml の値は既定の色にする。既定の色を替えたら面と文字のコントラスト比 4.5 以上を確かめる
- 色の役割を足したり名前を替えたりしたら、`TableOrder.Domain.ThemeRoles` (サーバの確かめと管理画面のチェーンの設定が使う) も合わせる
- C# で資源 (色など) を引くときは `FindResource` (`TryGetValue`) を使い、`ContainsKey` で探さない (`Source` で入れた辞書の中身は `ContainsKey` では見つからない)
- 記号 (`FontImageSource`) は `Markup/AppIcons` にグリフと色の役割の名前で足し、色の値を持たない。XAML からは `{x:Static markup:AppIcons.Xxx}` で使う
- ヘッダと待受のチェーンの印は `ViewHelper.Brand` (`BrandMark`) から出し、ロゴ (`BrandLogoImage`) を保存していなければ名前の頭の文字 (`BrandInitialLabel`) を出す (チェーンの名前・ロゴ・グリフを画面に書かない)
- 部品の色の既定値も色の値にせず、スタイルで役割の色を渡す (`QrCodeView.ForegroundColor` など)

## 文言

- 画面の文言は `Resources/Strings/AppResources.resx` (日本語) と `AppResources.en.resx` (英語) に置き、XAML は `{x:Static strings:AppResources.Xxx}`、C# は `AppResources.Xxx` で引く
- 共通の部品 (`TableOrder.Terminal`) が使う文言は `TerminalResources` (アプリの XAML から引けるように public) に置いてアプリの resx に重ねて持たず、言語を切り替えるときは `TerminalResources.Culture` も替える
- resx を変えたら `AppResources.Designer.cs` も合わせる (Visual Studio で保存すると作り直される)
- 値を埋め込む文言は `{0}` を使う書式にし、`ViewHelper.Format` で組み立てる
- 言語を切り替えたら表示中の画面を作り直して文言を引き直す (画面をまたぐ内容は State に置く)
- 言語のボタンには今の言語を出し、押したら選ぶポップアップ (`PopupNavigatorExtensions.LanguageAsync`) を開く (切り替え先を出すトグルにしない)
- 言語の名前はその言語で書く (`ViewHelper.LanguageName`。どの言語の画面でも読めるように)
- メニューの名前など、サーバから受ける文字は `LocalizedText.Get(language)` で選ぶ
- チェーンの名前はチェーンの設定 (`MenuState.BrandName`) から選んだ言語で出し、サーバの店舗の名前 (`storeName`) と混ぜない
- 選べる言語は店舗の設定の言語 (`LanguageState.Available`) にし、1 つなら言語のボタンを出さない

## 画面と遷移

- MainPage には帯を置かない。タブや操作の帯は各 View に置く
- 画面は `ViewId` に足して View に `[View(ViewId.Xxx)]` を、ポップアップは `DialogId` に足して `[Popup(DialogId.Xxx)]` を付ける
- どの端末でも同じポップアップ (電卓、知らせ、確認) は `TableOrder.Terminal` の `TerminalDialogId` に足し、入口は `TableOrder.Terminal` の `PopupNavigatorExtensions` に置く。アプリのポップアップの登録には `TerminalModules.DialogSource` も足す
- XAML で画面 ID を渡すときは `{markup:ViewId Xxx}` と書く (`x:Static` にしない)
- 端末の戻るは各画面で `OnNotifyBackAsync` (抽象) を実装して決める。お客様の画面では何もしない (アプリの外へ出さない)
- ViewModel のプロパティは `[ObservableProperty] public partial`、コマンドは `MakeAsyncCommand` / `MakeDelegateCommand` で作る
- 一覧の項目の操作は ViewModel の引数つきコマンドにし、項目からは `RelativeSource AncestorType` と `x:DataType` を付けたバインドで呼ぶ (前後を `ReSharper disable Xaml.BindingWithContextNotResolved` で挟む)
- ポップアップの ViewModel が読み直しを続けるときは、`CancellationTokenSource` を `Dispose` で止める (閉じると ViewModel が破棄される)
- ポップアップの結果を値の型で返すときは、開く側と同じ型 (Nullable も含めて) で `CloseAsync<T>` を呼ぶ (例: `CloseAsync<Language?>(x)`。型が違うと結果が渡らず null になる)
- コマンドの中で長く待たない (実行中は処理中の覆いで画面を止める)。支払の完了などを待つ繰り返しは、コマンドの外のタスクにする
- `AcceptsCommand` は画面の有効・無効 (遷移) に合わせて基底クラスが切り替える。各画面では書き換えず、止める必要があれば画面のフラグを用意する
- ボタンを押せなくするのは `MakeXxxCommand` の `canExecute` (画面の状態) で行う。実行中と遷移中は `BusyState` で止まる
- コマンドの外で待つ処理 (端末の戻るで API を呼ぶなど) は `using (BusyState.Begin())` で囲み、画面のボタンと重ならないようにする
- タイマーや裏のタスクと操作の両方から起きる処理 (支払の完了、待受に戻すなど) は、処理済みの印 (画面の状態、フラグ) を見て 2 回目を行わない。処理済みにするときは、API を待つ前に画面の状態を変える
- タイマーで画面を動かす処理は、操作の途中 (`BusyState.IsBusy`) なら行わない
- 読み直しの結果は、頼んだあとに操作で内容を反映していたら使わない (古い内容で上書きしない)
- サーバの通知は `OrderEventReceiver` が受けて状態を替え、操作の途中と遷移の間を待ってから `ShellEvent` で表示中の画面に知らせる。画面の動きは各画面が `OnVisitOpenedAsync` などで決める (受け手から画面を動かさない)
- 端末が使えなくなったとき (無効化、テナントの停止)、管理画面で端末を替えたとき (`device.updated`)、EMM が接続先を替えたとき、通知を追いかけられなくなったとき (`Expired`) は、`OrderEventReceiver` が `ShellEvent.Restart` を出し、表示中の画面が起動からやり直す (基底の `OnRestartAsync`。起動と端末の設定の画面は自分で確かめるので受けない)
- 起動の失敗 (通信できない、テーブルの割り当て待ち、テナントの停止) は、もう一度試すボタンのほかに、時間を置いて自動でもやり直す (店の端末は人が触らずに戻れるように)
- テーブルの名前は端末の設定 (`MenuState.TableName`) から出し、端末の側にテーブルを持たない
- テーブル端末が来店を開くのは、来店の開き方が席の店 (`MenuState.Features.VisitOpening`) だけにする (待受で人数を入れて始め、テーブルは送らない)。ほかの店は、スタッフや受付機が開いた知らせ (`OnVisitOpenedAsync`) で注文の画面に進む
- 待受の文言は来店の開き方で替える (スタッフは案内を待つ、受付機は受付機で受け付ける、席は人数を入れて始める)
- 通知は別の画面にいる間にも届くので、画面に入ったとき (`OnNavigatedToAsync`) にも状態を確かめる (来店が閉じていたら待受に戻すなど)
- 通知で変わる値 (人数、売り切れ) は ViewModel を作るときに読むだけにせず、知らせ (`OnVisitUpdatedAsync`、`OnStockUpdatedAsync`) で出し直す
- 来店が閉じたときと起動からやり直すときのポップアップは、`PopupCloseMessage` を受けた `Extender/PopupClosePlugin` が閉じる (処理の途中は終わってから、そのポップアップだけ)。各ポップアップに閉じる処理を持たせない
- ポップアップを外から閉じるときは、そのポップアップの `Popup.CloseAsync` で閉じる (`IPopupNavigator.CloseAsync` は一番上を閉じるので、先に閉じていると別のポップアップを閉じる)

## 配置

- 折り返して並べるタイルは `FlexLayout` を使わず、行に分けて `HorizontalStackLayout` で並べる (`FlexLayout` は縦の大きさを誤る)
- 表示を切り替える要素の列がある Grid は `ColumnSpacing` を使わず、要素の `Margin` で間を空ける (見えない列にも間隔が残り、隣の要素がずれる)
- スクロールする要素 (`ScrollView`、`CollectionView`) は、スタイルで `behaviors:Scroll.DisableOverScroll` を付ける
- 横にスクロールする帯 (カテゴリのタブなど) は、続きがある向きの端に送りのボタン (`behaviors:ScrollPager`) を重ねる
- ほかの要素を行の位置にそろえるラベルは `behaviors:LabelOption.FixedLineHeight` で行の高さを固定する (和文は CJK のフォントで行が広がり、英字と行の位置が変わる)
- 添付プロパティ (`Behaviors`) は MAUI のプロパティと同じ名前にしない (ハンドラの対応付けの同じキーに混ざり、Controls が飛ばす処理で一緒に飛ばされる)
- `GridItemsLayout` の間隔は端の項目の外側と見出し・末尾にも半分ずつ入る。外側の余白は `CollectionView` の `Margin` と見出しの高さからその分を引いて決める
- 画面全体のタッチは一番下の面に付け、上に重ねるボタンを含む親に `TapGestureRecognizer` を付けない (子のボタンのタップも拾う)
- 全画面と専用端末 (ロックタスク、Device Owner の制限、一時的な解除) は `Components/KioskManager` にまとめ、画面から直接 Android の API を呼ばない
- 全画面の間は、ポップアップの窓のシステムバーも隠す (`Extender/FullscreenPopupPlugin`)。ステータスバーの色の指定は全画面と合わせない
- CommunityToolkit の `TouchBehavior` は付けた要素の BindingContext を受け継がないので、要素に `x:Name` を付けて `BindingContext="{Binding Source={x:Reference Xxx}, Path=BindingContext, x:DataType={x:Type Border}}"` で渡す (前後を `ReSharper disable Xaml.BindingWithContextNotResolved` で挟む)
- スタッフメニューの入口は、ブランドの印の長押し (`AppGestures.StaffLongPress`) と PIN (`PopupNavigatorExtensions.VerifyStaffAsync`) にする。お客様の画面に入口のボタンを置かない
- PIN は `StaffLock` で確かめ (店舗の設定のハッシュ、間違いが続いたら止める)、起動に失敗したときの端末の設定にも同じ確かめを使う
- 店舗の設定で替わる動き (機能の有無 `MenuState.Features`、支払方法、呼び出しの用件、明細の上限) は設定から読み、端末に固定の値を持たない。使わない機能の操作は出さない
- チェーンと店舗の設定の変更 (設定の版) は待受で確かめ (入ったときと `store.updated`)、起動からやり直して反映する。来店の途中の画面では替えない

## 操作

- 物理キーボードを前提にしない。数値は電卓のポップアップで入力し、`PopupNavigatorExtensions` に入力の種類ごとのメソッドを置く
- 押せる面は高さ 64dp (約 10mm) 以上、主な操作は 80dp 以上にする
- 受ける / 断るのボタンは同じ大きさにする (提案を断りにくくしない)
- 押せない間の見た目がないボタン (記号つきの帯のボタンなど) は、押せない間は出さない
- EMM が配っている設定は画面で変えられないようにする。入力欄の代わりに値を出し、変える操作は出さずに「EMM で配られています」と添える
