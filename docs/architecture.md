# 構成

リポジトリのプロジェクトの構成と、テーブル端末 (`TableOrder.Terminal.Table`) の作り。  
ここには作ったものだけを書き、これから作るもの (サーバ、ホール端末とキッチン端末、実際の通信) は [plan.md](plan.md) に置く。  
API の想定は [api-design.md](api-design.md) を参照。

- [1. プロジェクト](#-1-プロジェクト)
- [2. テーブル端末の作り](#-2-テーブル端末の作り)
- [3. 画面](#-3-画面)
- [4. 状態と通信](#-4-状態と通信)
- [5. 多言語と色](#-5-多言語と色)
- [6. モックの動き](#-6-モックの動き)

---

## 📦 1. プロジェクト

モノレポにし、プロジェクトは `src/`、テストは `tests/` に置く。  
端末のソリューションは `TableOrder.Terminal.slnx`。

| プロジェクト | 種類 | 内容 |
| --- | --- | --- |
| `TableOrder.Domain` | .NET | 業務の値 (列挙型) と計算 (金額と内税、割り勘の目安、タグのルールの数え方)。サーバと端末で同じ計算を使う |
| `TableOrder.Contract` | .NET | 通信データ (`XxxRequest` / `XxxResponse`、`LocalizedText`) |
| `TableOrder.Client` | .NET | 端末が使う API の窓口 (`IOrderApi`、`ApiResult`) とモック (`Mock/MockOrderApi`) |
| `TableOrder.Terminal.Table` | .NET MAUI (Android) | テーブル端末のアプリ |

```
TableOrder.Terminal.Table ──> TableOrder.Client ──> TableOrder.Contract ──> TableOrder.Domain
```

### 名前の付け方

| 区分 | 名前 |
| --- | --- |
| 共有 | `TableOrder.{Domain\|Contract\|Client}` |
| 端末 | `TableOrder.Terminal.{Table\|Hall\|Kitchen\|Shared}` (`Shared` は端末に共通の画面部品) |
| サーバ | `TableOrder.Server.{Core\|Web\|AppHost}` |
| テスト | `tests/` に `{プロジェクト名}.Tests` |

- 名前空間の `TableOrder.Terminal.Table` と紛れるので、`Table` という名前の型は作らない
- MAUI の `MenuItem` とぶつかるので、メニューの品の型は `MenuProduct` などにする

---

## 🧱 2. テーブル端末の作り

### フォルダ

| フォルダ | 内容 |
| --- | --- |
| `Modules/` | 画面とポップアップの View と ViewModel (`Startup`、`Setup`、`Standby`、`Menu`、`Checkout`、`Dialogs`) |
| `State/` | 画面をまたぐ状態 (`Settings`、`MenuState`、`VisitState`、`CartState`、`LanguageState`) |
| `Usecase/` | 通信と状態の更新を組み合わせる手順 (`OrderUsecase`。来店の開始と終了、メニューのルールの判定、注文の送信) |
| `Models/` | 画面で使う形 (`MenuCategory`、`MenuProduct`、`CartLine`、`ItemSelection`、`Language`) |
| `Controls/` | 見た目の部品 (`QrCodeView`。QR コードの代わりの模様) |
| `Markup/` | 記号 (`AppIcons`)、画面 ID の拡張 |
| `Resources/` | 色 (`Colors.xaml`)、スタイル (`Styles.xaml`)、画面の文言 (`Strings/AppResources.resx`、`.en.resx`)、料理の絵、アイコン、スプラッシュ |
| `Extender/` | 画面の切り替えとポップアップのプラグイン |
| `Shell/` | MainPage から画面への通知 (戻る) と処理中の覆い |
| `Behaviors/`、`Components/`、`Diagnostics/`、`Platforms/` | プラットフォームの調整、端末の情報、異常終了の記録、Activity とマニフェスト |

### 層

```
View (XAML) ──> ViewModel ──> Usecase ──> IOrderApi (Client)
                    │            │
                    └────────────┴──> State (Settings / MenuState / VisitState / CartState / LanguageState)
```

- ViewModel は State を読み、通信と状態の更新を組み合わせる手順は Usecase に任せる (読むだけの通信は ViewModel から `IOrderApi` を呼ぶ)
- 通信の結果は `ApiResult<T>` で受け、例外にしない。  
  失敗は `ViewHelper.ErrorMessage` でお客様向けの文言にし、`Log.WarnApiFailed` で記録する
- ポップアップとの受け渡しは引数と戻り値で行い、ポップアップは閉じると ViewModel ごと破棄する

---

## 📱 3. 画面

### 画面とポップアップ

| 画面 | ViewId | 内容 |
| --- | --- | --- |
| 起動 | `Startup` | システムのロゴ、準備の進み具合 (設定、店舗の設定、メニュー、品切れ、今の来店)、失敗のときの再試行と設定 |
| 端末の設定 | `Setup` | テーブル番号 (電卓)、接続先 (空ならモック) |
| 待受 | `Standby` | 店の名前、いらっしゃいませ、言語の切り替え、ご注文をはじめる (人数を入れて来店を開く) |
| 注文 | `Menu` | ヘッダ (店、テーブル、人数、言語)、カテゴリのタブ、メニューのカード、注文リスト、下部の操作 (注文履歴、店員呼出、お会計) |
| お会計 | `Checkout` | 明細、内税、割り勘の目安、まだ出していない品の注意、支払方法 (QR コード決済、カード、レジ)、QR の表示と待ち、お礼と電子レシートの QR |

| ポップアップ | DialogId | 内容 |
| --- | --- | --- |
| 電卓 | `InputNumber` | テーブル番号 (システムの色) |
| 知らせ / 確認 | `Message` / `Confirm` | 上限、失敗、年齢などの確認 (受ける / 断るは同じ大きさ) |
| 人数 | `GuestCount` | 大人と子ども (増減のボタン) |
| 商品の詳細 | `ItemDetail` | 大きな絵、説明、アレルギー、カロリー、オプション (必須 / お好みで)、出す時機、数量、入れる (合計つき)。注文リストの行から開くと内容を直せる |
| 注文の確認 | `OrderConfirm` | 明細と合計、提案 (1 枠)、注文する、受け付けの知らせ (数秒で閉じる)、失敗と送り直し |
| 注文履歴 | `OrderHistory` | 注文と明細の状態、食後の品をお願いする、これまでの合計 |
| 店員呼出 | `StaffCall` | 用件のタイル、呼び出しの状態 (呼んでいます / 向かっています) |

### 遷移

```
起動 ──(設定なし)──> 端末の設定 ──保存──> 起動
起動 ──(来店なし)──> 待受 ──タッチ──> [人数] ──> 注文
起動 ──(来店あり)──> 注文
注文 ──お会計──> お会計 ──支払の完了──> (お礼) ──閉じる / 30 秒──> 待受
                    └──メニューに戻る──> 注文
注文 ──カード──> [商品の詳細] ──(確認のルール)──> [確認]
     ──注文を確定する──> [注文の確認]
     ──注文履歴──> [注文履歴]
     ──店員呼出──> [店員呼出]
```

- MainPage は画面を切り替える入れ物と処理中の覆いだけを持ち、帯 (ヘッダ、タブ、下部の操作) は各 View が持つ
- 端末の戻るは表示中の画面に渡す。  
  お客様の画面 (待受、注文) では何もせず、お会計では支払方法の選び直しかメニューへ戻る
- 画面の配置を決めるまでの仮で全画面 (システムバーを隠す) にしている。  
  ポップアップは Activity と別の窓に出るので、`FullscreenPopupPlugin` でポップアップの窓のシステムバーも隠す

---

## 🔗 4. 状態と通信

### 状態

| 状態 | 持つもの |
| --- | --- |
| `Settings` | テーブル番号、接続先 (`IPreferences`) |
| `MenuState` | 店舗の設定、メニュー、品切れ。画面には選んでいる言語に直して渡す |
| `VisitState` | 今の来店 (人数、状態、答えた確認のルール、注文した品) |
| `CartState` | 注文する前の行。送ったが結果のわからない注文の Id (送り直しで同じ Id を使う) |
| `LanguageState` | 画面の言語 |

### メニューのルール

ドリンクバー・お酒・数量限定などの特例はコードで分けず、メニューのタグとルール (`MenuResponseRule`) で表す。  
数え方は `TableOrder.Domain.TagRules` に置き、端末は案内に、サーバは注文の確認に使う。

| ルール | 端末の動き | モックの例 |
| --- | --- | --- |
| 確認 (`Confirmation`) | タグの品を入れる前に確かめ、答えを来店に記録する (`Visit` は来店で 1 回) | お酒の年齢の確認 |
| 上限 (`Limit`) | 来店の注文とカートを合わせて上限を超えたら入れない (`Guest` は 1 人あたり × 人数) | サーロインステーキはお一人様 1 点まで |
| 提案 (`Suggestion`) | 注文の確認で、タグの品を誰かが頼んでいて人数より少なければ 1 枠だけ提案する | ドリンクバー (単品とセットのオプション) を人数分に |

### 通信の窓口

- `IOrderApi` は API の想定 ([api-design.md](api-design.md)) の要求を 1 つずつメソッドにしたもの。  
  REST と gRPC のどちらで実装しても、端末は `IOrderApi` だけを見る
- 今は DI で `MockOrderApi` を登録している

---

## 🌐 5. 多言語と色

### 多言語

- 画面の文言は `Resources/Strings/AppResources.resx` (日本語、既定) と `AppResources.en.resx` (英語) に置き、XAML は `{x:Static strings:AppResources.Xxx}` で引く
- 言語を切り替えると `LanguageState` が UI のカルチャを替え、表示中の画面を作り直して文言を引き直す (注文リストは状態に残る)
- メニューの名前と説明、呼び出しの用件はサーバの `LocalizedText` から選んだ言語で出す
- 起動のときと来店が終わったときは、端末の言語の設定によらず日本語に戻す
- 金額と時刻の書式は言語で変えない

### 色

- 色は `Colors.xaml` の役割の名前で定義し、スタイル・画面・記号は役割の名前だけを使う
- Brand (主色、補助色) を替えるとチェーンの色になる。  
  Neutral と Status は店の雰囲気に合わせるときだけ替える
- System は起動・端末の設定・電卓の色で、チェーンの色に替えない。  
  スプラッシュとアプリのアイコンの色に揃えている

---

## 🧪 6. モックの動き

`MockOrderApi` は来店・注文・呼び出し・支払をメモリに持ち、時間の経過でキッチン・ホール・決済サービスの動きを真似る。  
アプリを起動し直すと消える。

| 操作 | モックの動き |
| --- | --- |
| 店舗の設定 | お客様が人数を入れて来店を開ける (`selfStart`)。支払方法は QR コード決済とカード、呼び出しの用件は店員・お水・取り皿など |
| 品切れ | パンケーキは売り切れ、サーロインステーキは残り 3 点 (注文で減り、なくなると売り切れ) |
| 注文 | 5 秒で調理中、15 秒でまもなくお持ちします、25 秒で提供済み。お客様がとる品 (ドリンクバー) は受けたときに提供済み、食後の品はお願いされるまで止める |
| 注文の確認 | 売り切れ・残りの数・確認のルール・上限のルールを確かめ、違えば受け付けない |
| 呼び出し | 5 秒で「向かっています」。同じ用件の呼び出しは増やさない |
| 支払 | 6 秒で完了。払い終えると来店を終える |
| 通信の遅れ | 0.4 秒 |
