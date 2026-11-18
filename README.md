# example-maui-order

.NET MAUI で作る、ファミリーレストラン向けテーブルオーダーのサンプル。  
お客様はテーブルのタブレットで、メニューを見て注文し、会計まで行う。  
端末は注文サーバ (ASP.NET Core) の REST API と SignalR の通知で動く。  
チェーンと店舗の設定で、名前・ロゴ・色・言語・機能を切り替える。

| 端末 | 機器 | 利用者 | 主な機能 |
| --- | --- | --- | --- |
| テーブル端末 | Android タブレット (横向き) | お客様 | メニュー、注文、注文履歴、店員呼出、会計 |
| ホール端末 | Android スマートフォン | ホールのスタッフ | 席への案内、呼び出しと提供、品切れ、代理注文、会計の補助 |
| 受付機 | Android タブレット (縦向き) | お客様 | 人数の入力と席の案内 (来店の開き方を受付機にした店) |
| キッチン端末 | タブレットのブラウザ (サーバが配信する Web アプリ) | 厨房・デザート・ドリンクの持ち場 | 持ち場ごとのチケット、作り始めとできあがりの送信 |
| 管理画面 | ブラウザ | 店長・本部・サービスの運営者 | 店内状況、端末・テーブル・店舗・利用者の管理 |

## 📱 画面

### テーブル端末

| | |
| --- | --- |
| ![起動](docs/images/startup.png) | ![待受](docs/images/standby.png) |
| 起動 | 待受 |
| ![注文](docs/images/menu.png) | ![商品の詳細](docs/images/item-detail.png) |
| 注文。カテゴリから選んで注文リストに入れる | 商品の詳細。オプションと数量を選んで入れる |
| ![注文の確認](docs/images/order-confirm.png) | ![注文履歴](docs/images/order-history.png) |
| 注文の確認。ドリンクバーを人数分まで提案する | 注文履歴。調理と提供の状態と食後の品のお願い |
| ![店員呼出](docs/images/staff-call.png) | ![お会計](docs/images/checkout.png) |
| 店員呼出。用件を選んで呼ぶ | お会計。明細と割り勘、QR コード決済 |
| ![English](docs/images/menu-en.png) | ![別のチェーン](docs/images/menu-other-chain.png) |
| 英語の表示。日本語と切り替える | 別のチェーン (さつき軒)。再登録するとデザインとメニューが変わる |
| ![時間帯の終わり](docs/images/menu-daypart-end.png) | ![お子様のメニュー](docs/images/menu-kids.png) |
| 時間帯の終わり。ランチの品を隠し、カートの品に印を付ける | お子様のメニュー。子どもがいる来店だけに出す |

### ホール端末

| | | |
| --- | --- | --- |
| ![席](docs/images/hall-seats.png) | ![案内](docs/images/hall-guide.png) | ![来店の詳細](docs/images/hall-visit.png) |
| 席。テーブルの状態と呼び出し | 案内。空いている席に人数を入れて来店を開く | 来店の詳細。注文の状態と、人数・席・会計の操作 |
| ![代理注文](docs/images/hall-order.png) | ![品の詳細](docs/images/hall-order-item.png) | ![会計](docs/images/hall-bill.png) |
| 代理注文。お客様の代わりに品を選んで送る | 品の詳細。オプションと数量を選んでカートに入れる | 会計。明細を確認して会計を始める |
| ![呼び出し](docs/images/hall-calls.png) | ![提供](docs/images/hall-serving.png) | ![品切れ](docs/images/hall-stock.png) |
| 呼び出し。音と振動で通知し、向かう・対応したを送る | 提供。できあがった品をテーブルごとに届ける | 品切れ。品切れと残りの数、注文の一時停止 |

### 受付機

| | | | |
| --- | --- | --- | --- |
| ![待受](docs/images/reception-standby.png) | ![人数](docs/images/reception-guests.png) | ![案内](docs/images/reception-guide.png) | ![満席](docs/images/reception-full.png) |
| 待受。受付の流れと空席の状態 | 人数。大人と子どもを入れて席を決める | 案内。サーバが決めたテーブルを表示する | 満席。スタッフが案内することを表示する |

### キッチン端末

| | |
| --- | --- |
| ![チケット](docs/images/kitchen-tickets.png) | ![品切れ](docs/images/kitchen-stock.png) |
| チケット。持ち場ごとに作り始めとできあがりを送る | 品切れ。品切れと残りの数を変更する |

### 管理画面

| | |
| --- | --- |
| ![ダッシュボード](docs/images/admin-dashboard.png) | ![店内状況](docs/images/admin-floor.png) |
| ダッシュボード。担当店舗の一覧 | 店内状況。テーブルと来店、注文の状態 |
| ![呼び出し・品切れ・通知履歴](docs/images/admin-floor-calls.png) | ![端末](docs/images/admin-devices.png) |
| 店内状況の下側。呼び出し、品切れ、通知履歴 | 端末。登録トークンの発行とテーブルの割り当て |
| ![利用者](docs/images/admin-users.png) | |
| 利用者。テナント管理者と店舗担当者の管理 | |

