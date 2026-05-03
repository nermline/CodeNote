using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteInfrastructure.Helpers;
using NoteInfrastructure.Models;

namespace NoteInfrastructure.Controllers;

public class SearchController : BaseUserController
{
    private readonly NotedbContext _context;
    private const    int SearchPageSize = 10;

    public SearchController(NotedbContext context) => _context = context;

    public async Task<IActionResult> Index(
        string? q,
        int filesPage   = 1,
        int foldersPage = 1,
        int commitsPage = 1,
        int tagsPage    = 1)
    {
        var vm = new SearchResultViewModel { Query = q ?? "" };

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();

            var rootFolderIds = await _context.Folders
                .Where(f => f.UserId == CurrentUserId)
                .Select(f => f.Id)
                .ToListAsync();

            var userFolderIds = await GetAllDescendantFolderIdsAsync(rootFolderIds);

            vm.Files = await PaginatedList<NoteDomain.Model.File>.CreateAsync(
                _context.Files
                    .Include(f => f.Folder)
                    .Include(f => f.Tags)
                    .Where(f => userFolderIds.Contains(f.Folderid) &&
                                f.Name.ToLower().Contains(term))
                    .OrderBy(f => f.Name),
                filesPage, SearchPageSize);

            vm.Folders = await PaginatedList<NoteDomain.Model.Folder>.CreateAsync(
                _context.Folders
                    .Include(f => f.Parentfolder)
                    .Where(f => userFolderIds.Contains(f.Id) &&
                                f.Name.ToLower().Contains(term))
                    .OrderBy(f => f.Name),
                foldersPage, SearchPageSize);

            vm.Commits = await PaginatedList<NoteDomain.Model.Fileversion>.CreateAsync(
                _context.Fileversions
                    .Include(fv => fv.File)
                    .Where(fv => fv.File != null &&
                                 userFolderIds.Contains(fv.File.Folderid) &&
                                 ((fv.Changelog != null && fv.Changelog.ToLower().Contains(term)) ||
                                  fv.Versionnumber.ToString().Contains(term) ||
                                  (fv.File != null && fv.File.Name.ToLower().Contains(term))))
                    .OrderByDescending(fv => fv.Createdat),
                commitsPage, SearchPageSize);

            vm.Tags = await PaginatedList<NoteDomain.Model.Tag>.CreateAsync(
                _context.Tags
                    .Include(t => t.Files)
                    .Where(t => t.UserId == CurrentUserId &&
                                t.Name.ToLower().Contains(term))
                    .OrderBy(t => t.Name),
                tagsPage, SearchPageSize);

            vm.TotalCount = vm.Files.TotalCount + vm.Folders.TotalCount
                          + vm.Commits.TotalCount + vm.Tags.TotalCount;
        }

        return View(vm);
    }

    private async Task<HashSet<int>> GetAllDescendantFolderIdsAsync(IEnumerable<int> rootIds)
    {
        var result     = new HashSet<int>(rootIds);
        var allFolders = await _context.Folders
            .Select(f => new { f.Id, f.Parentfolderid })
            .ToListAsync();
        var queue = new Queue<int>(rootIds);

        while (queue.Count > 0)
        {
            var pid = queue.Dequeue();
            foreach (var child in allFolders.Where(f => f.Parentfolderid == pid))
                if (result.Add(child.Id)) queue.Enqueue(child.Id);
        }
        return result;
    }
}
