namespace TableOrder.Server.Core.Accessors;

using TableOrder.Server.Core.Infrastructure.Data;

// Accessor 共通の型変換 ([ExecuteConfig(typeof(DataProfile))] で参照する。新しい列挙型も登録する)
[AccessorProfile]
[TypeHandler(typeof(EnumTextConverter<TenantStatus>))]
[TypeHandler(typeof(EnumTextConverter<EnrollmentMethod>))]
[TypeHandler(typeof(EnumTextConverter<DeviceKind>))]
[TypeHandler(typeof(EnumTextConverter<TaxRounding>))]
[TypeHandler(typeof(EnumTextConverter<StockTargetKind>))]
[TypeHandler(typeof(EnumTextConverter<StockStatus>))]
[TypeHandler(typeof(EnumTextConverter<VisitStatus>))]
[TypeHandler(typeof(EnumTextConverter<VisitOpenedBy>))]
[TypeHandler(typeof(EnumTextConverter<VisitClosedBy>))]
[TypeHandler(typeof(EnumTextConverter<OrderSource>))]
[TypeHandler(typeof(EnumTextConverter<OrderTiming>))]
[TypeHandler(typeof(EnumTextConverter<OrderLineStatus>))]
[TypeHandler(typeof(EnumTextConverter<ServedBy>))]
[TypeHandler(typeof(EnumTextConverter<KitchenTicketStatus>))]
[TypeHandler(typeof(EnumTextConverter<CallStatus>))]
[TypeHandler(typeof(EnumTextConverter<PaymentMethod>))]
[TypeHandler(typeof(EnumTextConverter<PaymentStatus>))]
[TypeHandler(typeof(GuidTextConverter))]
[TypeHandler(typeof(DateTimeOffsetTextConverter))]
[TypeHandler(typeof(DateOnlyTextConverter))]
[TypeHandler(typeof(LocalizedTextConverter))]
public static class DataProfile;
