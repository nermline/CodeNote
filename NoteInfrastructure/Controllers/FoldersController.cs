using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using NoteInfrastructure;

namespace NoteInfrastructure.Controllers
{
    public class FoldersController : Controller
    {
        private readonly NotedbContext _context;

        public FoldersController(NotedbContext context)
        {
            _context = context;
        }

        // GET: Folders
        public async Task<IActionResult> Index()
        {
            var rootFolders = _context.Folders
                .Include(f => f.Parentfolder)
                .Where(f => f.Parentfolderid == null);

            return View(await rootFolders.ToListAsync());
        }

        // GET: Folders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var folder = await _context.Folders
                .Include(f => f.Parentfolder)
                .Include(f => f.InverseParentfolder)
                .Include(f => f.Files)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (folder == null)
            {
                return NotFound();
            }

            return View(folder);
        }

        // GET: Folders/Create
        public IActionResult Create()
        {
            ViewData["Parentfolderid"] = new SelectList(_context.Folders, "Id", "Name");
            return View();
        }

        // POST: Folders/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Parentfolderid,Createdat,Id")] Folder folder)
        {
            if (ModelState.IsValid)
            {
                _context.Add(folder);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["Parentfolderid"] = new SelectList(_context.Folders, "Id", "Name", folder.Parentfolderid);
            return View(folder);
        }

        // GET: Folders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var folder = await _context.Folders.FindAsync(id);
            if (folder == null)
            {
                return NotFound();
            }
            ViewData["Parentfolderid"] = new SelectList(_context.Folders, "Id", "Name", folder.Parentfolderid);
            return View(folder);
        }

        // POST: Folders/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Name,Parentfolderid,Createdat,Id")] Folder folder)
        {
            if (id != folder.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(folder);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FolderExists(folder.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["Parentfolderid"] = new SelectList(_context.Folders, "Id", "Name", folder.Parentfolderid);
            return View(folder);
        }

        // GET: Folders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var folder = await _context.Folders
                .Include(f => f.Parentfolder)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (folder == null)
            {
                return NotFound();
            }

            return View(folder);
        }

        // POST: Folders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var folder = await _context.Folders.FindAsync(id);
            if (folder != null)
            {
                _context.Folders.Remove(folder);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool FolderExists(int id)
        {
            return _context.Folders.Any(e => e.Id == id);
        }
    }
}
