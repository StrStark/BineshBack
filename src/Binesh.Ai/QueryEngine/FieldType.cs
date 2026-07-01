namespace Binesh.Ai.QueryEngine;

/// <summary>
/// Categorizes a <see cref="FieldDescriptor"/>'s runtime type so the prompt
/// builder and runtime validator agree on what operators / aggregates make
/// sense for each field. Names are deliberately framework-agnostic — clients
/// receive these as strings via the JSON schema.
/// </summary>
public enum FieldType
{
    String,
    Int32,
    Int64,
    Float,
    Double,
    Decimal,
    DateTime,
    Bool,
    Enum,
}
