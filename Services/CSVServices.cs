using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using MyProjectBase.Helpers;
using MyProjectBase.Models;
using MyProjectBase.Utilities;

namespace MyProjectBase.Services;

public interface ICsvService
{
    Task<ServiceResult<List<Shoe>>> LoadDataAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult> SaveDataAsync(IEnumerable<Shoe> data, CancellationToken cancellationToken = default);
}

public sealed class CsvService : ICsvService
{
    private static readonly string[] RequiredHeaders = ["Id", "Brand", "Model", "ImagePath"];
    private readonly IAppLogger _logger;
    private sealed record CsvRow(int LineNumber, List<string> Values, string? Error);

    public CsvService(IAppLogger logger)
    {
        _logger = logger;
    }

    public async Task<ServiceResult<List<Shoe>>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
            return ServiceResult<List<Shoe>>.Fail("Main window unavailable for opening the CSV file.");

        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import CSV file",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("CSV") { Patterns = ["*.csv"] },
                    FilePickerFileTypes.All
                ]
            });

            if (files.Count == 0)
                return ServiceResult<List<Shoe>>.Ok([], "CSV import canceled.");

            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var csv = await reader.ReadToEndAsync(cancellationToken);
            return Parse(csv);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "CSV import failed.");
            return ServiceResult<List<Shoe>>.Fail("CSV import failed. Check the file and its encoding.");
        }
    }

    public async Task<ServiceResult> SaveDataAsync(IEnumerable<Shoe> data, CancellationToken cancellationToken = default)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
            return ServiceResult.Fail("Main window unavailable for exporting the CSV file.");

        try
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export CSV collection",
                SuggestedFileName = "sneaker-collection.csv",
                FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
            });

            if (file == null)
                return ServiceResult.Ok("CSV export canceled.");

            var csv = Serialize(data);
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            await writer.WriteAsync(csv.AsMemory(), cancellationToken);

            return ServiceResult.Ok("CSV export completed.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "CSV export failed.");
            return ServiceResult.Fail("CSV export failed.");
        }
    }

    private static ServiceResult<List<Shoe>> Parse(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return ServiceResult<List<Shoe>>.Fail("The CSV file is empty.");

        var separator = DetectSeparator(csv);
        var rows = ReadRows(csv, separator).ToList();
        if (rows.Count < 2)
            return ServiceResult<List<Shoe>>.Fail("The CSV file must contain a header and at least one sneaker.");

        var malformedHeader = rows[0].Error;
        if (malformedHeader != null)
            return ServiceResult<List<Shoe>>.Fail($"Corrupted CSV at line 1: {malformedHeader}");

        var malformedRows = rows.Where(row => row.Error != null).ToList();
        if (malformedRows.Count > 0)
        {
            var preview = string.Join(", ", malformedRows.Take(5).Select(row => row.LineNumber));
            return ServiceResult<List<Shoe>>.Fail($"Corrupted CSV: invalid quotes or separators at lines {preview}.");
        }

        var headers = rows[0].Values.Select(header => header.Trim()).ToList();
        if (headers.Any(string.IsNullOrWhiteSpace))
            return ServiceResult<List<Shoe>>.Fail("Corrupted CSV: header contains an empty column.");

        if (headers.Count != headers.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            return ServiceResult<List<Shoe>>.Fail("Corrupted CSV: duplicated columns in header.");

        var unexpectedHeaders = headers
            .Where(header => !RequiredHeaders.Any(required => required.Equals(header, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (unexpectedHeaders.Count > 0)
            return ServiceResult<List<Shoe>>.Fail($"Corrupted CSV: unknown columns: {string.Join(", ", unexpectedHeaders)}.");

        var missingHeaders = RequiredHeaders
            .Where(required => !headers.Any(header => header.Equals(required, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missingHeaders.Count > 0)
            return ServiceResult<List<Shoe>>.Fail($"Corrupted CSV: missing columns: {string.Join(", ", missingHeaders)}.");

        var requiredIndexes = RequiredHeaders
            .Select(required => headers.FindIndex(header => header.Equals(required, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var shoes = new List<Shoe>();
        var corruptedRows = new List<int>();
        var invalidRows = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i].Values;
            if (row.Count == 0 || row.All(string.IsNullOrWhiteSpace))
                continue;

            if (row.Count != headers.Count || requiredIndexes.Any(index => index >= row.Count))
            {
                corruptedRows.Add(rows[i].LineNumber);
                continue;
            }

            var shoe = new Shoe
            {
                Id = GetValue(headers, row, "Id"),
                Brand = GetValue(headers, row, "Brand").Trim(),
                Model = GetValue(headers, row, "Model").Trim(),
                ImagePath = GetValue(headers, row, "ImagePath").Trim()
            };

            if (string.IsNullOrWhiteSpace(shoe.Id))
                shoe.Id = Guid.NewGuid().ToString();

            var errors = ShoeValidator.Validate(shoe);
            if (errors.Count > 0)
            {
                invalidRows.Add($"line {rows[i].LineNumber} ({string.Join(" ", errors)})");
                continue;
            }

            if (!ids.Add(shoe.Id))
            {
                corruptedRows.Add(rows[i].LineNumber);
                continue;
            }

            shoe.Picture = ImageHelper.LoadShoePicture(shoe);
            shoes.Add(shoe);
        }

        if (corruptedRows.Count > 0)
        {
            var preview = string.Join(", ", corruptedRows.Take(5));
            return ServiceResult<List<Shoe>>.Fail($"Corrupted CSV: incorrect column count or duplicated ID at lines {preview}.");
        }

        if (invalidRows.Count > 0)
        {
            var preview = string.Join(", ", invalidRows.Take(5));
            return ServiceResult<List<Shoe>>.Fail($"Corrupted CSV: invalid data ({preview}).");
        }

        if (shoes.Count == 0)
            return ServiceResult<List<Shoe>>.Fail("Corrupted CSV: no valid row found.");

        return ServiceResult<List<Shoe>>.Ok(shoes, $"{shoes.Count} sneakers imported.");
    }

    private static string Serialize(IEnumerable<Shoe> data)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(';', RequiredHeaders));

        foreach (var shoe in data)
        {
            var values = new[]
            {
                shoe.Id,
                shoe.Brand,
                shoe.Model,
                shoe.ImagePath
            };

            builder.AppendLine(string.Join(';', values.Select(Escape)));
        }

        return builder.ToString();
    }

    private static IEnumerable<CsvRow> ReadRows(string csv, char separator)
    {
        using var reader = new StringReader(csv);
        string? line;
        var lineNumber = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            yield return ParseLine(line, separator, lineNumber);
        }
    }

    private static char DetectSeparator(string csv)
    {
        var firstLine = csv
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line)) ?? string.Empty;

        var semicolons = firstLine.Count(character => character == ';');
        var commas = firstLine.Count(character => character == ',');
        return semicolons >= commas ? ';' : ',';
    }

    private static CsvRow ParseLine(string line, char separator, int lineNumber)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;
        var quoteJustClosed = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];

            if (character == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                    quoteJustClosed = false;
                }
                else if (!inQuotes && value.Length > 0)
                {
                    return new CsvRow(lineNumber, values, "quote inside an unescaped field");
                }
                else
                {
                    inQuotes = !inQuotes;
                    quoteJustClosed = !inQuotes;
                }
            }
            else if (character == separator && !inQuotes)
            {
                values.Add(value.ToString());
                value.Clear();
                quoteJustClosed = false;
            }
            else if (quoteJustClosed && !char.IsWhiteSpace(character))
            {
                return new CsvRow(lineNumber, values, "text found after a closing quote");
            }
            else
            {
                value.Append(character);
                if (!char.IsWhiteSpace(character))
                    quoteJustClosed = false;
            }
        }

        if (inQuotes)
            return new CsvRow(lineNumber, values, "missing closing quote");

        values.Add(value.ToString());
        return new CsvRow(lineNumber, values, null);
    }

    private static string Escape(string value)
    {
        if (!value.Contains(';') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string GetValue(IReadOnlyList<string> headers, IReadOnlyList<string> row, string name)
    {
        var index = headers
            .Select((header, idx) => new { header, idx })
            .First(item => item.header.Equals(name, StringComparison.OrdinalIgnoreCase))
            .idx;

        return index < row.Count ? row[index] : string.Empty;
    }

    private static TopLevel? GetTopLevel()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? TopLevel.GetTopLevel(desktop.MainWindow)
            : null;
    }
}
