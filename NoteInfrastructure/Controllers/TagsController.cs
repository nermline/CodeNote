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
    public class TagsController : Controller
    {
        private readonly NotedbContext _context;
        private const int PageSize = 20;

        public TagsController(NotedbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            ViewData["Search"] = search;

            var query = _context.Tags
                .Include(t => t.Files)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => t.Name.ToLower().Contains(search.ToLower()));

            query = query.OrderBy(t => t.Name);

            var result = await PaginatedList<Tag>.CreateAsync(query, page, PageSize, search);
            return View(result);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var tag = await _context.Tags
                .Include(t => t.Files)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tag == null) return NotFound();

            return View(tag);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Id")] Tag tag)
        {
            bool exists = _context.Tags.Any(t => t.Name == tag.Name);
            if (exists)
                ModelState.AddModelError("Name", "Тег з таким іменем вже існує.");

            if (ModelState.IsValid)
            {
                _context.Add(tag);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(tag);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var tag = await _context.Tags.FindAsync(id);
            if (tag == null) return NotFound();
            return View(tag);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Name,Id")] Tag tag)
        {
            if (id != tag.Id) return NotFound();

            bool exists = _context.Tags.Any(t => t.Name == tag.Name && t.Id != tag.Id);
            if (exists)
                ModelState.AddModelError("Name", "Інший тег з таким іменем вже існує.");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tag);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TagExists(tag.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(tag);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var tag = await _context.Tags
                .Include(t => t.Files)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tag == null) return NotFound();

            return View(tag);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tag = await _context.Tags.FindAsync(id);
            if (tag != null)
                _context.Tags.Remove(tag);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TagExists(int id) => _context.Tags.Any(e => e.Id == id);
    }
}
