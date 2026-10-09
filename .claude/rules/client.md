---
paths:
  - "shared/src/TableOrder.Client/**"
---
# API の窓口 (TableOrder.Client)

- 窓口は端末の種類ごと (`ITableApi` など) と、すべての端末に共通の `IDeviceApi` (登録、トークン、状態の報告、端末の設定) に分ける。通知は `IOrderEvents`
- 結果は `ApiResult<T>` で返して例外を投げない。内容のない応答は `NoContent` にする
- 画像は `IDeviceApi.GetImageAsync` で中身をそのまま受け取り、保存と使い回しは端末に任せる
- 端末の側の値 (接続先、登録した端末の id、端末の鍵) は `IDeviceContext` で受け、実装が端末の設定を直接読まない
- アクセストークンは窓口の中で持って画面に渡さない。トークンの要求が断られたら (無効化、テナントの停止) `Denied` で端末の id と理由を知らせる
- 端末の鍵の値は `DeviceCredentials` で API の形 (JWK、ES256 の JWT) に直し、.NET の ASN.1 の読み書きで変換する (端末の暗号の実装に頼らない)
- 端末の鍵 (`IDeviceKey`) の公開鍵と署名は非同期にし、時間のかかる鍵の操作は鍵の実装が画面のスレッドの外で行う (ブラウザの鍵は非同期でしか使えない)。署名は DER で返し、ほかの形を返す鍵 (WebCrypto の r と s を並べた形) は `DeviceCredentials.ToDerSignature` で直す
- 注文サーバの実装は通信の方式を前に付け、REST の窓口は端末の種類ごと (`RestDeviceApi`、`Rest{種類}Api`) に分ける。要求の送り方 (接続先、トークンの取り直し、時間切れ、結果の分類) は `RestConnection` にまとめる
- 複数の端末の種類の窓口が同じ手順で読むもの (メニューの `304` での使い回し) は、共通の部品 (`RestMenuCache`) にして窓口ごとに持ち、窓口ごとに書かない
- 失敗は 4xx を `Rejected` (`errorCode` と文言を読む)、401 を `Unauthorized`、5xx・408・429・時間切れ・通信できないを `Unavailable`、呼び手の取り消しを `Canceled` にする
- 接続先と端末の id は要求のたびに `IDeviceContext` から読み、端末の設定で替えても窓口を作り直さない
- JSON はソース生成の `ClientJsonContext` で読み書きする (端末のトリミングでリフレクションの型の情報が消えるため)。通信データと通知の中身の型を足したら `[JsonSerializable]` にも足す
- 通知の受け口は、端末が今の状態を読む前につなぎ (`ConnectAsync`)、`ready` の番号から数える。つなぎ直したら抜けた通知を読み、その間に届いた通知と合わせて seq の順に渡す
- つなぎ直しは切れたとき (`Reconnecting`、閉じて始め直すとき) から通知をため、`ready` より前に届いた通知で番号を進めない (ハブはグループに入れてから番号を読むので、先に届いた通知で抜けた通知を読み飛ばす)。つなぎ直しが重なったら、最後に始めた抜けた通知の読み込みだけを使う
- 通知には接続 (`OrderEventArgs.Connection`) を付け、`ConnectAsync` で作り直したら前の接続の通知と読み込みは渡さない。数え方は通信に依らない部品 (`EventSequencer`) に分けてテストする
- 端末の鍵 (`IDeviceKey`) の失敗 (鍵がない、Keystore や WebCrypto の失敗) は `CryptographicException` だけを投げ、窓口は通信できない (`Unavailable`) として返す (つなぎ直しと状態の報告を例外で止めない)
- 扱わない種類の通知と読めない中身は渡さずに seq だけを進める (抜けと取り違えない)
- 通知の形 (`OrderEvent`) には端末が使う中身だけを持たせる。端末が一覧を読み直す通知 (呼び出し、チケット) は中身を持たない (使わない中身は、共有の型を調べるソリューションの InspectCode が使われていないと指摘する)
- ハブにつなぐ前の問い合わせ (negotiate) は時間を区切り、つなぎ直しが 401 で断られたらトークンを取り直す (サーバに届かないときと署名の鍵が替わったときに、つなぎ直しを止めない)
