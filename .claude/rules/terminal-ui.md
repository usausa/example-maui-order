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
- 起動・端末の設定・電卓はシステムの画面とし、チェーンの色ではなく System の役割の色とシステムのロゴで組む (電卓の表題は `KeypadTitleLabel`、罫線は `SystemRule`)
- ホール端末 (スマートフォンの縦向き) と同じ部品を使う面 (ポップアップ、起動と端末の設定の列) に幅を決めるときは、`TerminalSizes` で画面に収める (幅を決めた要素は親の幅を越えて測られ、狭い画面からはみ出す)
- ホール端末のポップアップで半分の幅に並べるボタンには短い文言だけを置き、変わる値 (選んだものの合計の金額など) は表題の側に出す (縦向きのスマートフォンでは、値を足した文言が 1 行に収まらない)
- 受付機は入口に縦に置くタブレット (1200x1920) の、受付機に固有の画面にする (テーブル端末の画面を流用しない)。Activity は縦向き (`SensorPortrait`) にする
- 受付機のお客様の画面は、上にチェーンの帯 (印、名前、店舗の名前) を通し、人数と案内の画面はその下に受付の段階の帯を置く。主な操作は画面の幅いっぱいのボタンにして下に置く
- 受付機のお客様の画面は中央にそろえ、離れても読める大きさにする (案内のテーブルは主色の札にする)。待受に戻る時間は画面に出す (人数は触らずに戻るまでの時間、案内は残りの秒)

## 色

- 色は `Resources/Styles/Colors.xaml` の役割の名前 (`PrimaryColor`、`SecondaryColor`、`SurfaceColor`、`OnSurfaceColor`、`OutlineColor`、`ErrorColor` など) だけを使い、XAML と C# に色の値を直接書かない
- System の役割の色は `TableOrder.Terminal` の `SystemColors`、どの端末でも同じスタイルは `TerminalStyles` に置き、アプリの `App.xaml` でアプリの色の辞書とアプリのスタイルの辞書の間に入れる (`TerminalStyles` は `SystemColors` を中に入れ、アプリが値を持つ役割の色は `DynamicResource` で引く)
- 新しい用途の色が要るときは既存の役割で足りないかを先に考え、足りなければ役割として Colors.xaml に足す (画面ごとの色の名前は作らない)
- 面の色と文字の色は対 (`Xxx` と `OnXxx`) で使う
- チェーンの色はチェーンの設定 (`brand.theme`) で受け取り、起動の画面で `ThemeManager` が Brand・Neutral・Status の役割を替える (System の役割は替えない)
- ホール端末はチェーンの色に替えず、`Colors.xaml` の Brand・Neutral・Status の役割に System の色に揃えた値を持つ
- スタイルで Brand・Neutral・Status の役割の色を引くときは `DynamicResource` にする (色はスタイルを作ったあとに替わる)。System の役割は `StaticResource` のままでよい
- Colors.xaml の値は既定の色にする。既定の色を替えたら面と文字のコントラスト比 4.5 以上を確かめる
- 色の役割を足したり名前を替えたりしたら、`TableOrder.Domain.ThemeRoles` (サーバの確かめと管理画面のチェーンの設定が使う) も合わせる
- C# で資源 (色など) を引くときは `FindResource` (`TryGetValue`) を使い、`ContainsKey` で探さない (`Source` で入れた辞書の中身は `ContainsKey` では見つからない)
- 記号 (`FontImageSource`) は `Markup/AppIcons` にグリフと色の役割の名前で足し、色の値を持たない。XAML からは `{x:Static markup:AppIcons.Xxx}` で使う
- ヘッダと待受のチェーンの印は `ViewHelper.Brand` (`BrandMark`) から出し、ロゴ (`BrandLogoImage`) を保存していなければ名前の頭の文字 (`BrandInitialLabel`) を出す (チェーンの名前・ロゴ・グリフを画面に書かない)
- 部品の色の既定値も色の値にせず、スタイルで役割の色を渡す (`QrCodeView.ForegroundColor` など)

## 文言

