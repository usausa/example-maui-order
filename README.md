# example-maui-order

.NET MAUI (Android のタブレット) で作る、ファミリーレストラン向けのテーブルオーダー端末のサンプル。  
テーブルに置いたタブレットで、お客様がメニューを見て注文し、注文履歴の確認、店員の呼び出し、テーブルでの会計を行う。  
端末は注文サーバにつないで動き、サーバは来店・注文・会計の API と通知を持つ。  
1 つのアプリのまま、チェーンと店舗の設定でチェーンの名前・ロゴ・色・言語・機能が替わる。

## 📱 画面

| | |
| --- | --- |
| ![起動](docs/images/startup.png) | ![待受](docs/images/standby.png) |
| 起動 | 待受 |
| ![注文](docs/images/menu.png) | ![商品の詳細](docs/images/item-detail.png) |
| 注文。カテゴリのタブ、料理のカード、注文リスト、下部の操作 | 商品の詳細。オプション、数量、合計を選んで入れる |
| ![注文の確認](docs/images/order-confirm.png) | ![注文履歴](docs/images/order-history.png) |
| 注文の確認。ドリンクバーの人数分の提案を 1 枠だけ出す | 注文履歴。調理と提供の状態、食後の品のお願い |
| ![店員呼出](docs/images/staff-call.png) | ![お会計](docs/images/checkout.png) |
| 店員呼出。用件を選ぶと「向かっています」まで知らせる | お会計。明細、内税、割り勘 (1 人分ずつ払える)、QR コード決済 |
| ![English](docs/images/menu-en.png) | ![別のチェーン](docs/images/menu-other-chain.png) |
| 英語の表示。ヘッダと待受で日本語と切り替える | 別のチェーン。登録し直すだけで名前・ロゴ・色・言語・機能が替わる |

## 📚 ドキュメント

| 文書 | 内容 |
| --- | --- |
| [docs/plan.md](docs/plan.md) | 実装計画。UI の調査、接続先と障害対策の調査、デザイン (色の役割)、画面と遷移、これから足すプロジェクトと通信の想定、専用端末化、本番の環境、フェーズ |
| [docs/architecture.md](docs/architecture.md) | 構成。プロジェクト、テーブル端末の作り (画面、状態と通信、メニューのルール、多言語と色)、サーバの作り (テナントの文脈、端末の認証、作った API、サンプルのデータ) |
| [docs/device-management.md](docs/device-management.md) | 端末の配布と管理。外部の EMM と自前の Device Owner の違い、EMM で配る手順、管理対象の構成 |
| [docs/business.md](docs/business.md) | 業務の前提と流れ。前提、端末と業務、管理画面、来店と人数・来店の状態、端末を置くまでと着席から会計までの流れ、金額と税、扱わないもの |
| [docs/api-design.md](docs/api-design.md) | 注文 API の想定。共通仕様 (端末の認証、テナント)、リソースごとの API (端末、店舗、メニュー、品切れ、来店、注文、調理、提供、呼び出し、会計)、リアルタイム通知、端末ごとの API (テーブル、ホール、キッチン、受付、外部)、エラーコード |
| [docs/database.md](docs/database.md) | データベースの設計。表の一覧と関係、店舗・端末・メニュー・来店・注文・調理・呼び出し・会計・通知の表、書き込みの決まり、残す期間 |
