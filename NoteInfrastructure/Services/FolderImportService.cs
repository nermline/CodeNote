using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using File = NoteDomain.Model.File;

namespace NoteInfrastructure.Services
{
    public class FolderImportService : IImportService<Folder>
    {
        private readonly NotedbContext _context;
        private const string DateFormat = "dd.MM.yyyy HH:mm";

        public FolderImportService(NotedbContext context)
        {
            _context = context;
        }

        public async Task ImportFromStreamAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (!stream.CanRead) throw new ArgumentException("Потік даних не може бути прочитаний.", nameof(stream));

            var folderCache = new Dictionary<string, Folder>(StringComparer.OrdinalIgnoreCase);
            var tagCache = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);

            using var workbook = new XLWorkbook(stream);

            foreach (IXLWorksheet worksheet in workbook.Worksheets)
            {
                var rootFolderName = worksheet.Name;
                var rootFolder = await GetOrCreateFolderChainAsync(rootFolderName, folderCache, cancellationToken);
                TryReadFolderDate(worksheet, rootFolder);

                var fileCache = new Dictionary<string, File>(StringComparer.OrdinalIgnoreCase);
                File? currentFile = null;

                // Визначаємо індекси колонок (підтримка зворотньої сумісності зі старими експортами)
                int colPath = -1, colFileName = 1, colDesc = 2, colTags = 3, colDate = 4, colVer = 5, colContent = 6, colChg = 7, colVerDate = 8;

                var firstHeader = worksheet.Cell(2, 1).Value.ToString()?.Trim();
                if (firstHeader == "Шлях" || firstHeader == "Каталог")
                {
                    colPath = 1; colFileName = 2; colDesc = 3; colTags = 4; colDate = 5;
                    colVer = 6; colContent = 7; colChg = 8; colVerDate = 9;
                }

                foreach (var row in worksheet.RowsUsed().Skip(2))
                {
                    var relativePath = colPath != -1 ? GetCellString(row, colPath) : string.Empty;
                    var fileName = GetCellString(row, colFileName);

                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        var fullPath = string.IsNullOrWhiteSpace(relativePath) ? rootFolderName : $"{rootFolderName}/{relativePath}";
                        var targetFolder = await GetOrCreateFolderChainAsync(fullPath, folderCache, cancellationToken);

                        currentFile = await GetOrCreateFileAsync(fileName, targetFolder, fileCache, cancellationToken);

                        var desc = GetCellString(row, colDesc);
                        if (!string.IsNullOrWhiteSpace(desc)) currentFile.Description = desc;

                        var rawTags = GetCellString(row, colTags);
                        if (!string.IsNullOrWhiteSpace(rawTags)) await AttachTagsAsync(rawTags, currentFile, tagCache, cancellationToken);

                        if (TryParseDate(GetCellString(row, colDate), out var fileDate)) currentFile.Createdat = fileDate;
                    }
                    else if (colPath != -1 && !string.IsNullOrWhiteSpace(relativePath)
                             && string.IsNullOrWhiteSpace(GetCellString(row, colVer))
                             && string.IsNullOrWhiteSpace(GetCellString(row, colContent)))
                    {
                        // Якщо це просто вказівка на створення порожньої папки
                        var fullPath = $"{rootFolderName}/{relativePath}";
                        await GetOrCreateFolderChainAsync(fullPath, folderCache, cancellationToken);
                        currentFile = null;
                        continue;
                    }

                    if (currentFile is null) continue;

                    var content = GetCellString(row, colContent);
                    var changelog = GetCellString(row, colChg);
                    if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(changelog)) continue;

                    var versionStr = GetCellString(row, colVer);
                    if (!int.TryParse(versionStr, out var vNum) || vNum <= 0)
                        vNum = currentFile.Fileversions.Any() ? currentFile.Fileversions.Max(v => v.Versionnumber) + 1 : 1;

                    if (currentFile.Fileversions.Any(v => v.Versionnumber == vNum)) continue;

                    var versionDateStr = GetCellString(row, colVerDate);
                    DateTime versionDate = TryParseDate(versionDateStr, out var parsedVDate) ? parsedVDate : DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                    var version = new Fileversion { File = currentFile, Versionnumber = vNum, Content = content, Changelog = changelog, Createdat = versionDate };
                    currentFile.Fileversions.Add(version);
                    _context.Fileversions.Add(version);
                }
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        private static void TryReadFolderDate(IXLWorksheet worksheet, Folder folder)
        {
            var label = worksheet.Cell(1, 1).Value.ToString()?.Trim() ?? string.Empty;
            if (!label.Equals(FolderExportService.FolderDateLabel, StringComparison.OrdinalIgnoreCase)) return;
            var dateStr = worksheet.Cell(1, 2).Value.ToString()?.Trim() ?? string.Empty;
            if (TryParseDate(dateStr, out var date)) folder.Createdat = date;
        }

        private async Task<Folder> GetOrCreateFolderChainAsync(string fullPath, Dictionary<string, Folder> cache, CancellationToken ct)
        {
            var parts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Folder? currentParent = null;
            string currentPath = "";

            foreach (var part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";

                if (!cache.TryGetValue(currentPath, out var folder))
                {
                    int? parentId = currentParent?.Id;
                    folder = await _context.Folders.FirstOrDefaultAsync(f => f.Name == part && f.Parentfolderid == parentId, ct);

                    if (folder == null)
                    {
                        folder = new Folder { Name = part, Parentfolderid = parentId, Createdat = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) };
                        _context.Folders.Add(folder);
                        await _context.SaveChangesAsync(ct); // Зберігаємо, щоб отримати Id для вкладених каталогів
                    }
                    cache[currentPath] = folder;
                }
                currentParent = folder;
            }
            return currentParent ?? throw new InvalidOperationException("Помилка обробки шляху папки.");
        }

        private async Task<File> GetOrCreateFileAsync(string name, Folder folder, Dictionary<string, File> cache, CancellationToken ct)
        {
            var cacheKey = $"{folder.Id}_{name}"; // Захист від однакових імен файлів в різних папках
            if (cache.TryGetValue(cacheKey, out var cached)) return cached;

            File? file = null;
            if (folder.Id > 0)
            {
                file = await _context.Files.Include(f => f.Tags).Include(f => f.Fileversions)
                    .FirstOrDefaultAsync(f => f.Name == name && f.Folderid == folder.Id, ct);
            }

            if (file is null)
            {
                file = new File { Name = name, Folder = folder, Createdat = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) };
                _context.Files.Add(file);
            }

            cache[cacheKey] = file;
            return file;
        }

        private async Task AttachTagsAsync(string rawTags, File file, Dictionary<string, Tag> cache, CancellationToken ct)
        {
            var tagNames = rawTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var tagName in tagNames)
            {
                var safeName = tagName.Length > 15 ? tagName[..15] : tagName;
                if (file.Tags.Any(t => t.Name.Equals(safeName, StringComparison.OrdinalIgnoreCase))) continue;

                if (!cache.TryGetValue(safeName, out var tag))
                {
                    tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == safeName, ct);
                    if (tag is null) { tag = new Tag { Name = safeName }; _context.Tags.Add(tag); }
                    cache[safeName] = tag;
                }
                file.Tags.Add(tag);
            }
        }

        private static string GetCellString(IXLRow row, int column) => row.Cell(column).Value.ToString()?.Trim() ?? string.Empty;
        private static bool TryParseDate(string value, out DateTime result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (DateTime.TryParseExact(value, DateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
            {
                result = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified); return true;
            }
            return false;
        }
    }
}