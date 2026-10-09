---
paths:
  - "server/src/TableOrder.KitchenApp/**"
---
# キッチン端末 (TableOrder.KitchenApp)

## 画面

- 画面は `Components/Pages` に `@page` を付けて置き、中身は `.razor.cs` の partial class に書く。部品は `Components/Controls` に置く (`Shared` は名前空間に使えない。CA1716)
- コンポーネントの DI は `[Inject] public required` のプロパティで受ける
- razor のフィールドとプロパティに、ディレクティブと同じ名前 (`code` など) を付けない (`@code` と取り違える)
- 画面をまたぐ値は State に置き (選んだ持ち場のタブなど)、画面は `KitchenEventReceiver.Changed` で `InvokeAsync(StateHasChanged)` して出し直す。購読は `Dispose` で外す
- 時計 (経過時間の出し直し) は画面が `PeriodicTimer` で持ち、`Dispose` で止める
- 操作を送っている間はボタンを押せなくし、ほかの端末で先に進めていた失敗 (`LINE_STATUS_INVALID`、`NOT_FOUND`) は知らせずに読み直した一覧を出す
- 起動からやり直すときは、アプリを読み込み直す (`NavigateTo(BaseUri, forceLoad: true)`。通知の接続と状態を残さない)。端末の設定で登録し直したときも同じ
- 起動で今の状態を読み終えるまで (`KitchenEventReceiver.Ready` の前) に届いた通知は捨てずにためておき、読み終えたあとに届いた順に扱う (読み終える前に起きた変化が、読んだ状態に入っていないことがある)
- チケットの読み直しは操作のあとと通知の両方から重なって走るので、頼んだ順の番号で、あとに頼んだ読み直しの応答を古い応答で上書きしない。通知での読み直しに失敗したら、しばらくしてから読み直す
- 起動を経ずに開いた画面 (読み込み直した端末の設定) からも使う部品 (端末の鍵) は、非同期の操作の中で必要な JavaScript の部品を読み込む
- 起動を終える前に途中の画面の URL を開いたとき (読み込み直したときなど) は、`MainLayout` が画面を作らずに起動へ戻す。画面は起動で読んだ状態 (メニューなど) があるものとして書いてよい
- 物理キーボードを前提にしない。数は `NumberPad` で入れる
- 押せる面は高さ 64px 以上、主な操作 (明細、下げる、下の帯) は 72px 以上にする

## 見た目と文言

- 色の値は `wwwroot/css/app.css` の `:root` に役割の名前の CSS の変数 (`--primary-color` など) で持ち、ほかの場所は `var(--xxx-color)` で使う。値はホール端末の System にそろえた色にする
- 状態の色の使い分けもホール端末にそろえる (品切れは失敗の色、残りの数と遅れは注意の色)
- 角丸・影・グラデーション・すりガラスを使わない。記号は Material Icons のグリフ (`Components/Icons`) を `class="icon"` で出し、絵文字を使わない
- 横に送る帯 (タブ) は指でなぞって送り、スクロールバーを出さない (`scrollbar-width: none`。選んだタブの下線に重なる)
- 画面の文言は `Resources/Strings/AppResources.resx` (日本語) と `.en.resx` (英語) に置いて `AppResources.Xxx` で引き、`AppResources.Designer.cs` も合わせる。値を埋め込む文言は `ViewHelper.Format`、サーバの文字は `ViewHelper.Text` で選ぶ

## ブラウザ

- ブラウザの機能 (WebCrypto、IndexedDB、`localStorage`、Wake Lock、音) は `Browser/` の部品と `wwwroot/js/` の ES モジュールにまとめ、画面から JavaScript を直接呼ばない
- すぐ返る JavaScript は `IJSInProcessRuntime` / `IJSInProcessObjectReference` で同期で呼び、Promise を返すものは非同期で呼ぶ
- 人の操作が要るブラウザの機能 (全画面、音を出す許可) は `pointerup` で求める (タッチの `pointerdown` は操作と認められない)
- 端末の鍵は WebCrypto の取り出せない鍵にし (`extractable` を false)、IndexedDB の操作は順に並べる。WebCrypto が使えない (HTTPS か localhost でない) ときは起動で知らせて止まる
- 接続先はアプリを配ったサーバ (`NavigationManager.BaseUri` の起点) にし、登録した端末の id は登録した接続先と組で `localStorage` に持つ
