using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Application.Abstractions;
using Binesh.Domain.Customers;

namespace Binesh.Ai.Tools;

public sealed class CustomerQueryTool(SchemaRegistry registry, AiQueryEngine engine, IBineshDbContext db)
    : QueryableToolBase<Customer>(registry.Get("Customer"), engine, db)
{
    public override string ToolName => "query_customer";
    public override string Description =>
        "Fetches customer (counterparty) data. Use for questions about buyers, contact " +
        "information, cities, payment reliability, or who bought something. " +
        "For sales BY customers use query_sale instead.";

    protected override IQueryable<Customer> Source(IBineshDbContext db) => db.Customers;
}
