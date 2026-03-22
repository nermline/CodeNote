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
    public class FileversionsController : Controller
    {
        private readonly NotedbContext _context;

        public FileversionsController(NotedbContext context)
        {
            _context = context;
        }

        // GET: Fileversions
        public async Task<IActionResult> Index()
        {
            var notedbContext = _context.Fileversions.Include(f => f.File);
            return View(await notedbContext.ToListAsync());
        }

        // GET: Fileversions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var fileversion = await _context.Fileversions
                .Include(f => f.File)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (fileversion == null)
            {
                return NotFound();
            }

            return View(fileversion);
        }

        // GET: Fileversions/Create
        public IActionResult Create(int? fileId)
        {
            ViewData["Fileid"] = new SelectList(_context.Files, "Id", "Name", fileId);
            return View();
        }

        // POST: Fileversions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
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

        // GET: Fileversions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var fileversion = await _context.Fileversions.FindAsync(id);
            if (fileversion == null)
            {
                return NotFound();
            }
            ViewData["Fileid"] = new SelectList(_context.Files, "Id", "Name", fileversion.Fileid);
            return View(fileversion);
        }

        // POST: Fileversions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Fileid,Content,Versionnumber,Changelog,Createdat,Id")] Fileversion fileversion)
        {
            if (id != fileversion.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(fileversion);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FileversionExists(fileversion.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction("Details", "Files", new { id = fileversion.Fileid });
            }
            ViewData["Fileid"] = new SelectList(_context.Files, "Id", "Name", fileversion.Fileid);
            return View(fileversion);
        }

        // GET: Fileversions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var fileversion = await _context.Fileversions
                .Include(f => f.File)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (fileversion == null)
            {
                return NotFound();
            }

            return View(fileversion);
        }

        // POST: Fileversions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var fileversion = await _context.Fileversions.FindAsync(id);
            if (fileversion != null)
            {
                _context.Fileversions.Remove(fileversion);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool FileversionExists(int id)
        {
            return _context.Fileversions.Any(e => e.Id == id);
        }
    }
}
