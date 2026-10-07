namespace TableOrder.Server.Core.Models.Entity;

// テナント (契約した会社)。この表だけは TenantId を持たない
public sealed class TenantEntity
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public TenantStatus Status { get; set; }

    public DateTimeOffset? SuspendedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int Version { get; set; }
}
