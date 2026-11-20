namespace Prodexa.Importing.Services;

/// <summary>
/// Indicates a source revision or canonical identifier conflicts with stored data.
/// </summary>
public sealed class ImportConflictException(string message) : Exception(message)
{
}
