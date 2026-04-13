using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using File = NoteDomain.Model.File;

namespace NoteInfrastructure.Services
{
    /// <summary>
    /// Імпортує каталоги, файли, теги та ВСІ версії файлів з Excel-файлу.
    ///
    /// Очікувана структура (відповідає FolderExportService):
    ///   - Кожен аркуш = один кореневий каталог.
    ///   - Рядок 1    – метадані: «📁 Дата папки:» у A1, дата у B1.
    ///   - Рядок 2    – заголовок (пропускається).
    ///   - Рядки 3+   – дані.
    ///
    /// Колонки:
    ///   A Назва файлу  – лише в першому рядку файлу (решта рядків – порожньо)
    ///   B Опис
    ///   C Теги
    ///   D Дата файлу
    ///   E Номер версії
    ///   F Вміст
    ///   G Журнал змін
    ///   H Дата версії
    ///
    /// Якщо файл має декілька версій — він займає декілька рядків
    /// (назва/теги/дата файлу лише в першому рядку; решта — нова версія).
    /// </summary>
    public class FolderImportService : IImportService<Folder>
    {
        private readonly NotedbContext _context;

        private const int ColFileName    = 1;
        private const int ColDescription = 2;
        private const int ColTags        = 3;
        private const int ColFileDate    = 4;
        private const int ColVersion     = 5;
        private const int ColContent     = 6;
        private const int ColChangelog   = 7;
        private const int ColVersionDate = 8;

        private const string DateFormat = "dd.MM.yyyy HH:mm";

        public FolderImportService(NotedbContext context)
        {
            _context = context;
        }

        public async Task ImportFromStreamAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (!stream.CanRead)
                throw new ArgumentException("Потік даних не може бути прочитаний.", nameof(stream));

            var folderCache = new Dictionary<string, Folder>(StringComparer.OrdinalIgnoreCase);
            var tagCache    = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);

            using var workbook = new XLWorkbook(stream);

