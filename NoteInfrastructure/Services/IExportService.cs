using NoteDomain.Model;

namespace NoteInfrastructure.Services
{
    public interface IExportService<TEntity>
        where TEntity : Entity
    {
        Task WriteToAsync(Stream stream, CancellationToken cancellationToken);
    }
}
