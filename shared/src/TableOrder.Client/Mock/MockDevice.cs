namespace TableOrder.Client.Mock;

// モックの端末 (登録のコードで種類と置き場所が決まる)
internal sealed record MockDevice(Guid Id, DeviceKind Kind, string Name, int? TableNo);
