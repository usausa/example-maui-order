---
paths:
  - "terminal/src/TableOrder.Terminal.*/Components/**"
  - "terminal/src/TableOrder.Terminal.*/State/**"
  - "terminal/src/TableOrder.Terminal.*/Platforms/**"
---
# 端末の部品と状態

- Android の API は `Components/` の部品にまとめ (共通の部分は `Xxx.cs`、Android の部分は `Xxx.android.cs`)、画面と状態から直接呼ばない
- 管理対象の構成 (EMM が配る設定) は `Components/ManagedConfiguration` で読み、`Settings` が端末の設定より優先して返す。端末の値は書き換えない (配られなくなったら端末の値に戻る)
- 管理対象の構成のキーを足すときは、`Platforms/Android/Resources/xml/app_restrictions.xml`、名前と説明の文言 (`values` / `values-en` の `restrictions.xml`)、`ManagedConfiguration` を揃える
- 配られた値のうち、空の値と正しくない値は使わずに記録し、端末の設定を使う
- 管理対象の構成の変更の知らせ (`ACTION_APPLICATION_RESTRICTIONS_CHANGED`) は動いている間に登録した受け口にだけ届くので、画面が前に出たときにも読み直す
- 専用端末の方式 (Device Owner、EMM の許したロックタスク) は動いている間に替わることがあるので、画面が前に出たときに掛け直し、出すとき (スタッフメニュー) にも読み直す
