using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using NoteInfrastructure.Services.Docx;

namespace NoteInfrastructure.Services
{
    public class FolderDocxExportService : IExportService<Folder>
    {
        private readonly NotedbContext _context;

        public FolderDocxExportService(NotedbContext context)
        {
            _context = context;
        }

        public async Task WriteToAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (!stream.CanWrite) throw new ArgumentException("Потік не підтримує запис.", nameof(stream));

            var allFolders = await _context.Folders
                .Include(f => f.Files).ThenInclude(file => file.Tags)
                .Include(f => f.Files).ThenInclude(file => file.Fileversions)
                .ToListAsync(cancellationToken);

            var rootFolders = allFolders.Where(f => f.Parentfolderid == null).OrderBy(f => f.Name).ToList();

            using var ms = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                AddHeadingStyles(mainPart);
                var body = mainPart.Document.Body!;

                body.AppendChild(DocxHelper.Heading1($"Звіт системи нотаток — {DateTime.UtcNow:dd.MM.yyyy}"));
                body.AppendChild(DocxHelper.NormalParagraph($"Сформовано: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC"));
                body.AppendChild(new Paragraph());

                foreach (var folder in rootFolders)
                {
                    WriteFolderTree(body, folder, allFolders, "");
                }
                mainPart.Document.Save();
            }

            ms.Position = 0;
            await ms.CopyToAsync(stream, cancellationToken);
        }

        private static void WriteFolderTree(Body body, Folder folder, List<Folder> allFolders, string parentPath)
        {
            string currentPath = string.IsNullOrEmpty(parentPath) ? folder.Name : $"{parentPath}/{folder.Name}";

            body.AppendChild(DocxHelper.HorizontalRule());
            body.AppendChild(DocxHelper.Heading1($"📁 Каталог: {currentPath}"));

            if (folder.Createdat.HasValue)
                body.AppendChild(DocxHelper.NormalParagraph($"Дата створення: {folder.Createdat.Value:dd.MM.yyyy HH:mm}"));

            var files = folder.Files.OrderBy(f => f.Name).ToList();

            if (files.Count == 0)
            {
                body.AppendChild(DocxHelper.NormalParagraph("(каталог порожній)"));
            }
            else
            {
                foreach (var file in files) WriteFileSection(body, file);
            }

            var subFolders = allFolders.Where(f => f.Parentfolderid == folder.Id).OrderBy(f => f.Name).ToList();
            foreach (var sub in subFolders)
            {
                WriteFolderTree(body, sub, allFolders, currentPath);
            }
        }

        private static void WriteFileSection(Body body, NoteDomain.Model.File file)
        {
            body.AppendChild(new Paragraph());
            body.AppendChild(DocxHelper.Heading2($"📄 {file.Name}"));

            var fileMeta = new List<(string, string)>
            {
                ("Опис", file.Description ?? "—"),
                ("Теги", file.Tags.Any() ? string.Join(", ", file.Tags.Select(t => t.Name)) : "—"),
                ("Дата створення", file.Createdat?.ToString("dd.MM.yyyy HH:mm") ?? "—"),
                ("Версій", file.Fileversions.Count.ToString()),
            };
            body.AppendChild(DocxHelper.DetailsTable(fileMeta));

            var versions = file.Fileversions.OrderBy(v => v.Versionnumber).ToList();
            foreach (var version in versions) WriteVersionSection(body, version);
        }

        private static void WriteVersionSection(Body body, Fileversion version)
        {
            body.AppendChild(new Paragraph());
            body.AppendChild(DocxHelper.NormalParagraph($"▸ Версія {version.Versionnumber}", bold: true));

            var versionMeta = new List<(string, string)>
            {
                ("Журнал змін", version.Changelog ?? "—"),
                ("Дата", version.Createdat?.ToString("dd.MM.yyyy HH:mm") ?? "—"),
                ("Вміст", TruncateContent(version.Content, 500)),
            };
            body.AppendChild(DocxHelper.DetailsTable(versionMeta));
        }

        private static string TruncateContent(string? content, int maxLength)
        {
            if (string.IsNullOrEmpty(content)) return "—";
            return content.Length <= maxLength ? content : content[..maxLength] + $"… [всього {content.Length} символів]";
        }

        private static void AddHeadingStyles(MainDocumentPart mainPart)
        {
            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles();
            stylesPart.Styles.AppendChild(BuildHeadingStyle("Heading1", "1", 28, true, "1F3864"));
            stylesPart.Styles.AppendChild(BuildHeadingStyle("Heading2", "2", 24, true, "2E74B5"));
            stylesPart.Styles.Save();
        }

        private static Style BuildHeadingStyle(string styleId, string uiPriority, int fontSize, bool bold, string color)
        {
            var style = new Style { Type = StyleValues.Paragraph, StyleId = styleId };
            style.AppendChild(new StyleName { Val = styleId });
            var rpr = new StyleRunProperties();
            if (bold) rpr.AppendChild(new Bold());
            rpr.AppendChild(new Color { Val = color });
            rpr.AppendChild(new FontSize { Val = (fontSize * 2).ToString() });
            style.AppendChild(rpr);
            return style;
        }
    }
}