- 画面の文言は `Resources/Strings/AppResources.resx` (日本語) と `AppResources.en.resx` (英語) に置き、XAML は `{x:Static strings:AppResources.Xxx}`、C# は `AppResources.Xxx` で引く
- 共通の部品 (`TableOrder.Terminal`) が使う文言は `TerminalResources` (アプリの XAML から引けるように public) に置いてアプリの resx に重ねて持たない。言語は `LanguageState` で替え (`TerminalResources` のカルチャも替わる)、アプリの文言のカルチャを替える処理は `LanguageOptions` で渡す
- resx を変えたら `AppResources.Designer.cs` も合わせる (Visual Studio で保存すると作り直される)
- 値を埋め込む文言は `{0}` を使う書式にし、`ViewHelper.Format` で組み立てる
- 言語を切り替えたら表示中の画面を作り直して文言を引き直す (画面をまたぐ内容は State に置く)
- 言語のボタンには今の言語を出し、押したら選ぶポップアップ (`PopupNavigatorExtensions.LanguageAsync`) を開く (切り替え先を出すトグルにしない)
- 言語の名前はその言語で書く (`Language.NativeName`。どの言語の画面でも読めるように)
- メニューの名前など、サーバから受ける文字は `LocalizedText.Get(language)` で選ぶ
- State と Usecase は画面の書式 (`ViewHelper`) を使わず、サーバの文字は `LocalizedText` のまま返す (言語で選ぶのと文言の組み立ては ViewModel で行う)
- チェーンの名前はチェーンの設定 (`MenuState.BrandName`) から選んだ言語で出し、サーバの店舗の名前 (`storeName`) と混ぜない
- 選べる言語は店舗の設定の言語 (`LanguageState.Available`) にし、1 つなら言語のボタンを出さない
- お客様が替わるとき (テーブル端末は来店の終わり、受付機は案内を閉じたときと入れかけて離れたとき) は、言語を店舗の初めの言語に戻す (`LanguageState.Reset`)。同じお客様が続ける操作 (人数の画面の戻る) では戻さない
- ホール端末は言語を切り替える操作を持たず、文言とサーバの文字を端末の言語で選ぶ (`ViewHelper.Text`)
- 数を埋め込む英語の文言は、1 でも 2 以上でも読める形にする (`Seats {0}`、`{0} pax`。数で文言を切り替えない)
- 時刻は店舗のタイムゾーン (`StoreHours.LocalDateTime`) で出し、端末のタイムゾーンの設定によらない
- お客様の画面の失敗の文言は、errorCode を `ErrorCodes` の定数で resx の文言に割り当て、割り当てのないコードは共通の文言 (`ErrorGeneric`) にする (サーバの文言 (`Detail`) は担当者向けの日本語なので出さない)
- お客様が頼んだ操作が通らなかったときは、ログだけにせず画面に失敗を出す (頼めたと思わせない)
- errorCode は `ErrorCodes` の定数で比べ、文字列で書かない
- 同じ errorCode でも操作で理由が違うときは、その操作の文言を出す (明細の取消の `LINE_STATUS_INVALID` は、提供済みか取消済み)

## 画面と遷移

