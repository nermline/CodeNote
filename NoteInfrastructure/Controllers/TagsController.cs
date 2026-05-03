using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using NoteInfrastructure.Helpers;

namespace NoteInfrastructure.Controllers;

public class TagsController : BaseUserController
{
    private readonly NotedbContext _context;
    private const    int PageSize = 20;

    public TagsController(NotedbContext context) => _context = context;

    private IQueryable<Tag> UserTags =>
        _context.Tags.Where(t => t.UserId == CurrentUserId);

    // ── Index ──────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        ViewData["Search"] = search;

        var query = UserTags.Include(t => t.Files).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Name.ToLower().Contains(search.ToLower()));

        query = query.OrderBy(t => t.Name);

        var result = await PaginatedList<Tag>.CreateAsync(query, page, PageSize, search);
        return View(result);
    }

    // ── Details ────────────────────────────────────────────────────────────

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var tag = await UserTags
            .Include(t => t.Files)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (tag == null) return NotFound();
        return View(tag);
    }

    // ── Create ─────────────────────────────────────────────────────────────

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Id")] Tag tag)
    {
        bool exists = UserTags.Any(t => t.Name == tag.Name);
        if (exists)
            ModelState.AddModelError("Name", "Тег з таким іменем вже існує.");

        if (ModelState.IsValid)
        {
            tag.UserId = CurrentUserId;
            _context.Add(tag);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(tag);
    }

    // ── Edit ───────────────────────────────────────────────────────────────

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var tag = await UserTags.FirstOrDefaultAsync(t => t.Id == id);
        if (tag == null) return NotFound();
        return View(tag);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Name,Id")] Tag tag)
    {
        if (id != tag.Id) return NotFound();

        // Перевірка що тег належить поточному користувачу
        if (!await UserTags.AnyAsync(t => t.Id == id)) return Forbid();

        bool exists = UserTags.Any(t => t.Name == tag.Name && t.Id != tag.Id);
        if (exists)
            ModelState.AddModelError("Name", "Інший тег з таким іменем вже існує.");

        if (ModelState.IsValid)
        {
            try
            {
                // Зберігаємо UserId при оновленні
                var existing = await UserTags.AsNoTracking().FirstAsync(t => t.Id == id);
                tag.UserId = existing.UserId;

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

    // ── Delete ─────────────────────────────────────────────────────────────

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var tag = await UserTags
            .Include(t => t.Files)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (tag == null) return NotFound();
        return View(tag);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var tag = await UserTags.FirstOrDefaultAsync(t => t.Id == id);
        if (tag == null) return Forbid();

        _context.Tags.Remove(tag);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool TagExists(int id) => UserTags.Any(e => e.Id == id);
}