## 📚 ドキュメント

| 文書 | 内容 |
| --- | --- |
| [docs/business.md](docs/business.md) | 業務の前提と流れ (端末と業務、来店、金額と税、扱わないもの) |
| [docs/architecture.md](docs/architecture.md) | プロジェクト構成と、各端末・サーバの実装 |
| [docs/api-design.md](docs/api-design.md) | API の設計 (共通仕様、リソース別 API、リアルタイム通知、端末ごとの API、エラーコード) |
| [docs/database.md](docs/database.md) | データベースの設計 (表の一覧と関係、書き込みのルール、保持期間) |
| [docs/device-management.md](docs/device-management.md) | 端末の配布と管理 (外部の EMM、自前の Device Owner、管理対象の構成) |
| [docs/plan.md](docs/plan.md) | 実装計画 (調査、デザイン、端末ごとの計画、外部の連携、本番の環境、フェーズ) |
| [docs/backlog.md](docs/backlog.md) | 未実装の機能と検討事項 |

## 📊 進捗

✅ 実装済み、🚧 一部実装 (枠や仮実装)、⬜ 未着手。  
詳細な項目は [docs/plan.md](docs/plan.md#-フェーズ) のフェーズ、未実装の機能は [docs/backlog.md](docs/backlog.md) にある。

| 対象 | 状態 | 実装済み | 未実装 |
| --- | --- | --- | --- |
| テーブル端末 | ✅ | 注文から会計まで (商品の詳細、メニューのルール、注文履歴、店員呼出、割り勘)、メニューの出し分け (時間帯、キッズ)、ラストオーダーと注文の一時停止、端末の登録、チェーンと店舗の設定 | |
| 専用端末と EMM | ✅ | 自前の Device Owner、EMM が許すロックタスク、管理対象の構成 (接続先、登録トークン) | Device Owner の QR コードでの登録 (許可の申請が必要) |
| ホール端末 | ✅ | 席と来店 (案内、人数の変更、席の移動、レジで払った、取りやめ)、呼び出しと提供 (画面が消えていても音と振動で通知)、品切れと注文の一時停止、代理注文、会計の補助 | |
| 受付機 | ✅ | 待受、人数、案内、満席、受付の停止 (受付機で来店を開かない店、ラストオーダーの後)、空席の通知 | |
| 来店の開き方 | ✅ | 店舗の設定でスタッフ・受付機・席から選ぶ。設定で許した端末だけが来店を開ける | |
| キッチン端末 | ✅ | `/kitchen` で配信する Web アプリ (ブラウザのエクスポートできない鍵で登録)、持ち場ごとのチケット (作り始め、できあがり、下げる、遅れの色)、品切れ、新しいチケットの音 | |
| 端末の共通部品 | ✅ | `TableOrder.Terminal` (端末の設定と登録、専用端末、通知の受信の基盤、共通のポップアップ、共通のスタイル、お客様の画面の部品)。テーブル端末・ホール端末・受付機で使う | |
| サーバの業務 | ✅ | 来店・注文・調理・提供・呼び出し・会計・支払の API と通知、端末の認証、マルチテナント、メニューの出し分けの検証、サンプルのチェーン (洋食と和食)、古いデータのクリーンアップ、電子レシート、開発用のシミュレーション | |
| 決済 | 🚧 | 開発用の仮の決済サービス | 実際の決済サービスと決済端末の接続、実際の QR コード |
| 管理画面 | ✅ | サインイン (ロール、多要素認証、仮パスワード)、利用者・テナント・店舗・テーブル・端末の管理、トークンの即時の失効、店内状況、チェーンと店舗の設定 | 外部の ID 基盤 (OIDC)、操作ログ、外部のクライアントと Webhook の画面 |
| 外部の連携 | ⬜ | 設計 ([docs/plan.md](docs/plan.md#-外部の連携)) | 外部のクライアント、メニューと写真の公開、端末の再読み込み、POS、Webhook |
| 本番の環境 | ⬜ | 構成の検討 ([docs/plan.md](docs/plan.md#-本番の環境)) | Amazon ECS・Amazon RDS・AWS CDK (C#) の IaC、画像の保存先 (Amazon S3)、複数台での運用 |
| 店舗のエッジ | ⬜ | | 回線が切れても店内で注文を続ける |

## 🔗 基にしたテンプレート

| テンプレート | 基にしたもの |
| --- | --- |
| [template-maui](https://github.com/usausa/template-maui) | 端末の共通の部品 |
| [template-maui-keyboard](https://github.com/usausa/template-maui-keyboard) | 端末のアプリの構成 (物理キーの入力とフォーカスの移動を除く) |
| [template-maui-server](https://github.com/usausa/template-maui-server) | サーバの構成と通信 |
