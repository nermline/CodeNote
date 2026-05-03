using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using File = NoteDomain.Model.File;

namespace NoteInfrastructure.Services;

public class FolderImportService : IImportService<Folder>
{
    private readonly NotedbContext _context;
    private readonly string        _userId;
    private const    string        DateFormat = "dd.MM.yyyy HH:mm";

    public FolderImportService(NotedbContext context, string userId)
    {
        _context = context;
        _userId  = userId;
    }

    public async Task ImportFromStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (!stream.CanRead)
            throw new ArgumentException("Потік не може бути прочитаний.", nameof(stream));

        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        using var spreadsheet = SpreadsheetDocument.Open(ms, isEditable: false);
        var workbookPart = spreadsheet.WorkbookPart
            ?? throw new InvalidOperationException("Файл не містить workbook.");

        var sharedStrings = LoadSharedStrings(workbookPart);
        var folderCache   = new Dictionary<string, Folder>(StringComparer.OrdinalIgnoreCase);
        var tagCache      = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);

        var sheets = workbookPart.Workbook.Descendants<Sheet>().ToList();

        foreach (var sheet in sheets)
        {
            if (sheet.Id?.Value is not string relId) continue;
            if (workbookPart.GetPartById(relId) is not WorksheetPart wsPart) continue;

            var sheetName = sheet.Name?.Value ?? "Sheet";
            await ProcessWorksheetAsync(wsPart, sheetName, sharedStrings, folderCache, tagCache, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessWorksheetAsync(
        WorksheetPart wsPart, string rootFolderName,
        List<string> ss, Dictionary<string, Folder> folderCache,
        Dictionary<string, Tag> tagCache, CancellationToken ct)
    {
        var rows = wsPart.Worksheet.Descendants<Row>()
                         .OrderBy(r => r.RowIndex?.Value ?? 0).ToList();
        if (rows.Count < 3) return;

        var rootFolder = await GetOrCreateFolderChainAsync(rootFolderName, folderCache, ct);

        var row1 = rows.FirstOrDefault(r => r.RowIndex?.Value == 1);
        if (row1 != null) TryReadFolderDate(row1, rootFolder, ss);

        var headerRow   = rows.FirstOrDefault(r => r.RowIndex?.Value == 2);
        var firstHeader = headerRow != null ? GetCellValue(headerRow, 1, ss)?.Trim() : null;
        bool hasPath    = firstHeader == "Шлях" || firstHeader == "Каталог";

        int colPath    = hasPath ? 1 : -1;
        int colFileName = hasPath ? 2 : 1;
        int colDesc    = hasPath ? 3 : 2;
        int colTags    = hasPath ? 4 : 3;
        int colDate    = hasPath ? 5 : 4;
        int colVer     = hasPath ? 6 : 5;
        int colContent = hasPath ? 7 : 6;
        int colChg     = hasPath ? 8 : 7;
        int colVerDate = hasPath ? 9 : 8;

        var fileCache   = new Dictionary<string, File>(StringComparer.OrdinalIgnoreCase);
        File? currentFile = null;

        foreach (var row in rows.Where(r => r.RowIndex?.Value >= 3).OrderBy(r => r.RowIndex?.Value))
        {
            var relativePath = colPath != -1 ? GetCellValue(row, colPath, ss) : string.Empty;
            var fileName     = GetCellValue(row, colFileName, ss);

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var fullPath = string.IsNullOrWhiteSpace(relativePath)
                    ? rootFolderName : $"{rootFolderName}/{relativePath}";

                var targetFolder = await GetOrCreateFolderChainAsync(fullPath, folderCache, ct);
                currentFile = await GetOrCreateFileAsync(fileName, targetFolder, fileCache, ct);

                var desc = GetCellValue(row, colDesc, ss);
                if (!string.IsNullOrWhiteSpace(desc)) currentFile.Description = desc;

                var rawTags = GetCellValue(row, colTags, ss);
                if (!string.IsNullOrWhiteSpace(rawTags))
                    await AttachTagsAsync(rawTags, currentFile, tagCache, ct);

                if (TryParseDate(GetCellValue(row, colDate, ss), out var fd)) currentFile.Createdat = fd;
            }
            else if (colPath != -1 && !string.IsNullOrWhiteSpace(relativePath)
                     && string.IsNullOrWhiteSpace(GetCellValue(row, colVer, ss))
                     && string.IsNullOrWhiteSpace(GetCellValue(row, colContent, ss)))
            {
                await GetOrCreateFolderChainAsync($"{rootFolderName}/{relativePath}", folderCache, ct);
                currentFile = null;
                continue;
            }

            if (currentFile is null) continue;

            var content   = GetCellValue(row, colContent, ss);
            var changelog = GetCellValue(row, colChg, ss);
            if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(changelog)) continue;

            var versionStr = GetCellValue(row, colVer, ss);
            if (!int.TryParse(versionStr, out var vNum) || vNum <= 0)
                vNum = currentFile.Fileversions.Any()
                    ? currentFile.Fileversions.Max(v => v.Versionnumber) + 1 : 1;

            if (currentFile.Fileversions.Any(v => v.Versionnumber == vNum)) continue;

            DateTime vDate = TryParseDate(GetCellValue(row, colVerDate, ss), out var pDate)
                ? pDate : DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            var version = new Fileversion
            {
                File = currentFile, Versionnumber = vNum,
                Content = content, Changelog = changelog, Createdat = vDate
            };
            currentFile.Fileversions.Add(version);
            _context.Fileversions.Add(version);
        }
    }

    private static List<string> LoadSharedStrings(WorkbookPart wbp)
    {
        var result = new List<string>();
        var ssPart = wbp.SharedStringTablePart;
        if (ssPart is null) return result;
        foreach (var item in ssPart.SharedStringTable.Elements<SharedStringItem>())
            result.Add(item.InnerText);
        return result;
    }

    private static string? GetCellValue(Row row, int colIndex, List<string> ss)
    {
        var colLetter = ColumnIndexToLetter(colIndex);
        var cellRef   = $"{colLetter}{row.RowIndex?.Value}";

        var cell = row.Elements<Cell>()
            .FirstOrDefault(c => c.CellReference?.Value == cellRef);

        if (cell?.CellValue is null) return null;
        var raw = cell.CellValue.InnerText;

        if (cell.DataType?.Value == CellValues.SharedString)
        {
            if (int.TryParse(raw, out var idx) && idx < ss.Count) return ss[idx];
            return null;
        }
        if (cell.DataType?.Value == CellValues.InlineString)
            return cell.InlineString?.InnerText;

        return raw;
    }

    private static string ColumnIndexToLetter(int index)
    {
        var result = string.Empty;
        while (index > 0)
        {
            index--;
            result = (char)('A' + index % 26) + result;
            index /= 26;
        }
        return result;
    }

    private static void TryReadFolderDate(Row row, Folder folder, List<string> ss)
    {
        var label = GetCellValue(row, 1, ss)?.Trim() ?? string.Empty;
        if (!label.Equals(FolderExportService.FolderDateLabel, StringComparison.OrdinalIgnoreCase)) return;
        var dateStr = GetCellValue(row, 2, ss)?.Trim() ?? string.Empty;
        if (TryParseDate(dateStr, out var date)) folder.Createdat = date;
    }

    private async Task<Folder> GetOrCreateFolderChainAsync(
        string fullPath, Dictionary<string, Folder> cache, CancellationToken ct)
    {
        var parts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Folder? currentParent = null;
        string  currentPath   = "";

        for (int depth = 0; depth < parts.Length; depth++)
        {
            var part = parts[depth];
            currentPath = depth == 0 ? part : $"{currentPath}/{part}";

            if (cache.TryGetValue(currentPath, out var folder))
            {
                currentParent = folder;
                continue;
            }

            int? parentId = currentParent?.Id;

            folder = depth == 0
                ? _context.Folders.Local.FirstOrDefault(f => f.Name == part && f.Parentfolderid == null && f.UserId == _userId)
                  ?? await _context.Folders.FirstOrDefaultAsync(f => f.Name == part && f.Parentfolderid == null && f.UserId == _userId, ct)
                : _context.Folders.Local.FirstOrDefault(f => f.Name == part && f.Parentfolderid == parentId)
                  ?? await _context.Folders.FirstOrDefaultAsync(f => f.Name == part && f.Parentfolderid == parentId, ct);

            if (folder is null)
            {
                folder = new Folder
                {
                    Name = part, Parentfolderid = parentId,
                    Createdat = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    UserId = depth == 0 ? _userId : null
                };
                _context.Folders.Add(folder);
                await _context.SaveChangesAsync(ct);
            }

            cache[currentPath] = folder;
            currentParent = folder;
        }

        return currentParent ?? throw new InvalidOperationException("Помилка обробки шляху.");
    }

    private async Task<File> GetOrCreateFileAsync(
        string name, Folder folder, Dictionary<string, File> cache, CancellationToken ct)
    {
        var key = $"{folder.Id}_{name}";
        if (cache.TryGetValue(key, out var cached)) return cached;

        var file = folder.Id > 0
            ? await _context.Files.Include(f => f.Tags).Include(f => f.Fileversions)
                             .FirstOrDefaultAsync(f => f.Name == name && f.Folderid == folder.Id, ct)
            : null;

        if (file is null)
        {
            file = new File { Name = name, Folder = folder, Createdat = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) };
            _context.Files.Add(file);
        }
        cache[key] = file;
        return file;
    }

    private async Task AttachTagsAsync(string rawTags, File file, Dictionary<string, Tag> cache, CancellationToken ct)
    {
        var names = rawTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                           .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var tagName in names)
        {
            var safe = tagName.Length > 15 ? tagName[..15] : tagName;
            if (file.Tags.Any(t => t.Name.Equals(safe, StringComparison.OrdinalIgnoreCase))) continue;
            if (!cache.TryGetValue(safe, out var tag))
            {
                tag = _context.Tags.Local.FirstOrDefault(t => t.Name == safe && t.UserId == _userId)
                   ?? await _context.Tags.FirstOrDefaultAsync(t => t.Name == safe && t.UserId == _userId, ct);
                if (tag is null) { tag = new Tag { Name = safe, UserId = _userId }; _context.Tags.Add(tag); }
                cache[safe] = tag;
            }
            file.Tags.Add(tag);
        }
    }

    private static bool TryParseDate(string? value, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (DateTime.TryParseExact(value, DateFormat,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
        {
            result = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
            return true;
        }
        return false;
    }
}
