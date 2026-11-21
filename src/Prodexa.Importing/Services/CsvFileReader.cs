using Microsoft.VisualBasic.FileIO;
using Prodexa.Importing.Services;

namespace Prodexa.Importing.Services;

/// <summary>
/// Streams parsed CSV rows from a file with RFC-style quoted fields.
/// </summary>
public sealed class CsvFileReader : IDisposable
{
    private readonly TextFieldParser _parser;

    /// <summary>
    /// Columns declared by the first CSV row.
    /// </summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>
    /// Opens a CSV file and validates its header.
    /// </summary>
    public CsvFileReader(string path)
    {
        _parser = new TextFieldParser(path)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        _parser.SetDelimiters(",");

        try
        {
            var header = _parser.ReadFields();

            if (header is null || header.Length == 0
                || header.Any(string.IsNullOrWhiteSpace)
                || header.Distinct(StringComparer.Ordinal).Count() != header.Length)
            {
                throw new ImportValidationException("CSV header is empty, contains blank names, or has duplicate column names.");
            }

            Columns = header;
        }
        catch (MalformedLineException exception)
        {
            throw new ImportValidationException($"Malformed CSV header near line {exception.LineNumber}.");
        }
    }

    /// <summary>
    /// Reads the next source row, or null when the CSV has ended.
    /// </summary>
    public Dictionary<string, string>? ReadRow()
    {
        try
        {
            if (_parser.EndOfData)
            {
                return null;
            }

            var values = _parser.ReadFields();

            if (values is null || values.Length != Columns.Count)
            {
                throw new ImportValidationException($"CSV column count differs from the header near line {_parser.LineNumber}.");
            }

            var row = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var i = 0; i < Columns.Count; i++)
            {
                row.Add(Columns[i], values[i]);
            }

            return row;
        }
        catch (MalformedLineException exception)
        {
            throw new ImportValidationException($"Malformed CSV near line {exception.LineNumber}.");
        }
    }

    /// <summary>
    /// Releases the parser and its file handle.
    /// </summary>
    public void Dispose()
    {
        _parser.Dispose();
    }
}