- MainPage には帯を置かない。タブや操作の帯は各 View に置く
- ホール端末の下部のタブは、タブごとに画面 (`ViewId`) を分け、ヘッダとタブの帯の部品 (`Controls/TabHeader`、`Controls/TabFooter`) を各画面に置く。部品は画面の ViewModel の基底 (`TabViewModelBase`) にバインドする
- 画面は `ViewId` に足して View に `[View(ViewId.Xxx)]` を、ポップアップは `DialogId` に足して `[Popup(DialogId.Xxx)]` を付ける
- どの端末でも同じポップアップ (電卓、知らせ、確認) は `TableOrder.Terminal` の `TerminalDialogId` に足し、入口は `TableOrder.Terminal` の `PopupNavigatorExtensions` に置く。アプリのポップアップの登録には `TerminalModules.DialogSource` も足す
- XAML で画面 ID を渡すときは `{markup:ViewId Xxx}` と書く (`x:Static` にしない)
- 端末の戻るは各画面で `OnNotifyBackAsync` (抽象) を実装して決める。お客様の画面では何もしない (アプリの外へ出さない)
- 端末の戻るは画面の戻るのボタンと同じ条件で行い、ボタンを押せないとき (`CanBack` が false) は何もしない
- ViewModel のプロパティは `[ObservableProperty] public partial`、コマンドは `MakeAsyncCommand` / `MakeDelegateCommand` で作る
- 一覧の項目の操作は ViewModel の引数つきコマンドにし、項目からは `RelativeSource AncestorType` と `x:DataType` を付けたバインドで呼ぶ (前後を `ReSharper disable Xaml.BindingWithContextNotResolved` で挟む)
- ポップアップの ViewModel が読み直しを続けるときは、`CancellationTokenSource` を `Dispose` で止める (閉じると ViewModel が破棄される)
- ポップアップの結果を値の型で返すときは、開く側と同じ型 (Nullable も含めて) で `CloseAsync<T>` を呼ぶ (例: `CloseAsync<Language?>(x)`。型が違うと結果が渡らず null になる)
- コマンドの中で長く待たない (実行中は処理中の覆いで画面を止める)。支払の完了などを待つ繰り返しは、コマンドの外のタスクにする
- `AcceptsCommand` は画面の有効・無効 (遷移) に合わせて基底クラスが切り替える。各画面では書き換えず、止める必要があれば画面のフラグを用意する
- ボタンを押せなくするのは `MakeXxxCommand` の `canExecute` (画面の状態) で行う。実行中と遷移中は `BusyState` で止まる
- コマンドの外で待つ処理 (端末の戻るで API を呼ぶ・確かめる、通知で読み直すなど) は `using (BusyState.Begin())` で囲み、画面のボタンと重ならないようにする
- タイマーや裏のタスクと操作の両方から起きる処理 (支払の完了、待受に戻すなど) は、処理済みの印 (画面の状態、フラグ) を見て 2 回目を行わない。処理済みにするときは、API を待つ前に画面の状態を変える
- タイマーで画面を動かす処理は、操作の途中 (`BusyState.IsBusy`) なら行わず、操作が終わるのを待ってから行う (やめたままにしない。お礼が出たまま残る)
- お客様が入れかけて離れることのある画面 (受付機の人数) は、しばらく触らなければ待受に戻し、触るたびに時間を数え直す。結果を出す画面 (受付機の案内) も、しばらくたったら待受に戻す
- 失敗の知らせを開いたままお客様が離れることもあるので、触らない時間が来たら知らせを閉じて (`PopupCloseMessage`) から待受に戻し、閉じたあとに時間を数え直さない
- スタッフメニューは、しばらく操作しなければ、専用端末に戻してからお客様の画面に戻る (開いたまま離れると、お客様が端末の設定や Android の設定に触れる)
- 受付機の待受で言語を選んで受付せずに離れたら、しばらくして店舗の初めの言語に戻す
- 結果を出している画面 (受付機の案内) は、起動からやり直す知らせで結果を消さず、閉じたときに起動へ移る
- 読み直しの結果は、頼んだあとに操作で内容を反映していたら使わない (古い内容で上書きしない)
- ホール端末の一覧 (席、呼び出し、提供、品切れ) は、`ItemsHelper.Sync` で読み直した並びに合わせ、残る項目を作り直さない (スクロールの位置を保つ)。残る項目も出している値 (数量、時刻) はすべて替える (並びが同じでも、一部の取消は同じ明細の数量を減らす)
- 一覧を作り直すとき (カテゴリを替えたときなど) は、`ObservableCollection` を差し替えずに `Clear` してから入れ直す (コレクションのプロパティは読み取り専用にする。CA2227)
- サーバの通知は `OrderEventReceiver` が受けて状態を替え、操作の途中と遷移の間を待ってから `ShellEvent` で表示中の画面に知らせる。画面の動きは各画面が `OnVisitOpenedAsync` などで決める (受け手から画面を動かさない)
- 端末が使えなくなったとき (無効化、テナントの停止)、管理画面で端末を替えたとき (`device.updated`)、EMM が接続先を替えたとき、通知を追いかけられなくなったとき (`Expired`) は、`OrderEventReceiver` が `ShellEvent.Restart` を出し、表示中の画面が起動からやり直す (基底の `OnRestartAsync`。起動と端末の設定の画面は自分で確かめるので受けない)
- 起動の失敗 (通信できない、テーブルの割り当て待ち、テナントの停止) は、もう一度試すボタンのほかに、時間を置いて自動でもやり直す (店の端末は人が触らずに戻れるように)
- テナントの停止で断られた起動は、自動でやり直す間を延ばし (5 分)、案内にその間隔を出す (再開まで長く続くので、止めている間に要求を送り続けない。もう一度試すはすぐに確かめる)
- 断られた登録トークン (種類の違い、期限切れや取り消し) は待っても通らないので、起動の失敗でも自動ではやり直さず、自動でやり直す知らせ (`IsAutoRetry`) も出さない (登録の流量を使い続けない)
- テーブルの名前は端末の設定 (`MenuState.TableName`) から出し、端末の側にテーブルを持たない
- テーブル端末が来店を開くのは、来店の開き方が席の店 (`MenuState.Features.VisitOpening`) だけにする (待受で人数を入れて始め、テーブルは送らない)。ほかの店は、スタッフや受付機が開いた知らせ (`OnVisitOpenedAsync`) で注文の画面に進む
- 待受の文言は来店の開き方で替える (スタッフは案内を待つ、受付機は受付機で受け付ける、席は人数を入れて始める)
- 通知は別の画面にいる間にも届くので、画面に入ったとき (`OnNavigatedToAsync`) にも状態を確かめる (来店が閉じていたら待受に戻すなど)
- `OnNavigatedToAsync` は遷移の途中で呼ばれ、その中では遷移できない (例外になる)。入ったときの処理の結果でほかの画面に移るときは、`Navigator.PostForwardAsync` (移るだけ) か `Navigator.PostActionAsync` (処理ごと) で遷移を終えてから行う (終えるまでに別の画面に移っていたら行わない)
- 読み込みの結果で移る処理 (来店が終わっていたら戻るなど) は、開いたときの読み込みと通知での読み直しが重なることがあるので、`Navigator.PostForwardAsync` に画面 (`this`) を渡し、表示中の画面からの 1 回だけ移る
- テーブル端末の来店が終わったか (閉じた、取りやめた、ほかのテーブルに移った) は `VisitState.IsFinished` で見る (状態の `Closed` だけで見ない。移った来店の状態は `Open` のまま届く)
- 起動からやり直したときは、読んだ来店が前と同じときだけカートと言語を残す (来店が替わっていたら、前のお客様のものを引き継がない)
- テーブル端末は、来店が終わってから画面が来店を終えるまで (お礼の間、スタッフメニューの間) に同じテーブルで開いた次の来店 (移ってきた来店も) を `VisitState.Next` に残し、来店を終えたら続けて開いて注文の画面にする (知らせを捨てると、次のお客様の来店が待受のまま止まる)
- 通知で変わる値 (人数、会計中、売り切れ) は ViewModel を作るときに読むだけにせず、知らせ (`OnVisitUpdatedAsync`、`OnStockUpdatedAsync`) で出し直す
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
- ホール端末は設定の版をタブの画面で確かめ (入ったときと `store.updated`)、起動からやり直して反映する。来店の詳細と代わりの注文の画面では替えない (入れかけの操作を捨てない)

