using System.Collections.Concurrent;

namespace Binesh.Ai.QueryEngine;

/// <summary>
/// In-memory registry of every <see cref="EntitySchema"/> the AI knows about.
/// Looked up by case-insensitive name. Populated once at startup by
/// <c>Binesh.Ai.DependencyInjection.AddBineshAi</c>.
/// </summary>
public sealed class SchemaRegistry
{
    private readonly ConcurrentDictionary<string, EntitySchema> _schemas
        = new(StringComparer.OrdinalIgnoreCase);

    public void Register(EntitySchema schema)
    {
        if (!_schemas.TryAdd(schema.Name, schema))
        {
            throw new InvalidOperationException(
                $"An EntitySchema named '{schema.Name}' is already registered.");
        }
    }

    public EntitySchema Get(string name) =>
        _schemas.TryGetValue(name, out var schema)
            ? schema
            : throw new InvalidOperationException($"No EntitySchema named '{name}' is registered.");

    public bool TryGet(string name, out EntitySchema schema)
    {
        var ok = _schemas.TryGetValue(name, out var found);
        schema = found!;
        return ok;
    }

    public IReadOnlyCollection<EntitySchema> All => _schemas.Values.ToList();
}
