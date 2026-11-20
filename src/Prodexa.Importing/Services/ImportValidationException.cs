namespace Prodexa.Importing.Services;

/// <summary>
/// Indicates that an input record cannot be accepted as valid manufacturing data.
/// </summary>
public sealed class ImportValidationException(string message) : Exception(message)
{
}
