using NoteDomain.Model;

namespace NoteInfrastructure.Services;

/// <summary>
/// Фабрика сервісів імпорту/експорту для сутності <see cref="Folder"/>.
/// Підтримує Excel (.xlsx) та Word (.docx).
/// </summary>
public class FolderDataPortServiceFactory : IDataPortServiceFactory<Folder>
{
    public const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly NotedbContext _context;

    public FolderDataPortServiceFactory(NotedbContext context)
        => _context = context;

    public bool IsContentTypeSupported(string contentType)
        => contentType is ExcelContentType or DocxContentType;

    public IImportService<Folder> GetImportService(string contentType, string userId)
        => contentType switch
        {
            ExcelContentType => new FolderImportService(_context, userId),
            DocxContentType  => new FolderDocxImportService(_context, userId),
            _ => throw new NotImplementedException(
                     $"Імпорт для типу «{contentType}» не реалізовано.")
        };

    public IExportService<Folder> GetExportService(string contentType, string userId)
        => contentType switch
        {
            ExcelContentType => new FolderExportService(_context, userId),
            DocxContentType  => new FolderDocxExportService(_context, userId),
            _ => throw new NotImplementedException(
                     $"Експорт для типу «{contentType}» не реалізовано.")
        };
}
