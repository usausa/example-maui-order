namespace TableOrder.Terminal.Components;

public sealed record BatteryStatus(
    double Level,
    BatteryState State,
    BatteryPowerSource PowerSource);

public sealed record NetworkStatus(
    NetworkAccess Access,
    IReadOnlyList<ConnectionProfile> Profiles);

public sealed partial class DeviceInformation : IDisposable
{
    private bool started;

    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        StartBattery();
        StartNetwork();
    }

    public void Stop()
    {
        if (!started)
        {
            return;
        }

        started = false;
        StopNetwork();
        StopBattery();
    }

    public string DeviceId { get; } = ResolveDeviceId();

    private static partial string ResolveDeviceId();

    public event EventHandler? BatteryChanged;

    public BatteryStatus? Battery { get; private set; }

    private void UpdateBattery(BatteryStatus status)
    {
        Battery = status;
        BatteryChanged?.Invoke(this, EventArgs.Empty);
    }

    private partial void StartBattery();

    private partial void StopBattery();

    public event EventHandler? NetworkChanged;

    public NetworkStatus? Network { get; private set; }

    private void UpdateNetwork(NetworkStatus status)
    {
        Network = status;
        NetworkChanged?.Invoke(this, EventArgs.Empty);
    }

    private partial void StartNetwork();

    private partial void StopNetwork();
}
