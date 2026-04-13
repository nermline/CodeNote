using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;

namespace NoteInfrastructure.Services
{
    /// <summary>
    /// Експортує каталоги разом з файлами, тегами та ВСІМА версіями кожного файлу
    /// у Excel-файл.
    ///
    /// Структура файлу:
    ///   - Кожен аркуш = один кореневий каталог.
    ///   - Рядок 1    – метадані каталогу: «📁 Дата папки:» | дата_створення
    ///   - Рядок 2    – заголовок (жирний).
    ///   - Рядки 3+   – дані (один рядок на версію файлу).
    ///
    /// Колонки:
    ///   A Назва файлу  – лише у першому рядку файлу
    ///   B Опис         – лише у першому рядку файлу
    ///   C Теги         – лише у першому рядку файлу
    ///   D Дата файлу   – лише у першому рядку файлу
    ///   E Номер версії
    ///   F Вміст
    ///   G Журнал змін
    ///   H Дата версії
    /// </summary>
    public class FolderExportService : IExportService<Folder>
    {
        private readonly NotedbContext _context;

        internal const string FolderDateLabel = "📁 Дата папки:";

        private static readonly IReadOnlyList<string> HeaderNames = new[]
        {
            "Назва файлу",
            "Опис",
            "Теги",
            "Дата файлу",
            "Номер версії",
            "Вміст",
            "Журнал змін",
            "Дата версії",
        };

        public FolderExportService(NotedbContext context)
        {
            _context = context;
        }

        public async Task WriteToAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (!stream.CanWrite)
                throw new ArgumentException("Потік не підтримує запис.", nameof(stream));

            var folders = await _context.Folders
                .Where(f => f.Parentfolderid == null)
                .Include(f => f.Files)
                    .ThenInclude(file => file.Tags)
                .Include(f => f.Files)
                    .ThenInclude(file => file.Fileversions)
                .OrderBy(f => f.Name)
                .ToListAsync(cancellationToken);

            var workbook = new XLWorkbook();

            foreach (var folder in folders)
            {
                var sheetName = folder.Name.Length > 31
                    ? folder.Name[..31]
                    : folder.Name;

                var worksheet = workbook.Worksheets.Add(sheetName);
                WriteFolderMetaRow(worksheet, folder);
                WriteHeader(worksheet);
                WriteFiles(worksheet, folder.Files.OrderBy(f => f.Name).ToList());
                worksheet.Columns().AdjustToContents();
            }

            if (!workbook.Worksheets.Any())
                workbook.Worksheets.Add("Порожньо");

            workbook.SaveAs(stream);
        }

        private static void WriteFolderMetaRow(IXLWorksheet worksheet, Folder folder)
        {
            worksheet.Cell(1, 1).Value = FolderDateLabel;
            worksheet.Cell(1, 2).Value = folder.Createdat?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty;

            var metaRow = worksheet.Row(1);
            metaRow.Style.Font.Italic = true;
            metaRow.Style.Font.FontColor = XLColor.FromHtml("#595959");
            metaRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
        }

        private static void WriteHeader(IXLWorksheet worksheet)
        {
            for (int col = 0; col < HeaderNames.Count; col++)
                worksheet.Cell(2, col + 1).Value = HeaderNames[col];

            var headerRow = worksheet.Row(2);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        }

        private static void WriteFiles(IXLWorksheet worksheet, IList<NoteDomain.Model.File> files)
        {
            int rowIndex = 3;
            foreach (var file in files)
                WriteFile(worksheet, file, ref rowIndex);
        }

        private static void WriteFile(IXLWorksheet worksheet, NoteDomain.Model.File file, ref int rowIndex)
        {
            var versions = file.Fileversions
                .OrderBy(v => v.Versionnumber)
                .ToList();

            if (versions.Count == 0)
            {
                WriteFileRow(worksheet, rowIndex++, file, version: null, isFirstRow: true);
                return;
            }

            for (int i = 0; i < versions.Count; i++)
                WriteFileRow(worksheet, rowIndex++, file, versions[i], isFirstRow: i == 0);
        }

        private static void WriteFileRow(
            IXLWorksheet worksheet,
            int rowIndex,
            NoteDomain.Model.File file,
            Fileversion? version,
            bool isFirstRow)
        {
            if (isFirstRow)
            {
                worksheet.Cell(rowIndex, 1).Value = file.Name;
                worksheet.Cell(rowIndex, 2).Value = file.Description ?? string.Empty;
                worksheet.Cell(rowIndex, 3).Value = string.Join(", ", file.Tags.Select(t => t.Name));
                worksheet.Cell(rowIndex, 4).Value = file.Createdat?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty;
            }

            if (version is not null)
            {
                worksheet.Cell(rowIndex, 5).Value = version.Versionnumber;
                worksheet.Cell(rowIndex, 6).Value = version.Content ?? string.Empty;
                worksheet.Cell(rowIndex, 7).Value = version.Changelog ?? string.Empty;
                worksheet.Cell(rowIndex, 8).Value = version.Createdat?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty;
            }
        }
    }
}
