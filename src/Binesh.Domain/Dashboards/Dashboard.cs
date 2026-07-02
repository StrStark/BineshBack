namespace Binesh.Domain.Dashboards;

public sealed class Dashboard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Icon { get; set; } = "LayoutDashboard";
    public string ConfigJson { get; set; } = """{"widgets":[]}""";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
