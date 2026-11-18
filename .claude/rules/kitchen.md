---
paths:
  - "server/src/TableOrder.KitchenApp/**"
---
# キッチン端末 (TableOrder.KitchenApp)

## 画面

- 画面は `Components/Pages` に `@page` を付けて置き、処理は `.razor.cs` の partial class に書く
- 部品は `Components/Controls` に置く (`Shared` は名前空間に使えない。CA1716)
- コンポーネントの DI は `[Inject] public required` のプロパティで受ける
- razor のフィールドとプロパティに、ディレクティブと同じ名前 (`code` など) を付けない (`@code` と取り違える)
- 画面をまたぐ値 (選んだ持ち場のタブなど) は State に置く
- 画面は `KitchenEventReceiver.Changed` で `InvokeAsync(StateHasChanged)` して再描画し、購読は `Dispose` で解除する
- 時計 (経過時間の更新) は画面が `PeriodicTimer` で持ち、`Dispose` で止める
- 起動を終える前に途中の画面の URL を開いたとき (再読み込みしたときなど) は、`MainLayout` が画面を作らずに起動へ戻す
- 画面は、起動で読んだ状態 (メニューなど) があるものとして書いてよい
- 操作を送っている間はボタンを押せなくする
- ほかの端末で先に進めていた失敗 (`LINE_STATUS_INVALID`、`NOT_FOUND`) はメッセージを出さずに、再取得した一覧を出す
- 数は `NumberPad` で入れる
- 入力の桁 (ペアリングコード、残りの数) は共有の `Length` から求め、数を直接書かない (サーバと端末のアプリと同じ値を使う)
- タップ領域は高さ 64px 以上、主な操作 (明細、下げる、下部のバー) は 72px 以上にする

## 起動と通知

- 起動からやり直すときと、端末の設定で再登録したときは、アプリを再読み込みする (`NavigateTo(BaseUri, forceLoad: true)`。通知の接続と状態を残さない)
- 起動に失敗したときの自動のリトライは端末のアプリと同じ間隔にし、テナントの停止で拒否されたら間隔を延ばす (5 分。案内にその間隔を出す)
- 起動で今の状態を読み終えるまで (`KitchenEventReceiver.Ready` の前) に届いた通知は捨てずにためておき、読み終えたあとに届いた順に扱う (読み終える前に起きた変化が、読んだ状態に入っていないことがある)
- チケットの再取得は操作のあとと通知の両方から重なって走るので、頼んだ順の番号を付け、あとに頼んだ再取得のレスポンスを古いレスポンスで上書きしない
- 通知での再取得に失敗したら、しばらくしてから再取得する

## デザインと文言

- 色の値は `wwwroot/css/app.css` の `:root` に役割の名前の CSS 変数 (`--primary-color` など) で持ち、ほかの場所は `var(--xxx-color)` で使う
- 色の値は、ホール端末の System にそろえる
- 状態の色の使い分けもホール端末にそろえる (品切れは失敗の色、残りの数と遅れは注意の色)
- 角丸、影、グラデーション、すりガラスを使わない
- アイコンは Material Icons のグリフ (`Components/Icons`) を `class="icon"` で出し、絵文字を使わない
- 横にスクロールするタブのバーは指でなぞって送り、スクロールバーを出さない (`scrollbar-width: none`。選んだタブの下線に重なる)
- 使っていないスタイルを `app.css` に残さない
- 画面の文言は `Resources/Strings/AppResources.resx` (日本語) と `.en.resx` (英語) に置き、`AppResources.Xxx` で引く
- resx を変えたら `AppResources.Designer.cs` も合わせる
- 値を埋め込む文言は `ViewHelper.Format` で組み立てる
- サーバの文字 (店舗の名前) は `ViewHelper.Text` で選ぶ
- メニューの名前 (料理、オプション、カテゴリ) と持ち場の名前は、ブラウザの言語によらずサーバが返す日本語のまま出す (チケットと品切れの画面で同じ名前にする)

## ブラウザ

- ブラウザの機能 (WebCrypto、IndexedDB、`localStorage`、Wake Lock、音) は `Browser/` の部品と `wwwroot/js/` の ES モジュールにまとめ、画面から JavaScript を直接呼ばない
- すぐ返る JavaScript は `IJSInProcessRuntime` / `IJSInProcessObjectReference` で同期で呼び、Promise を返すものは非同期で呼ぶ
- 起動を経ずに開いた画面 (再読み込みした端末の設定) からも使う部品 (端末の鍵) は、非同期の操作の中で必要な JavaScript の部品を読み込む
- 人の操作が要るブラウザの機能 (全画面、音を出す許可) は `pointerup` で求める (タッチの `pointerdown` は操作と認められない)

## 端末の鍵と接続先

- 端末の鍵は WebCrypto のエクスポートできない鍵にする (`extractable` を false)
- IndexedDB の操作は順に並べる
- WebCrypto が使えない (HTTPS か localhost でない) ときは、起動でメッセージを出して止まる
- 接続先は、アプリを配信したサーバ (`NavigationManager.BaseUri` の起点) にする
- 登録した端末の ID は、登録した接続先と組で `localStorage` に持つ
