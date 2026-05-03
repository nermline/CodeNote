using NoteDomain.Model;

namespace NoteInfrastructure.Services;

public interface IDataPortServiceFactory<TEntity>
    where TEntity : Entity
{
    IImportService<TEntity> GetImportService(string contentType, string userId);
    IExportService<TEntity> GetExportService(string contentType, string userId);
    bool IsContentTypeSupported(string contentType);
}