            foreach (IXLWorksheet worksheet in workbook.Worksheets)
            {
                var folder = await GetOrCreateFolderAsync(worksheet.Name, folderCache, cancellationToken);

                // Рядок 1 — метадані каталогу (дата створення)
                TryReadFolderDate(worksheet, folder);

                var fileCache    = new Dictionary<string, File>(StringComparer.OrdinalIgnoreCase);
                File? currentFile = null;

                // Пропускаємо рядок 1 (метадані) і рядок 2 (заголовок)
                foreach (var row in worksheet.RowsUsed().Skip(2))
                {
                    currentFile = await AddFileRowAsync(row, folder, fileCache, tagCache, currentFile, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        // ──────────────────────────────────────────────
        // Метадані каталогу
        // ──────────────────────────────────────────────

        private static void TryReadFolderDate(IXLWorksheet worksheet, Folder folder)
        {
            var label = worksheet.Cell(1, 1).Value.ToString()?.Trim() ?? string.Empty;
            if (!label.Equals(FolderExportService.FolderDateLabel, StringComparison.OrdinalIgnoreCase))
                return;

            var dateStr = worksheet.Cell(1, 2).Value.ToString()?.Trim() ?? string.Empty;
            if (TryParseDate(dateStr, out var date))
                folder.Createdat = date;
        }

        // ──────────────────────────────────────────────
        // Обробка рядка даних
        // ──────────────────────────────────────────────

        /// <returns>Поточний файл (для передачі у наступний рядок).</returns>
        private async Task<File?> AddFileRowAsync(
            IXLRow row,
            Folder folder,
            Dictionary<string, File> fileCache,
            Dictionary<string, Tag>  tagCache,
            File? currentFile,
            CancellationToken ct)
        {
            var fileName = GetCellString(row, ColFileName);

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                // Новий файл — отримуємо або створюємо
                currentFile = await GetOrCreateFileAsync(fileName, folder, fileCache, ct);

                var desc = GetCellString(row, ColDescription);
                if (!string.IsNullOrWhiteSpace(desc))
                    currentFile.Description = desc;

                var rawTags = GetCellString(row, ColTags);
                if (!string.IsNullOrWhiteSpace(rawTags))
                    await AttachTagsAsync(rawTags, currentFile, tagCache, ct);

                // Дата файлу
                var fileDateStr = GetCellString(row, ColFileDate);
                if (TryParseDate(fileDateStr, out var fileDate))
                    currentFile.Createdat = fileDate;
            }

            // Якщо ім'я порожнє — продовжуємо додавати версії до попереднього файлу
            if (currentFile is null) return null;

            AddFileVersion(row, currentFile);
            return currentFile;
        }

        // ──────────────────────────────────────────────
        // Сутності
        // ──────────────────────────────────────────────

        private async Task<Folder> GetOrCreateFolderAsync(
            string name,
            Dictionary<string, Folder> cache,
            CancellationToken ct)
        {
            if (cache.TryGetValue(name, out var cached)) return cached;

            var folder = await _context.Folders
                .FirstOrDefaultAsync(f => f.Name == name && f.Parentfolderid == null, ct);

            if (folder is null)
            {
                folder = new Folder { Name = name, Createdat = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) };
                _context.Folders.Add(folder);
            }

            cache[name] = folder;
            return folder;
        }

        private async Task<File> GetOrCreateFileAsync(
            string name,
            Folder folder,
            Dictionary<string, File> cache,
            CancellationToken ct)
        {
            if (cache.TryGetValue(name, out var cached)) return cached;

            File? file = null;
            if (folder.Id > 0)
            {
                file = await _context.Files
                    .Include(f => f.Tags)
                    .Include(f => f.Fileversions)
                    .FirstOrDefaultAsync(f => f.Name == name && f.Folderid == folder.Id, ct);
            }

            if (file is null)
            {
                file = new File
                {
                    Name      = name,
                    Folder    = folder,
                    Createdat = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                };
                _context.Files.Add(file);
            }

            cache[name] = file;
            return file;
        }

        private async Task AttachTagsAsync(
            string rawTags,
            File file,
            Dictionary<string, Tag> cache,
            CancellationToken ct)
        {
            var tagNames = rawTags
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var tagName in tagNames)
            {
                var safeName = tagName.Length > 15 ? tagName[..15] : tagName;

                if (file.Tags.Any(t => t.Name.Equals(safeName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (!cache.TryGetValue(safeName, out var tag))
                {
                    tag = await _context.Tags
                        .FirstOrDefaultAsync(t => t.Name == safeName, ct);

                    if (tag is null)
                    {
                        tag = new Tag { Name = safeName };
                        _context.Tags.Add(tag);
                    }

                    cache[safeName] = tag;
                }

                file.Tags.Add(tag);
            }
        }

        private void AddFileVersion(IXLRow row, File file)
        {
            var content   = GetCellString(row, ColContent);
            var changelog = GetCellString(row, ColChangelog);

            if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(changelog))
                return;

            var versionStr = GetCellString(row, ColVersion);
            if (!int.TryParse(versionStr, out var vNum) || vNum <= 0)
            {
                vNum = file.Fileversions.Any()
                    ? file.Fileversions.Max(v => v.Versionnumber) + 1
                    : 1;
            }

            if (file.Fileversions.Any(v => v.Versionnumber == vNum))
                return;

            // Дата версії
            var versionDateStr = GetCellString(row, ColVersionDate);
            DateTime versionDate = TryParseDate(versionDateStr, out var parsedVDate)
                ? parsedVDate
                : DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            var version = new Fileversion
            {
                File          = file,
                Versionnumber = vNum,
                Content       = content,
                Changelog     = changelog,
                Createdat     = versionDate,
            };

            file.Fileversions.Add(version);
            _context.Fileversions.Add(version);
        }

        // ──────────────────────────────────────────────
        // Утиліти
        // ──────────────────────────────────────────────

        private static string GetCellString(IXLRow row, int column)
            => row.Cell(column).Value.ToString()?.Trim() ?? string.Empty;

        private static bool TryParseDate(string value, out DateTime result)
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
}
