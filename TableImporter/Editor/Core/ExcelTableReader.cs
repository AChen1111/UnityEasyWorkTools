using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public sealed class ExcelTable
{
    private readonly Dictionary<string, int> headerLookup = new Dictionary<string, int>();
    private readonly List<ExcelRow> rows = new List<ExcelRow>();

    internal ExcelTable(IReadOnlyList<string> headers, IReadOnlyList<RowData> dataRows)
    {
        Headers = headers.ToArray();

        for (var i = 0; i < Headers.Count; i++)
        {
            var normalized = ExcelTableReader.NormalizeHeader(Headers[i]);
            if (!string.IsNullOrEmpty(normalized) && !headerLookup.ContainsKey(normalized))
            {
                headerLookup.Add(normalized, i);
            }
        }

        foreach (var row in dataRows)
        {
            rows.Add(new ExcelRow(this, row.Cells, row.RowNumber));
        }
    }

    public IReadOnlyList<string> Headers { get; }

    public IReadOnlyList<ExcelRow> Rows => rows;

    public bool TryGetColumnIndex(string columnName, out int index)
    {
        return headerLookup.TryGetValue(ExcelTableReader.NormalizeHeader(columnName), out index);
    }

    internal readonly struct RowData
    {
        public RowData(IReadOnlyList<string> cells, int rowNumber)
        {
            Cells = cells;
            RowNumber = rowNumber;
        }

        public IReadOnlyList<string> Cells { get; }

        public int RowNumber { get; }
    }
}

public sealed class ExcelRow
{
    private readonly ExcelTable table;
    private readonly IReadOnlyList<string> cells;

    internal ExcelRow(ExcelTable table, IReadOnlyList<string> cells, int rowNumber)
    {
        this.table = table;
        this.cells = cells;
        RowNumber = rowNumber;
    }

    public int RowNumber { get; }

    public IReadOnlyList<string> Cells => cells;

    public bool IsEmpty => cells.Count == 0 || cells.All(string.IsNullOrWhiteSpace);

    public bool HasColumn(string columnName)
    {
        return table.TryGetColumnIndex(columnName, out _);
    }

    public string Get(string columnName)
    {
        return TryGet(columnName, out var value) ? value : string.Empty;
    }

    public bool TryGet(string columnName, out string value)
    {
        value = string.Empty;
        if (!table.TryGetColumnIndex(columnName, out var index))
        {
            return false;
        }

        value = GetCell(index);
        return true;
    }

    public string GetCell(int index)
    {
        return index >= 0 && index < cells.Count ? cells[index] : string.Empty;
    }
}

