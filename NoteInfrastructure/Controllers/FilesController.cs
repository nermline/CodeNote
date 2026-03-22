using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using NoteInfrastructure;

using File = NoteDomain.Model.File;
using Folder = NoteDomain.Model.Folder;

namespace NoteInfrastructure.Controllers
{
    public class FilesController : Controller
    {
        private readonly NotedbContext _context;

        public FilesController(NotedbContext context)
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

        public async Task<IActionResult> Index()
        {
            var notedbContext = _context.Files
                .Include(f => f.Folder)
                .Include(f => f.Tags);
            return View(await notedbContext.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var file = await _context.Files
                .Include(f => f.Folder)
                .Include(f => f.Tags)
                .Include(f => f.Fileversions)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (file == null) return NotFound();

            await LoadFolderParentChain(file.Folder);

            return View(file);
        }

        public IActionResult Create(int? folderId)
        {
            ViewData["Folderid"] = new SelectList(_context.Folders, "Id", "Name", folderId);
            ViewData["Tags"] = new MultiSelectList(_context.Tags, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Folderid,Createdat,Id")] File file, int[] selectedTags)
        {
            bool exists = _context.Files.Any(f => f.Name == file.Name && f.Folderid == file.Folderid);
            if (exists)
                ModelState.AddModelError("Name", "Файл з таким іменем вже існує в цій папці.");

            if (ModelState.IsValid)
            {
                if (selectedTags != null && selectedTags.Length > 0)
                    file.Tags = await _context.Tags.Where(t => selectedTags.Contains(t.Id)).ToListAsync();

                _context.Add(file);
                await _context.SaveChangesAsync();
                return RedirectToAction("Details", "Folders", new { id = file.Folderid });
            }

            ViewData["Folderid"] = new SelectList(_context.Folders, "Id", "Name", file.Folderid);
            ViewData["Tags"] = new MultiSelectList(_context.Tags, "Id", "Name", selectedTags);
            return View(file);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var file = await _context.Files
                .Include(f => f.Folder)
                .Include(f => f.Tags)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (file == null) return NotFound();

            await LoadFolderParentChain(file.Folder);

            var selectedTagIds = file.Tags.Select(t => t.Id).ToArray();
            ViewData["Tags"] = new MultiSelectList(_context.Tags, "Id", "Name", selectedTagIds);

            return View(file);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Name,Description,Folderid,Createdat,Id")] File file, int[] selectedTags)
        {
            if (id != file.Id) return NotFound();

            bool exists = _context.Files.Any(f => f.Name == file.Name && f.Folderid == file.Folderid && f.Id != file.Id);
            if (exists)
                ModelState.AddModelError("Name", "Файл з таким іменем вже існує в цій папці.");

            if (ModelState.IsValid)
            {
                try
                {
                    var fileToUpdate = await _context.Files
                        .Include(f => f.Tags)
                        .FirstOrDefaultAsync(f => f.Id == id);

                    if (fileToUpdate == null) return NotFound();

                    fileToUpdate.Name = file.Name;
                    fileToUpdate.Description = file.Description;
                    fileToUpdate.Folderid = file.Folderid;

                    fileToUpdate.Tags.Clear();
                    if (selectedTags != null && selectedTags.Length > 0)
                    {
                        var newTags = await _context.Tags.Where(t => selectedTags.Contains(t.Id)).ToListAsync();
                        foreach (var tag in newTags)
                            fileToUpdate.Tags.Add(tag);
                    }

                    _context.Update(fileToUpdate);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FileExists(file.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction("Details", "Folders", new { id = file.Folderid });
            }

            var folderForView = await _context.Folders.FindAsync(file.Folderid);
            await LoadFolderParentChain(folderForView);
            file.Folder = folderForView;

            ViewData["Tags"] = new MultiSelectList(_context.Tags, "Id", "Name", selectedTags);
            return View(file);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var file = await _context.Files
                .Include(f => f.Folder)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (file == null) return NotFound();

            return View(file);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var file = await _context.Files.FindAsync(id);
            if (file != null)
                _context.Files.Remove(file);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool FileExists(int id) => _context.Files.Any(e => e.Id == id);
    }
}
