using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using NoteInfrastructure.Services.Docx;
using File = NoteDomain.Model.File;
using NoteTag = NoteDomain.Model.Tag;

namespace NoteInfrastructure.Services
{
    /// <summary>
    /// Імпортує каталоги, файли, теги та версії файлів з .docx-документа.
    ///
    /// Очікувана структура (відповідає FolderDocxExportService):
    ///   Heading1  → назва каталогу (після «📁 Каталог:»)
    ///   Абзац «Дата створення: dd.MM.yyyy HH:mm» → дата каталогу
    ///   Heading2  → назва файлу (після «📄 »)
    ///   Таблиця після H2 → деталі файлу: Опис, Теги, Дата створення
    ///   Абзац «▸ Версія N» → нова версія
    ///   Таблиця після маркера версії → Журнал змін, Дата, Вміст
    /// </summary>
    public class FolderDocxImportService : IImportService<Folder>
    {
        private readonly NotedbContext _context;

        private const string FolderPrefix      = "📁 Каталог:";
        private const string FilePrefix        = "📄 ";
        private const string VersionPrefix     = "▸ Версія ";
        private const string FolderDatePrefix  = "Дата створення:";

        private const string LabelDescription  = "Опис";
        private const string LabelTags         = "Теги";
        private const string LabelFileDate     = "Дата створення";
        private const string LabelChangelog    = "Журнал змін";
        private const string LabelVersionDate  = "Дата";
        private const string LabelContent      = "Вміст";

        private const string DateFormat        = "dd.MM.yyyy HH:mm";

        public FolderDocxImportService(NotedbContext context)
        {
            _context = context;
        }

        // ── Клас-стан ──────────────────────────────────────────────────────
        private sealed class ImportState
        {
            public Folder?      CurrentFolder  { get; set; }
            public File?        CurrentFile    { get; set; }
            public Fileversion? CurrentVersion { get; set; }
            public bool         InFileMeta     { get; set; }
            public bool         InVersionBlock { get; set; }
        }

        public async Task ImportFromStreamAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (!stream.CanRead)
                throw new ArgumentException("Потік не може бути прочитаний.", nameof(stream));

            var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;

            using var doc = WordprocessingDocument.Open(ms, isEditable: false);
            var body = doc.MainDocumentPart?.Document?.Body
                       ?? throw new InvalidOperationException("Документ не містить тіла.");

            var folderCache = new Dictionary<string, Folder>(StringComparer.OrdinalIgnoreCase);
            var fileCache   = new Dictionary<string, File>(StringComparer.OrdinalIgnoreCase);
            var tagCache    = new Dictionary<string, NoteTag>(StringComparer.OrdinalIgnoreCase);

            var state = new ImportState();