public static class ExcelTableReader
{
    public static ExcelTable Read(string tablePath)
    {
        if (string.IsNullOrWhiteSpace(tablePath) || !File.Exists(tablePath))
        {
            throw new FileNotFoundException("Excel2SO table file not found.", tablePath);
        }

        var rows = ReadRawRows(tablePath);
        var headerRowIndex = rows.FindIndex(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)));
        if (headerRowIndex < 0)
        {
            return new ExcelTable(Array.Empty<string>(), Array.Empty<ExcelTable.RowData>());
        }

        var headers = rows[headerRowIndex];
        var dataRows = new List<ExcelTable.RowData>();
        for (var i = headerRowIndex + 1; i < rows.Count; i++)
        {
            dataRows.Add(new ExcelTable.RowData(rows[i], i + 1));
        }

        return new ExcelTable(headers, dataRows);
    }

    public static string NormalizeHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header)) return string.Empty;

        return new string(header.Trim().ToLowerInvariant()
            .Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-')
            .ToArray());
    }

    private static List<List<string>> ReadRawRows(string tablePath)
    {
        var extension = Path.GetExtension(tablePath).ToLowerInvariant();
        if (extension == ".csv")
        {
            return ReadCsv(tablePath);
        }

        if (extension == ".xlsx")
        {
            return ReadXlsxFirstSheet(tablePath);
        }

        throw new NotSupportedException($"Unsupported Excel2SO table extension: {extension}");
    }

    private static List<List<string>> ReadCsv(string csvPath)
    {
        var rows = new List<List<string>>();
        using var stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, DetectEncoding(stream), detectEncodingFromByteOrderMarks: true);

        while (!reader.EndOfStream)
        {
            rows.Add(ParseCsvLine(reader.ReadLine() ?? string.Empty));
        }

        return rows;
    }

    private static List<List<string>> ReadXlsxFirstSheet(string xlsxPath)
    {
        using var stream = new FileStream(xlsxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var sharedStrings = ReadSharedStrings(archive);
        var sheetPath = GetFirstWorksheetPath(archive);
        var sheetEntry = archive.GetEntry(sheetPath);
        if (sheetEntry == null)
        {
            throw new FileNotFoundException($"Worksheet not found in xlsx: {sheetPath}");
        }

        using var sheetStream = sheetEntry.Open();
        var document = XDocument.Load(sheetStream);
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<List<string>>();

        foreach (var row in document.Descendants(main + "row"))
        {
            var values = new List<string>();
            foreach (var cell in row.Elements(main + "c"))
            {
                var columnIndex = GetColumnIndex((string)cell.Attribute("r"));
                while (values.Count < columnIndex)
                {
                    values.Add(string.Empty);
                }

                values.Add(ReadCellValue(cell, sharedStrings, main));
            }

            rows.Add(values);
        }

        return rows;
    }

    private static string GetFirstWorksheetPath(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry == null || relsEntry == null)
        {
            throw new InvalidDataException("Invalid xlsx file: workbook metadata is missing.");
        }

        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        XNamespace officeRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        using var workbookStream = workbookEntry.Open();
        var workbook = XDocument.Load(workbookStream);
        var firstSheet = workbook.Descendants(main + "sheet").FirstOrDefault();
        var relationshipId = (string)firstSheet?.Attribute(officeRelNs + "id");
        if (string.IsNullOrEmpty(relationshipId))
        {
            throw new InvalidDataException("Invalid xlsx file: no worksheet found.");
        }

        using var relsStream = relsEntry.Open();
        var rels = XDocument.Load(relsStream);
        var target = rels.Descendants(relNs + "Relationship")
            .Where(node => (string)node.Attribute("Id") == relationshipId)
            .Select(node => (string)node.Attribute("Target"))
            .FirstOrDefault();

        if (string.IsNullOrEmpty(target))
        {
            throw new InvalidDataException($"Invalid xlsx file: missing relationship {relationshipId}.");
        }

        target = target.Replace('\\', '/').TrimStart('/');
        return target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? target : "xl/" + target;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        var strings = new List<string>();
        if (entry == null) return strings;

        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using var stream = entry.Open();
        var document = XDocument.Load(stream);

        foreach (var item in document.Descendants(main + "si"))
        {
            strings.Add(string.Concat(item.Descendants(main + "t").Select(node => node.Value)));
        }

        return strings;
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings, XNamespace main)
    {
        var type = (string)cell.Attribute("t");
        if (type == "inlineStr")
        {
            return string.Concat(cell.Descendants(main + "t").Select(node => node.Value)).Trim();
        }

        var rawValue = cell.Element(main + "v")?.Value ?? string.Empty;
        if (type == "s" && int.TryParse(rawValue, out var sharedStringIndex))
        {
            return sharedStringIndex >= 0 && sharedStringIndex < sharedStrings.Count
                ? sharedStrings[sharedStringIndex].Trim()
                : string.Empty;
        }

        return rawValue.Trim();
    }

    private static int GetColumnIndex(string cellReference)
    {
        if (string.IsNullOrEmpty(cellReference)) return 0;

        var index = 0;
        foreach (var c in cellReference)
        {
            if (!char.IsLetter(c)) break;
            index = index * 26 + char.ToUpperInvariant(c) - 'A' + 1;
        }

        return Math.Max(0, index - 1);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(value.ToString().Trim());
                value.Clear();
                continue;
            }

            value.Append(c);
        }

        result.Add(value.ToString().Trim());
        return result;
    }

    private static Encoding DetectEncoding(FileStream stream)
    {
        var buffer = new byte[Math.Min(4096, (int)stream.Length)];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);
        stream.Position = 0;

        if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF) return Encoding.UTF8;
        if (bytesRead >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE) return Encoding.Unicode;
        if (bytesRead >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF) return Encoding.BigEndianUnicode;

        var strictUtf8 = new UTF8Encoding(false, true);
        try
        {
            strictUtf8.GetString(buffer, 0, bytesRead);
            return Encoding.UTF8;
        }
        catch (DecoderFallbackException)
        {
            try
            {
                return Encoding.GetEncoding("GB18030");
            }
            catch
            {
                return Encoding.Default;
            }
        }
    }
}
