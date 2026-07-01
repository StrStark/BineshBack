using Binesh.Ai.QueryEngine;
using Binesh.Domain.Customers;

namespace Binesh.Ai.Schemas;

/// <summary>
/// Field metadata for <see cref="Customer"/>. The "PhoneNumber" field in the
/// old schema is renamed to "Mobile" (matches the Round 7 domain rename) and
/// "BirthDay" → "BirthDate" (DateTimeOffset on the new model).
/// </summary>
public static class CustomerSchema
{
    public static EntitySchema Build() => new()
    {
        Name = "Customer",
        EntityType = typeof(Customer),
        Fields =
        [
            new FieldDescriptor
            {
                Name = "Active",
                Type = FieldType.Bool,
                Selector = (Customer c) => c.Active,
                AllowedOperators = ["eq", "ne"],
                Orderable = false,
                Selectable = true,
                Groupable = true,
                Aggregatable = false,
            },
            new FieldDescriptor
            {
                Name = "PaymentReliability",
                Type = FieldType.Float,
                Selector = (Customer c) => c.PaymentReliability,
                AllowedOperators = ["eq", "ge", "le"],
                Orderable = true,
                Selectable = true,
                Aggregatable = true,
                AllowedAggregates = ["avg"],
            },
            new FieldDescriptor
            {
                Name = "CustomerType",
                Type = FieldType.Enum,
                Selector = (Customer c) => c.Type,
                AllowedOperators = ["eq", "ne"],
                Orderable = false,
                Selectable = true,
                Groupable = true,
                Aggregatable = false,
                AllowedValues = Enum.GetNames<CustomerType>(),
            },
            new FieldDescriptor
            {
                Name = "Name",
                Type = FieldType.String,
                Selector = (Customer c) => c.Person.Name,
                RequiredIncludes = ["Person"],
                AllowedOperators = ["eq", "ne"],
                Selectable = true,
            },
            new FieldDescriptor
            {
                Name = "Family",
                Type = FieldType.String,
                Selector = (Customer c) => c.Person.Family,
                RequiredIncludes = ["Person"],
                AllowedOperators = ["eq", "ne"],
                Selectable = true,
            },
            new FieldDescriptor
            {
                Name = "Code",
                Type = FieldType.String,
                Selector = (Customer c) => c.Person.Code,
                RequiredIncludes = ["Person"],
                AllowedOperators = ["eq", "ne"],
            },
            new FieldDescriptor
            {
                Name = "Phone",
                Type = FieldType.String,
                Selector = (Customer c) => c.Person.Phone,
                RequiredIncludes = ["Person"],
                AllowedOperators = ["eq", "ne"],
            },
            new FieldDescriptor
            {
                Name = "Mobile",   // renamed from old "PhoneNumber" — see Round 7 domain rename
                Type = FieldType.String,
                Selector = (Customer c) => c.Person.Mobile,
                RequiredIncludes = ["Person"],
                AllowedOperators = ["eq", "ne"],
            },
            new FieldDescriptor
            {
                Name = "Address",
                Type = FieldType.String,
                Selector = (Customer c) => c.Person.Address,
                RequiredIncludes = ["Person"],
                AllowedOperators = ["eq"],
            },
            new FieldDescriptor
            {
                Name = "BirthDate", // renamed from old "BirthDay" — Person.BirthDate is DateTimeOffset?
                Type = FieldType.DateTime,
                Selector = (Customer c) => c.Person.BirthDate,
                RequiredIncludes = ["Person"],
                AllowedOperators = ["ge", "le"],
            },
            new FieldDescriptor
            {
                Name = "City",
                Type = FieldType.String,
                Selector = (Customer c) => c.Person.Region!.City,
                RequiredIncludes = ["Person.Region"],
                AllowedOperators = ["eq", "ne"],
                Groupable = true,
            },
            new FieldDescriptor
            {
                Name = "Province",
                Type = FieldType.String,
                Selector = (Customer c) => c.Person.Region!.Province,
                RequiredIncludes = ["Person.Region"],
                AllowedOperators = ["eq", "ne"],
                Groupable = true,
            },
        ],
    };
}
