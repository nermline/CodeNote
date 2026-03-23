using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteInfrastructure.Models;

namespace NoteInfrastructure.Controllers
{
    public class SearchController : Controller
    {
        private readonly NotedbContext _context;
        private const int MaxResultsPerCategory = 20;

        public SearchController(NotedbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? q)
        {
            var vm = new SearchResultViewModel { Query = q ?? "" };

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();

                vm.Files = await _context.Files
                    .Include(f => f.Folder)
                    .Include(f => f.Tags)
                    .Where(f => f.Name.ToLower().Contains(term))
                    .OrderBy(f => f.Name)
                    .Take(MaxResultsPerCategory)
                    .ToListAsync();

                vm.Folders = await _context.Folders
                    .Include(f => f.Parentfolder)
                    .Where(f => f.Name.ToLower().Contains(term))
                    .OrderBy(f => f.Name)
                    .Take(MaxResultsPerCategory)
                    .ToListAsync();

                vm.Commits = await _context.Fileversions
                    .Include(fv => fv.File)
                    .Where(fv =>
                        (fv.Changelog != null && fv.Changelog.ToLower().Contains(term)) ||
                        (fv.Versionnumber != null && fv.Versionnumber.ToLower().Contains(term)) ||
                        (fv.File != null && fv.File.Name.ToLower().Contains(term)))
                    .OrderByDescending(fv => fv.Createdat)
                    .Take(MaxResultsPerCategory)
                    .ToListAsync();

                vm.Tags = await _context.Tags
                    .Include(t => t.Files)
                    .Where(t => t.Name.ToLower().Contains(term))
                    .OrderBy(t => t.Name)
                    .Take(MaxResultsPerCategory)
                    .ToListAsync();
            }

            return View(vm);
        }
    }
}
