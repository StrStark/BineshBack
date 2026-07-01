using Binesh.Ai.QueryEngine;
using Binesh.Application.Abstractions;

namespace Binesh.Ai.Tools;

/// <summary>
/// Common base for concrete query tools — wires the schema + engine and
/// requires each tool to expose the <see cref="IQueryable{T}"/> root for its
/// entity via <see cref="Source"/>.
/// </summary>
public abstract class QueryableToolBase<T>(
    EntitySchema schema,
    AiQueryEngine engine,
    IBineshDbContext db)
    : IQueryableTool
    where T : class
{
    private readonly AiQueryEngine _engine = engine;
    protected IBineshDbContext Db { get; } = db;

    public abstract string ToolName { get; }
    public abstract string Description { get; }
    public EntitySchema Schema { get; } = schema;

    /// <summary>The IQueryable root the engine pipelines off. Override per tool.</summary>
    protected abstract IQueryable<T> Source(IBineshDbContext db);

    public Task<object> ExecuteAsync(AiQueryRequest request, CancellationToken cancellationToken) =>
        _engine.ExecuteAsync(Schema, Source(Db), request, cancellationToken);
}
