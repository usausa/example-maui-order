---
paths:
  - "server/src/TableOrder.Server.Web/Components/**"
  - "server/src/TableOrder.Server.Web/Application/Account/**"
  - "server/src/TableOrder.Server.Web/Application/Context/**"
  - "server/src/TableOrder.Server.Web/Application/AdminPolicies.cs"
  - "server/src/TableOrder.Server.Web/Application/ApplicationExtensions.cs"
  - "server/src/TableOrder.Server.Web/Hubs/StoreActivity.cs"
  - "server/src/TableOrder.Server.Web/wwwroot/**"
---
# 管理画面 (Blazor Server)

## 認証

- 管理画面はどの環境でも開き、ページはサインインを求める (`MapRazorComponents` に `AdminPolicies.SignedIn`)
- `[AllowAnonymous]` は、サインインの前に開く画面 (サインイン、多要素のコード、エラー) と、お客様が開く電子レシートの画面にだけ付ける
- 管理画面の認証方式は Cookie に限り (ポリシーは `IdentityConstants.ApplicationScheme`)、端末の API と通知のハブはアクセストークンに限る (既定の認証方式は経路で選ぶ)
- 役割で絞る画面は `[Authorize(Policy = AdminPolicies.Xxx)]` を付け、メニュー (`NavMenu`) にも出さない
- 利用者の扱える範囲は `AdminScope` (サインインのクレームの役割、テナント、受け持つ店舗) で決め、`StoreSelection` は範囲の外を選ばない (画面で選んだ値をそのまま Service のコンテキストにしない)
- Cookie を書く画面 (サインイン、パスワード、多要素) は `[ExcludeFromInteractiveRouting]` の静的な画面にして `AccountLayout` で出し、入力は対話しない素の部品 (`InputText`) にする
- フォームの値 (`[SupplyParameterFromForm]`) は初期化子を付けず、`OnInitialized` で作る
- 静的な画面から遷移するときは、`NavigationManager.NavigateTo` のあとに処理を続けない (`return` する)
- 戻る先は `AccountPaths.LocalReturnPath` で管理画面の中の経路に限る
- サインインできるか (止めた利用者、止めたテナント) は、サインインのほか Cookie の再検証 (`ValidateSecurityStampAsync`) と再発行 (`RefreshSignInAsync`) でも `AdminSignInManager` で確認する
- セキュリティスタンプ (`SecurityStamp`) は、利用者の扱える範囲と資格情報 (役割、受け持つ店舗、パスワード、多要素、止める) を変更したときだけ変え、名前だけの変更では変えない
- セキュリティスタンプを変えたら、開いている管理画面をサインインからやり直させる
- 開いている回線の再検証 (`AdminAuthenticationStateProvider`) は、回線を始めてから Cookie の期限を過ぎたら false にする (回線の操作は Cookie を延ばさないため)
- 仮のパスワードの平文は、出した画面で一度だけ見せる
- 仮のパスワードの間は、パスワードの画面のほか (アカウント、多要素) をすべてパスワードの画面に移す
- 今と同じパスワードへの変更は拒否する
- 回復用のコードは、認証アプリの鍵と同じくデータ保護で暗号化して持つ (桁の少ないコードをソルトのないハッシュで持たない)
- Identity の更新は結果 (`IdentityResult`、回復用のコードの null) を確認し、書けなかったら Cookie を再発行せずにメッセージを出す
- Identity の続けて書く更新は、1 回にまとめるか、途中で書けなくても食い違わない順にする

## ページ

- 管理画面のページは `AppPageBase` を継承し、選んだテナントと店舗 (`StoreSelection`) のコンテキストで Service を呼ぶ
- ほかの部品からのイベント (店舗の選び直し、店舗の通知) で再読み込みするときは、`AppPageBase.ReloadAsync` で行う (コンテキストを始め、失敗を画面の `ErrorBoundary` に渡す。イベントの中の例外を捨てない)
- 店舗の通知で再読み込みする管理画面は、選んだ店舗を `StoreActivity.Watch` で監視する (店舗を変更したら、監視する店舗も切り替える)
- `StoreActivity` からディスパッチャのスレッドで呼ばれたら、少し待って 1 回だけ、画面のコンテキスト (`InvokeAsync` と `BeginServiceScope`) で再読み込みする
- 再読み込みで、選んだものと入力の途中の値を消さない
- 店舗の選択 (`StoreSelector`) もページと同じ `ErrorBoundary` の中に置く (外の失敗は回線ごと終わり、選択と入力を失うため)
- `MudDialogProvider` は `MainLayout` の `ErrorBoundary` の中にだけ置く (ダイアログの例外も `ErrorBoundary` が受け、回線の未処理例外にしないため)
- `ErrorBoundary` の外の部品 (上部のバー、メニュー) からダイアログを開かない (エラーを出している間は `MudDialogProvider` がないため)
- 発行や追加のように重ねて押すと 2 回行うボタンは、`AppPageBase.RunOnceAsync` で 1 回にし、処理中は `Busy` で押せなくする
- テナントを選び直したら、前のテナントの店舗を指す入力 (受け持つ店舗、設定のコピー元の店舗) を消す

