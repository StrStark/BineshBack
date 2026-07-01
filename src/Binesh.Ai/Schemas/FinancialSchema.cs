using Binesh.Ai.QueryEngine;
using Binesh.Domain.Financial;

namespace Binesh.Ai.Schemas;

/// <summary>
/// Field metadata for <see cref="FinancialEntry"/>. The old schema used
/// Persian field names (<c>Bedehkar</c>, <c>Bestankar</c>) directly; the
/// new schema uses the translated names (<c>Debit</c>, <c>Credit</c>) per
/// the Round 11 domain rename. The prompt layer (12d) is where Persian
/// language understanding lives — the field names themselves stay English.
/// </summary>
public static class FinancialSchema
{
    public static EntitySchema Build() => new()
    {
        Name = "Financial",
        EntityType = typeof(FinancialEntry),
        Fields =
        [
            new FieldDescriptor
            {
                Name = "Code",
                Type = FieldType.String,
                Selector = (FinancialEntry f) => f.Code,
                AllowedOperators = ["eq", "ne"],
                Orderable = true,
                Selectable = true,
            },
            new FieldDescriptor
            {
                Name = "Name",
                Type = FieldType.String,
                Selector = (FinancialEntry f) => f.Name,
                AllowedOperators = ["eq", "ne"],
                Orderable = true,
                Selectable = true,
                Groupable = true,
            },
            new FieldDescriptor
            {
                Name = "Type",
                Type = FieldType.String,
                Selector = (FinancialEntry f) => f.Type,
                AllowedOperators = ["eq", "ne"],
                Selectable = true,
                Groupable = true,
            },
            new FieldDescriptor
            {
                Name = "Debit",      // was Bedehkar
                Type = FieldType.Int64,
                Selector = (FinancialEntry f) => f.Debit,
                AllowedOperators = ["eq", "ge", "le"],
                Orderable = true,
                Selectable = true,
                Aggregatable = true,
                AllowedAggregates = ["sum", "avg"],
            },
            new FieldDescriptor
            {
                Name = "Credit",     // was Bestankar
                Type = FieldType.Int64,
                Selector = (FinancialEntry f) => f.Credit,
                AllowedOperators = ["eq", "ge", "le"],
                Orderable = true,
                Selectable = true,
                Aggregatable = true,
                AllowedAggregates = ["sum", "avg"],
            },
        ],
    };
}
