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
[TypeHandler(typeof(GuidTextConverter))]
[TypeHandler(typeof(DateTimeOffsetTextConverter))]
[TypeHandler(typeof(DateOnlyTextConverter))]
[TypeHandler(typeof(LocalizedTextConverter))]
public static class DataProfile;
