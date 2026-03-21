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

        // GET: Files
        public async Task<IActionResult> Index()
        {
            var notedbContext = _context.Files.Include(f => f.Folder);
            return View(await notedbContext.ToListAsync());
        }

        // GET: Files/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @file = await _context.Files
                .Include(f => f.Folder)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (@file == null)
            {
                return NotFound();
            }

            return View(@file);
        }

        // GET: Files/Create
        public IActionResult Create()
        {
            ViewData["Folderid"] = new SelectList(_context.Folders, "Id", "Name");
            return View();
        }

        // POST: Files/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Folderid,Createdat,Id")] File @file)
        {
            if (ModelState.IsValid)
            {
                _context.Add(@file);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["Folderid"] = new SelectList(_context.Folders, "Id", "Name", @file.Folderid);
            return View(@file);
        }

        // GET: Files/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @file = await _context.Files.FindAsync(id);
            if (@file == null)
            {
                return NotFound();
            }
            ViewData["Folderid"] = new SelectList(_context.Folders, "Id", "Name", @file.Folderid);
            return View(@file);
        }

        // POST: Files/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Name,Description,Folderid,Createdat,Id")] File @file)
        {
            if (id != @file.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(@file);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FileExists(@file.Id))
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
            ViewData["Folderid"] = new SelectList(_context.Folders, "Id", "Name", @file.Folderid);
            return View(@file);
        }

        // GET: Files/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @file = await _context.Files
                .Include(f => f.Folder)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (@file == null)
            {
                return NotFound();
            }

            return View(@file);
        }

        // POST: Files/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var @file = await _context.Files.FindAsync(id);
            if (@file != null)
            {
                _context.Files.Remove(@file);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool FileExists(int id)
        {
            return _context.Files.Any(e => e.Id == id);
        }
    }
}
