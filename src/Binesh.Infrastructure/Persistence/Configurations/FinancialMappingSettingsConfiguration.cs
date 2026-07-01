using System.Text.Json;
using Binesh.Domain.Financial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class FinancialMappingSettingsConfiguration : IEntityTypeConfiguration<FinancialMappingSettings>
{
    public void Configure(EntityTypeBuilder<FinancialMappingSettings> builder)
    {
        builder.ToTable("financial_mapping_settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");

        ConfigureJson(builder, s => s.OperationalCost);
        ConfigureJson(builder, s => s.Payables);
        ConfigureJson(builder, s => s.ToCalculateSales);
        ConfigureJson(builder, s => s.ToCalculateLiquidity);
        ConfigureJson(builder, s => s.ToCalculateGrossProfitLoss);
        ConfigureJson(builder, s => s.ToCalculateOperatingProfitLoss);
        ConfigureJson(builder, s => s.ToCalculateProfitLossBeforTax);
        ConfigureJson(builder, s => s.ToCalculateNetProfitLoss);
        ConfigureJson(builder, s => s.ToCalculateAccumulatedProfitLoss);
        ConfigureJson(builder, s => s.ToCalculateEquity);
    }

    private static void ConfigureJson(
        EntityTypeBuilder<FinancialMappingSettings> builder,
        System.Linq.Expressions.Expression<Func<FinancialMappingSettings, IReadOnlyList<DetailedItem>>> property)
    {
        var converter = new ValueConverter<IReadOnlyList<DetailedItem>, string>(
            v => JsonSerializer.Serialize(v, JsonOpts),
            v => JsonSerializer.Deserialize<List<DetailedItem>>(v, JsonOpts) ?? new());

        var comparer = new ValueComparer<IReadOnlyList<DetailedItem>>(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v.Aggregate(0, (h, i) => HashCode.Combine(h, i.GetHashCode())),
            v => v.ToList());

        builder.Property(property)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(converter)
            .Metadata.SetValueComparer(comparer);
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
}