            foreach (var element in body.ChildElements)
            {
                switch (element)
                {
                    case Paragraph para:
                        await HandleParagraphAsync(para, state, folderCache, fileCache, tagCache, cancellationToken);
                        break;

                    case Table table:
                        await HandleTableAsync(table, state, tagCache, cancellationToken);
                        state.InFileMeta     = false;
                        state.InVersionBlock = false;
                        break;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        // ──────────────────────────────────────────────
        // Обробники елементів
        // ──────────────────────────────────────────────

        private async Task HandleParagraphAsync(
            Paragraph para,
            ImportState state,
            Dictionary<string, Folder>  folderCache,
            Dictionary<string, File>    fileCache,
            Dictionary<string, NoteTag> tagCache,
            CancellationToken ct)
        {
            var text = DocxHelper.GetParagraphText(para).Trim();
            if (string.IsNullOrEmpty(text)) return;

            // H1 → каталог
            if (DocxHelper.IsHeading(para, 1))
            {
                if (!text.StartsWith(FolderPrefix, StringComparison.Ordinal))
                    return;

                var folderName = text[FolderPrefix.Length..].Trim();
                state.CurrentFolder  = await GetOrCreateFolderAsync(folderName, folderCache, ct);
                state.CurrentFile    = null;
                state.CurrentVersion = null;
                state.InFileMeta     = false;
                state.InVersionBlock = false;
                fileCache.Clear();
                return;
            }

            // H2 → файл
            if (DocxHelper.IsHeading(para, 2))
            {
                state.CurrentFolder ??= await GetOrCreateFolderAsync("Імпортовано з docx", folderCache, ct);

                var fileName = text.StartsWith(FilePrefix)
                    ? text[FilePrefix.Length..].Trim()
                    : text;

                state.CurrentFile    = await GetOrCreateFileAsync(fileName, state.CurrentFolder, fileCache, ct);
                state.CurrentVersion = null;
                state.InFileMeta     = true;
                state.InVersionBlock = false;
                return;
            }

            // Абзац з датою каталогу: «Дата створення: 13.04.2026 06:03»
            if (state.CurrentFolder is not null
                && text.StartsWith(FolderDatePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var dateStr = text[FolderDatePrefix.Length..].Trim();
                if (TryParseDate(dateStr, out var folderDate))
                    state.CurrentFolder.Createdat = folderDate;
                return;
            }

            // Маркер версії: «▸ Версія N»
            if (text.StartsWith(VersionPrefix) && state.CurrentFile is not null)
            {
                var versionStr = text[VersionPrefix.Length..].Trim();
                int.TryParse(versionStr, out var vNum);
                if (vNum <= 0) vNum = NextVersionNumber(state.CurrentFile);

                if (state.CurrentFile.Fileversions.Any(v => v.Versionnumber == vNum))
                    return;

                state.CurrentVersion = new Fileversion
                {
                    File          = state.CurrentFile,
                    Versionnumber = vNum,
                    Createdat     = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                };
                _context.Fileversions.Add(state.CurrentVersion);
                state.InVersionBlock = true;
                state.InFileMeta     = false;
            }
        }

        private async Task HandleTableAsync(
            Table table, ImportState state, Dictionary<string, NoteTag> tagCache, CancellationToken ct)
        {
            if (state.CurrentFile is null) return;

            var rows = table.Descendants<TableRow>().ToList();

            if (state.InFileMeta)
            {
                foreach (var row in rows)
                {
                    var label = DocxHelper.GetCellText(row, 0);
                    var value = DocxHelper.GetCellText(row, 1);

                    if (label.Equals(LabelDescription, StringComparison.OrdinalIgnoreCase))
                        state.CurrentFile.Description = value;

                    if (label.Equals(LabelTags, StringComparison.OrdinalIgnoreCase))
                        await AttachTagsAsync(value, state.CurrentFile, tagCache, ct);

                    // Дата створення файлу
                    if (label.Equals(LabelFileDate, StringComparison.OrdinalIgnoreCase)
                        && TryParseDate(value, out var fileDate))
                        state.CurrentFile.Createdat = fileDate;
                }
                return;
            }

            if (state.InVersionBlock && state.CurrentVersion is not null)
            {
                foreach (var row in rows)
                {
                    var label = DocxHelper.GetCellText(row, 0);
                    var value = DocxHelper.GetCellText(row, 1);

                    if (label.Equals(LabelChangelog, StringComparison.OrdinalIgnoreCase))
                        state.CurrentVersion.Changelog = value;

                    // Дата версії
                    if (label.Equals(LabelVersionDate, StringComparison.OrdinalIgnoreCase)
                        && TryParseDate(value, out var versionDate))
                        state.CurrentVersion.Createdat = versionDate;

                    if (label.Equals(LabelContent, StringComparison.OrdinalIgnoreCase))
                    {
                        var content  = value;
                        var truncIdx = content.LastIndexOf("… [всього", StringComparison.Ordinal);
                        if (truncIdx >= 0) content = content[..truncIdx];
                        state.CurrentVersion.Content = content;
                    }
                }
                return;
            }

            // Таблиця без явного контексту — розпізнаємо мітки
            foreach (var row in rows)
            {
                var label = DocxHelper.GetCellText(row, 0);
                var value = DocxHelper.GetCellText(row, 1);

                if (label.Equals(LabelDescription, StringComparison.OrdinalIgnoreCase))
                    state.CurrentFile.Description = value;

                if (label.Equals(LabelTags, StringComparison.OrdinalIgnoreCase))
                    await AttachTagsAsync(value, state.CurrentFile, tagCache, ct);

                if (label.Equals(LabelFileDate, StringComparison.OrdinalIgnoreCase)
                    && TryParseDate(value, out var fileDate))
                    state.CurrentFile.Createdat = fileDate;

                if (state.CurrentVersion is not null)
                {
                    if (label.Equals(LabelChangelog, StringComparison.OrdinalIgnoreCase))
                        state.CurrentVersion.Changelog = value;

                    if (label.Equals(LabelVersionDate, StringComparison.OrdinalIgnoreCase)
                        && TryParseDate(value, out var versionDate))
                        state.CurrentVersion.Createdat = versionDate;

                    if (label.Equals(LabelContent, StringComparison.OrdinalIgnoreCase))
                        state.CurrentVersion.Content = value;
                }
            }
        }

        // ──────────────────────────────────────────────
        // БД-хелпери
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
            Dictionary<string, NoteTag> cache,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(rawTags) || rawTags == "—") return;

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
                        tag = new NoteTag { Name = safeName };
                        _context.Tags.Add(tag);
                    }

                    cache[safeName] = tag;
                }

                file.Tags.Add(tag);
            }
        }

        private static int NextVersionNumber(File file)
            => file.Fileversions.Any()
                ? file.Fileversions.Max(v => v.Versionnumber) + 1
                : 1;

        private static bool TryParseDate(string value, out DateTime result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(value) || value == "—") return false;

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