## 文言と書式

- 失敗の文言は `AdminNames.ErrorMessage` で作り、画面ごとの文言は errorCode から返す関数で渡す (骨組みを画面ごとにコピーしない)
- どの画面でも同じもの (利用者の状態、出した仮のパスワード、確認のダイアログ) は `Components` にまとめる
- 管理画面に列挙型の値を出すときは `AdminNames` の名前から引き、ページとダイアログに名前を書かない (列挙型の名前をそのまま出さない)
- 列挙型の入力の選択肢は、注記を添えた文言をその場に書いてよい
- 管理画面の時刻、店舗の名前、金額、件数の書式は `AdminNames` で作り、画面ごとに組まない
- 時刻の書式は、今日の動きは時刻だけ、近い日時は月日から、何年も前のことがある日時 (最後のサインイン、最後の通信) は年からにする
- 管理画面の時刻は 1 つに決めたタイムゾーンで出し、見出しにタイムゾーンを書く
- 見出しのない表の時刻は、画面の上にタイムゾーンを書く
- 金額は 3 桁で区切って「円」を付け (`AdminNames.FormatAmount`)、件数・人数・台数は 3 桁で区切る (`AdminNames.FormatCount`)
- 画面の構成とボタンの文言でわかる案内 (ページの見出しの下の説明、「[保存] で反映します」「行を押すと詳細を出します」のような手順や操作の説明、保存したあとの端末の動き) を出さない
- 説明の文言は、画面からわからない入力のルール (入力欄の `HelperText`)、一度しか出さない値の注意 (仮パスワード、登録トークン)、事実 (「〜がありません」) に限る
- 管理画面の文言は業務システムの用語にし (追加、変更、保存、キャンセル、再読み込み、発行、再発行、停止、再開、利用停止、無効化、割り当て、未提供、担当店舗など)、和語の言い換え (足す、替える、止める、戻す、出す、写す、読み直す) にしない
- ボタン、列の見出し、状態、通知の種類の名前は名詞にする (「割り当てる」ではなく「割り当て」、「使える」ではなく「利用可能」、「明細が進んだ」ではなく「明細の更新」)
- 画面の文言 (表示、メッセージ、入力の誤り) は「。」の要らない短い句にし、文を重ねず、最後に「。」を付けない
- 確認のダイアログは `DialogServiceExtensions.ConfirmAsync` で出す
- 確認のダイアログの本文は「〜しますか？」に、取り消しにくい影響を短い句で 1 つだけ添える (「？」の後に空白を置く)
- 確認のダイアログの確定のボタンは、操作の名詞 (停止、無効化、取消) にする

## お客様が開く画面

- お客様が開く画面 (電子レシート) は、管理画面の外の素のレイアウト (`ReceiptLayout`) の静的な画面 (`[ExcludeFromInteractiveRouting]`) にする
- お客様が開く画面は検索に載せず (`X-Robots-Tag: noindex`)、キャッシュさせない (`Cache-Control: no-cache, no-store`。静的な画面に付く偽造防止の値と同じにして、上書きの警告を出させない)
- お客様が開く画面の文言は、ブラウザの言語 (`Accept-Language` の重みの順) で日本語と英語から選ぶ (`ReceiptTexts`)
- お客様が開く画面のメニューの名前は、`LocalizedText` のその言語 (ない言語は日本語) を出す
- 静的な画面で 404 を返すときは、本文を書き始めるとき (`Response.OnStarting`) にステータスコードを変える (画面の中で 404 にすると、Blazor は本文を書かずに StatusCodePages の再実行に任せるため)
- 見つからないトークンは、あるかないかのほかは見せない
