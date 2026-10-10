namespace TableOrder.Server.Web.Application.Telemetry;

using OpenTelemetry.Metrics;

public static class MeterProviderBuilderExtensions
{
    public static MeterProviderBuilder AddApplicationInstrumentation(this MeterProviderBuilder builder)
    {
        builder.AddMeter(Source.Name);
        return builder;
    }

    public static IServiceCollection AddApplicationInstrument(this IServiceCollection services)
    {
        services.AddSingleton<ApplicationInstrument>();
        return services;
    }
}
