namespace TableOrder.Terminal;

// アプリの端末の種類 (登録で受けるペアリングコードと登録トークンの種類。違う種類のコードでは登録しない)
public sealed record TerminalOptions(DeviceKind Kind);