## 操作

- 物理キーボードを前提にしない。数値は電卓のポップアップで入力し、`PopupNavigatorExtensions` に入力の種類ごとのメソッドを置く。桁は `Length` の上限から求め、別に持たない
- 押せる面は高さ 64dp (約 10mm) 以上、主な操作は 80dp 以上にする
- 受ける / 断るのボタンは同じ大きさにする (提案を断りにくくしない)
- 押せない間の見た目がないボタン (記号つきの帯のボタンなど) は、押せない間は出さない
- EMM が配っている設定は画面で変えられないようにする。入力欄の代わりに値を出し、変える操作は出さずに「EMM で配られています」と添える
- 状態でどちらかしか通らない操作 (レジで払った / 取りやめ) は、ボタンを並べずに、通るほうだけを同じ場所に出す
- ホール端末の提供の一覧は、払い終えて閉じた来店のカードに会計済みを出す (同じテーブルの次のお客様の品と見分ける)
- サーバが成功として返しても状態の変わらない操作 (払い終えた支払のある会計の取りやめ) は、出さずに理由を添え、返った状態が変わっていなければ知らせる
- 時刻で替わる可否 (受付機のラストオーダー) は、画面を間隔ごとに出し直すだけでなく、操作したとき (受付する、席を決める) にも確かめる
- 受付機の開店前 (ラストオーダーの後のうち、その日の開店より前) は、終わりではなく受付の始まる時刻を出す
- 受付機は注文の一時停止の間も受け付け、待受と案内の画面に一時停止の文言 (店舗の文言、なければ既定の文言) を出す (お客様が席に着いてから知らないように)
- カートに入れる前の上限 (1 回の注文の明細の数、1 明細の数量 (同じ内容の行にまとめた数)、メニューのルールの上限、残りの数 (商品とオプション)) は `OrderUsecase.FindExceededLimit` で確かめ、注文の画面と提案の追加で同じ確かめを通す
- 端末で確かめられる上限は、お客様に確認のルールを尋ねる前に見る (入れられないものに確認を求めない)
- オプションの品切れも商品と同じく出して選べなくする (既定のオプションも品切れなら選ばない。直すときに選んでいた品切れのオプションは外せるようにし、選んだままでは入れられない)
