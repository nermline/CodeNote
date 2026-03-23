using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteInfrastructure.Helpers;
using NoteInfrastructure.Models;

namespace NoteInfrastructure.Controllers
{
    public class SearchController : Controller
    {
        private readonly NotedbContext _context;
        private const int SearchPageSize = 10;

        public SearchController(NotedbContext context)
        {
            _context = context;
        }

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

                vm.Files = await PaginatedList<NoteDomain.Model.File>.CreateAsync(
                    _context.Files
                        .Include(f => f.Folder)
                        .Include(f => f.Tags)
                        .Where(f => f.Name.ToLower().Contains(term))
                        .OrderBy(f => f.Name),
                    filesPage, SearchPageSize);

                vm.Folders = await PaginatedList<NoteDomain.Model.Folder>.CreateAsync(
                    _context.Folders
                        .Include(f => f.Parentfolder)
                        .Where(f => f.Name.ToLower().Contains(term))
                        .OrderBy(f => f.Name),
                    foldersPage, SearchPageSize);

                vm.Commits = await PaginatedList<NoteDomain.Model.Fileversion>.CreateAsync(
                    _context.Fileversions
                        .Include(fv => fv.File)
                        .Where(fv =>
                            (fv.Changelog != null && fv.Changelog.ToLower().Contains(term)) ||
                            fv.Versionnumber.ToString().Contains(term) ||
                            (fv.File != null && fv.File.Name.ToLower().Contains(term)))
                        .OrderByDescending(fv => fv.Createdat),
                    commitsPage, SearchPageSize);

                vm.Tags = await PaginatedList<NoteDomain.Model.Tag>.CreateAsync(
                    _context.Tags
                        .Include(t => t.Files)
                        .Where(t => t.Name.ToLower().Contains(term))
                        .OrderBy(t => t.Name),
                    tagsPage, SearchPageSize);

                vm.TotalCount = vm.Files.TotalCount + vm.Folders.TotalCount
                              + vm.Commits.TotalCount + vm.Tags.TotalCount;
            }

            return View(vm);
        }
    }
}
