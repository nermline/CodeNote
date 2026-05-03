using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;

namespace NoteInfrastructure.Services;

public class FolderExportService : IExportService<Folder>
{
    private readonly NotedbContext _context;
    private readonly string        _userId;

    internal const string FolderDateLabel = "📁 Дата папки:";

    private static readonly IReadOnlyList<string> HeaderNames = new[]
    {
        "Шлях", "Назва файлу", "Опис", "Теги", "Дата файлу",
        "Номер версії", "Вміст", "Журнал змін", "Дата версії",
    };

    public FolderExportService(NotedbContext context, string userId)
    {
        _context = context;
        _userId  = userId;
    }

    public async Task WriteToAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (!stream.CanWrite)
            throw new ArgumentException("Потік не підтримує запис.", nameof(stream));

        // Завантажуємо всі папки і фільтруємо в пам'яті — підтримує довільну глибину вкладеності
        var allFoldersRaw = await _context.Folders
            .Include(f => f.Files).ThenInclude(file => file.Tags)
            .Include(f => f.Files).ThenInclude(file => file.Fileversions)
            .ToListAsync(cancellationToken);

        // Збираємо Id усіх папок, що належать поточному користувачу (рекурсивно вниз)
        var userRootIds = allFoldersRaw
            .Where(f => f.UserId == _userId)
            .Select(f => f.Id)
            .ToHashSet();

        var userFolderIds = new HashSet<int>(userRootIds);
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var f in allFoldersRaw)
                if (f.Parentfolderid.HasValue &&
                    userFolderIds.Contains(f.Parentfolderid.Value) &&
                    userFolderIds.Add(f.Id))
                    changed = true;
        }

        var allFolders = allFoldersRaw.Where(f => userFolderIds.Contains(f.Id)).ToList();

        // Кореневі папки цього користувача
        var rootFolders = allFolders
            .Where(f => f.UserId == _userId)
            .OrderBy(f => f.Name)
            .ToList();

        var workbook = new XLWorkbook();

        foreach (var root in rootFolders)
        {
            var sheetName = root.Name.Length > 31 ? root.Name[..31] : root.Name;
            var worksheet = workbook.Worksheets.Add(sheetName);

            WriteFolderMetaRow(worksheet, root);
            WriteHeader(worksheet);

            int rowIndex = 3;
            WriteFolderTreeExcel(worksheet, root, allFolders, "", ref rowIndex);
            worksheet.Columns().AdjustToContents();
        }

        if (!workbook.Worksheets.Any()) workbook.Worksheets.Add("Порожньо");
        workbook.SaveAs(stream);
    }

    private static void WriteFolderTreeExcel(
        IXLWorksheet worksheet, Folder folder,
        List<Folder> allFolders, string relativePath, ref int rowIndex)
    {
        var files = folder.Files.OrderBy(f => f.Name).ToList();

        if (files.Count == 0)
        {
            if (!string.IsNullOrEmpty(relativePath))
            {
                worksheet.Cell(rowIndex, 1).Value = relativePath;
                rowIndex++;
            }
        }
        else
        {
            foreach (var file in files) WriteFile(worksheet, file, relativePath, ref rowIndex);
        }

        foreach (var sub in allFolders.Where(f => f.Parentfolderid == folder.Id).OrderBy(f => f.Name))
        {
            var childPath = string.IsNullOrEmpty(relativePath) ? sub.Name : $"{relativePath}/{sub.Name}";
            WriteFolderTreeExcel(worksheet, sub, allFolders, childPath, ref rowIndex);
        }
    }

    private static void WriteFolderMetaRow(IXLWorksheet worksheet, Folder folder)
    {
        worksheet.Cell(1, 1).Value = FolderDateLabel;
        worksheet.Cell(1, 2).Value = folder.Createdat?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty;
        var row = worksheet.Row(1);
        row.Style.Font.Italic = true;
        row.Style.Font.FontColor = XLColor.FromHtml("#595959");
        row.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
    }

    private static void WriteHeader(IXLWorksheet worksheet)
    {
        for (int col = 0; col < HeaderNames.Count; col++)
            worksheet.Cell(2, col + 1).Value = HeaderNames[col];
        var row = worksheet.Row(2);
        row.Style.Font.Bold = true;
        row.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
    }

    private static void WriteFile(
        IXLWorksheet worksheet, NoteDomain.Model.File file,
        string relativePath, ref int rowIndex)
    {
        var versions = file.Fileversions.OrderBy(v => v.Versionnumber).ToList();
        if (versions.Count == 0)
        {
            WriteFileRow(worksheet, rowIndex++, file, null, true, relativePath);
            return;
        }
        for (int i = 0; i < versions.Count; i++)
            WriteFileRow(worksheet, rowIndex++, file, versions[i], i == 0, relativePath);
    }

    private static void WriteFileRow(
        IXLWorksheet worksheet, int rowIndex,
        NoteDomain.Model.File file, Fileversion? version,
        bool isFirstRow, string relativePath)
    {
        if (isFirstRow)
        {
            worksheet.Cell(rowIndex, 1).Value = relativePath;
            worksheet.Cell(rowIndex, 2).Value = file.Name;
            worksheet.Cell(rowIndex, 3).Value = file.Description ?? string.Empty;
            worksheet.Cell(rowIndex, 4).Value = string.Join(", ", file.Tags.Select(t => t.Name));
            worksheet.Cell(rowIndex, 5).Value = file.Createdat?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty;
        }
        if (version is not null)
        {
            worksheet.Cell(rowIndex, 6).Value = version.Versionnumber;
            worksheet.Cell(rowIndex, 7).Value = version.Content ?? string.Empty;
            worksheet.Cell(rowIndex, 8).Value = version.Changelog ?? string.Empty;
            worksheet.Cell(rowIndex, 9).Value = version.Createdat?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty;
        }
    }
}
