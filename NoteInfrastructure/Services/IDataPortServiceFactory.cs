using NoteDomain.Model;

namespace NoteInfrastructure.Services
{
    public interface IDataPortServiceFactory<TEntity>
    where TEntity : Entity
    {
        IImportService<TEntity> GetImportService(string contentType);
        IExportService<TEntity> GetExportService(string contentType);
        bool IsContentTypeSupported(string contentType);
    }
}
