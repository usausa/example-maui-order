---
paths:
  - "table/src/**"
  - "hall/src/**"
  - "reception/src/**"
  - "terminal/src/**"
---
# 端末の部品と状態

- Android の API は `Components/` の部品にまとめ (共通の部分は `Xxx.cs`、Android の部分は `Xxx.android.cs`)、画面と状態から直接呼ばない
- 管理対象の構成 (EMM が配る設定) は `Components/ManagedConfiguration` で読み、`Settings` が端末の設定より優先して返す。端末の値は書き換えない (配られなくなったら端末の値に戻る)
- 管理対象の構成のキーを足すときは、`Platforms/Android/Resources/xml/app_restrictions.xml`、名前と説明の文言 (`values` / `values-en` の `restrictions.xml`)、`ManagedConfiguration` を揃える
- 配られた値のうち、空の値と正しくない値は使わずに記録し、端末の設定を使う
- 管理対象の構成の変更の知らせ (`ACTION_APPLICATION_RESTRICTIONS_CHANGED`) は動いている間に登録した受け口にだけ届くので、画面が前に出たときにも読み直す
- 専用端末の方式 (Device Owner、EMM の許したロックタスク) は動いている間に替わることがあるので、画面が前に出たときに掛け直し、出すとき (スタッフメニュー) にも読み直す
- 端末の鍵は `Components/DeviceKey` (Android の Keystore の P-256) に作り、秘密鍵を取り出さない。値は Keystore の形 (公開鍵は SubjectPublicKeyInfo、署名は DER) のまま返す
- 鍵は登録し直しても使い回し、端末を無効にされたときだけ消す (登録に失敗しても前の登録を壊さない)
- 登録した端末の id は登録した接続先と組で `Settings` に持ち、今の接続先と違えば登録していないものとする (接続先ごとに登録する)
- EMM の登録トークンは、登録していない端末が起動したときだけ使う (登録している端末は登録し直さない)
- 画像 (料理の写真、チェーンのロゴ) は `Components/ImageCache` が登録ごとのフォルダに名前で保存し、保存したものは取り直さない (名前は内容が変わると変わる)。名前は保存の前に `ImageNames.IsValid` で確かめる
- 画像は起動のときにまとめて受け取り (同時に 4 つまで。時間を区切る)、受け取れなくても起動は止めない。受け取れなかった画像は待受に戻ったときに取り直し、今のメニューで使わない画像とほかの登録のフォルダは受け取るときに消す
- スタッフの PIN は店舗の設定のハッシュ (`StaffPinHash`) を登録と一緒に `Settings` に保存し、平文の PIN を持たない (EMM でも配らない)。登録を消すときにハッシュも消す
- PIN の確かめ (回数の多い計算) は画面のスレッドの外で行う
