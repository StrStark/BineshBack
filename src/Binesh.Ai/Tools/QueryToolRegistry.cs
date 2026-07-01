namespace Binesh.Ai.Tools;

/// <summary>
/// Central registry of <see cref="IQueryableTool"/> instances. Looked up by
/// case-insensitive <see cref="IQueryableTool.ToolName"/> when OpenAI emits a
/// function call.
///
/// <para><b>Insertion order is preserved.</b> The prompt builder emits one
/// entity table per registered tool in registration order, and stable order
/// is required for prompt-cache hits and deterministic snapshot tests. The
/// dictionary side gives O(1) name lookup; the list side gives ordered
/// enumeration.</para>
/// </summary>
public sealed class QueryToolRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IQueryableTool> _byName =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IQueryableTool> _ordered = [];

    public QueryToolRegistry Register(IQueryableTool tool)
    {
        lock (_gate)
        {
            if (_byName.ContainsKey(tool.ToolName))
            {
                throw new InvalidOperationException(
                    $"A tool with name '{tool.ToolName}' is already registered.");
            }
            _byName.Add(tool.ToolName, tool);
            _ordered.Add(tool);
        }
        return this;
    }

    public IReadOnlyList<IQueryableTool> All
    {
        get { lock (_gate) { return _ordered.ToList(); } }
    }

    public IQueryableTool? Get(string toolName)
    {
        lock (_gate)
        {
            return _byName.TryGetValue(toolName, out var tool) ? tool : null;
        }
    }

    public bool Contains(string toolName)
    {
        lock (_gate)
        {
            return _byName.ContainsKey(toolName);
        }
    }
}
