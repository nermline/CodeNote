using Microsoft.EntityFrameworkCore;

namespace NoteInfrastructure.Helpers
{
    public class PaginatedList<T> : List<T>
    {
        public int PageIndex { get; }
        public int TotalPages { get; }
        public int TotalCount { get; }
        public int PageSize { get; }
        public string? SearchQuery { get; }

        public PaginatedList(List<T> items, int count, int pageIndex, int pageSize, string? searchQuery = null)
        {
            PageIndex = pageIndex;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);
            TotalCount = count;
            PageSize = pageSize;
            SearchQuery = searchQuery;
            AddRange(items);
        }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public static async Task<PaginatedList<T>> CreateAsync(
            IQueryable<T> source, int pageIndex, int pageSize, string? searchQuery = null)
        {
            var count = await source.CountAsync();
            var items = await source
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return new PaginatedList<T>(items, count, pageIndex, pageSize, searchQuery);
        }

        public IEnumerable<int> PageNumbers()
        {
            int start = Math.Max(1, PageIndex - 2);
            int end = Math.Min(TotalPages, PageIndex + 2);
            for (int i = start; i <= end; i++)
                yield return i;
        }
    }
}
