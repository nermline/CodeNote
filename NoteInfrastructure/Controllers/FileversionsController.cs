using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using NoteInfrastructure;
using NoteInfrastructure.Helpers;

namespace NoteInfrastructure.Controllers
{
    public class FileversionsController : Controller
    {
        private readonly NotedbContext _context;
        private const int PageSize = 15;

        public FileversionsController(NotedbContext context)
        {
            _context = context;
        }

        private async Task LoadFolderParentChain(Folder? folder)
        {
            if (folder == null) return;
            var current = folder;
            while (current.Parentfolderid != null)
            {
                current.Parentfolder = await _context.Folders.FindAsync(current.Parentfolderid);
                if (current.Parentfolder == null) break;
                current = current.Parentfolder;
            }
        }

        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            ViewData["Search"] = search;

            var query = _context.Fileversions
                .Include(f => f.File)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(fv =>
                    (fv.Versionnumber != null && fv.Versionnumber.ToLower().Contains(search.ToLower())) ||
                    (fv.Changelog != null && fv.Changelog.ToLower().Contains(search.ToLower())) ||
                    (fv.File != null && fv.File.Name.ToLower().Contains(search.ToLower())));

            query = query.OrderByDescending(f => f.Createdat);

            var result = await PaginatedList<Fileversion>.CreateAsync(query, page, PageSize, search);
            return View(result);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var fileversion = await _context.Fileversions
                .Include(f => f.File)
                    .ThenInclude(f => f.Folder)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (fileversion == null) return NotFound();

            if (fileversion.File?.Folder != null)
                await LoadFolderParentChain(fileversion.File.Folder);

            return View(fileversion);
        }

        public IActionResult Create(int? fileId)
        {
            ViewData["Fileid"] = new SelectList(_context.Files, "Id", "Name", fileId);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Fileid,Content,Versionnumber,Changelog,Createdat,Id")] Fileversion fileversion)
        {
            if (ModelState.IsValid)
            {
                _context.Add(fileversion);
                await _context.SaveChangesAsync();
                return RedirectToAction("Details", "Files", new { id = fileversion.Fileid });
            }
            ViewData["Fileid"] = new SelectList(_context.Files, "Id", "Name", fileversion.Fileid);
            return View(fileversion);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var fileversion = await _context.Fileversions
                .Include(f => f.File)
                    .ThenInclude(f => f.Folder)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (fileversion == null) return NotFound();

            if (fileversion.File?.Folder != null)
                await LoadFolderParentChain(fileversion.File.Folder);

            ViewData["Fileid"] = new SelectList(_context.Files, "Id", "Name", fileversion.Fileid);
            return View(fileversion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Fileid,Content,Versionnumber,Changelog,Createdat,Id")] Fileversion fileversion)
        {
            if (id != fileversion.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(fileversion);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FileversionExists(fileversion.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction("Details", "Files", new { id = fileversion.Fileid });
            }
            ViewData["Fileid"] = new SelectList(_context.Files, "Id", "Name", fileversion.Fileid);
            return View(fileversion);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var fileversion = await _context.Fileversions
                .Include(f => f.File)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (fileversion == null) return NotFound();

            return View(fileversion);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var fileversion = await _context.Fileversions.FindAsync(id);
            if (fileversion != null)
                _context.Fileversions.Remove(fileversion);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool FileversionExists(int id) => _context.Fileversions.Any(e => e.Id == id);
    }
}
