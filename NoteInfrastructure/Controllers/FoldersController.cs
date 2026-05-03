using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using NoteInfrastructure.Helpers;
using NoteInfrastructure.Services;

namespace NoteInfrastructure.Controllers;

public class FoldersController : BaseUserController
{
    private readonly NotedbContext                      _context;
    private readonly IDataPortServiceFactory<Folder>   _dataPortFactory;
    private const    int PageSize    = 24;
    private const    int SubPageSize = 20;
    private const    int FilePageSize = 15;

    public FoldersController(NotedbContext context)
    {
        _context         = context;
        _dataPortFactory = new FolderDataPortServiceFactory(context);
    }

    // ── Допоміжні ─────────────────────────────────────────────────────────

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

    /// <summary>
    /// Повертає Id кореневої папки для даної папки (рекурсивно вгору по дереву).
    /// </summary>
    private async Task<string?> GetRootUserIdAsync(Folder folder)
    {
        var current = folder;
        while (current.Parentfolderid != null)
        {
            var parent = await _context.Folders.FindAsync(current.Parentfolderid);
            if (parent == null) break;
            current = parent;
        }
        return current.UserId;
    }

    /// <summary>
    /// Перевіряє, що папка належить поточному користувачеві.
    /// </summary>
    private async Task<bool> FolderBelongsToCurrentUser(int folderId)
    {
        var folder = await _context.Folders.FindAsync(folderId);
        if (folder == null) return false;
        var rootUserId = await GetRootUserIdAsync(folder);
        return rootUserId == CurrentUserId;
    }

    /// <summary>
    /// SelectList папок поточного користувача.
    /// </summary>
    private IQueryable<Folder> UserFolders =>
        _context.Folders.Where(f =>
            f.UserId == CurrentUserId ||
            _context.Folders.Any(root =>
                root.UserId == CurrentUserId &&
                f.Parentfolderid == root.Id));

    // ── Index ──────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        ViewData["Search"] = search;

        var query = _context.Folders
            .Include(f => f.Parentfolder)
            .Where(f => f.Parentfolderid == null && f.UserId == CurrentUserId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(f => f.Name.ToLower().Contains(search.ToLower()));

        query = query.OrderBy(f => f.Name);

        var result = await PaginatedList<Folder>.CreateAsync(query, page, PageSize, search);
        return View(result);
    }

    // ── Details ────────────────────────────────────────────────────────────

    public async Task<IActionResult> Details(int? id, int subPage = 1, int filePage = 1)
    {
        if (id == null) return NotFound();

        if (!await FolderBelongsToCurrentUser(id.Value)) return Forbid();

        var folder = await _context.Folders
            .Include(f => f.Parentfolder)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (folder == null) return NotFound();

        await LoadFolderParentChain(folder);

        var subFolders = await PaginatedList<Folder>.CreateAsync(
            _context.Folders
                .Where(f => f.Parentfolderid == id)
                .OrderBy(f => f.Name),
            subPage, SubPageSize);

        var files = await PaginatedList<NoteDomain.Model.File>.CreateAsync(
            _context.Files
                .Where(f => f.Folderid == id)
                .OrderBy(f => f.Name),
            filePage, FilePageSize);

        ViewData["SubFolders"] = subFolders;
        ViewData["Files"]      = files;

        return View(folder);
    }

    // ── Create ─────────────────────────────────────────────────────────────

    public IActionResult Create(int? parentFolderId)
    {
        ViewData["Parentfolderid"] = new SelectList(
            _context.Folders.Where(f =>
                f.UserId == CurrentUserId ||
                _context.Folders.Any(root => root.UserId == CurrentUserId && f.Id == root.Id)),
            "Id", "Name", parentFolderId);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Parentfolderid,Createdat,Id")] Folder folder)
    {
        // Якщо це коренева папка — присвоюємо userId
        if (folder.Parentfolderid == null)
            folder.UserId = CurrentUserId;
        else if (!await FolderBelongsToCurrentUser(folder.Parentfolderid.Value))
            return Forbid();

        bool exists = _context.Folders.Any(f =>
            f.Name == folder.Name && f.Parentfolderid == folder.Parentfolderid);
        if (exists)
            ModelState.AddModelError("Name", "Папка з таким іменем вже існує в цьому каталозі.");

        if (ModelState.IsValid)
        {
            _context.Add(folder);
            await _context.SaveChangesAsync();
            return folder.Parentfolderid != null
                ? RedirectToAction("Details", new { id = folder.Parentfolderid })
                : RedirectToAction(nameof(Index));
        }

        ViewData["Parentfolderid"] = new SelectList(
            _context.Folders.Where(f => f.UserId == CurrentUserId),
            "Id", "Name", folder.Parentfolderid);
        return View(folder);
    }

    // ── Edit ───────────────────────────────────────────────────────────────

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        if (!await FolderBelongsToCurrentUser(id.Value)) return Forbid();

        var folder = await _context.Folders.FindAsync(id);
        if (folder == null) return NotFound();

        ViewData["Parentfolderid"] = new SelectList(
            _context.Folders.Where(f => f.UserId == CurrentUserId),
            "Id", "Name", folder.Parentfolderid);
        return View(folder);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Name,Parentfolderid,Createdat,Id")] Folder folder)
    {
        if (id != folder.Id) return NotFound();
        if (!await FolderBelongsToCurrentUser(id)) return Forbid();

        bool exists = _context.Folders.Any(f =>
            f.Name == folder.Name && f.Parentfolderid == folder.Parentfolderid && f.Id != folder.Id);
        if (exists)
            ModelState.AddModelError("Name", "Папка з таким іменем вже існує в цьому каталозі.");

        if (ModelState.IsValid)
        {
            try
            {
                // Зберігаємо UserId при редагуванні кореневої папки
                var existing = await _context.Folders.AsNoTracking().FirstAsync(f => f.Id == id);
                folder.UserId = existing.UserId;

                _context.Update(folder);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FolderExists(folder.Id)) return NotFound();
                else throw;
            }
            return folder.Parentfolderid != null
                ? RedirectToAction("Details", new { id = folder.Parentfolderid })
                : RedirectToAction(nameof(Index));
        }

        ViewData["Parentfolderid"] = new SelectList(
            _context.Folders.Where(f => f.UserId == CurrentUserId),
            "Id", "Name", folder.Parentfolderid);
        return View(folder);
    }

    // ── Delete ─────────────────────────────────────────────────────────────

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        if (!await FolderBelongsToCurrentUser(id.Value)) return Forbid();

        var folder = await _context.Folders
            .Include(f => f.Parentfolder)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (folder == null) return NotFound();
        return View(folder);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!await FolderBelongsToCurrentUser(id)) return Forbid();

        var folder = await _context.Folders.FindAsync(id);
        if (folder != null) _context.Folders.Remove(folder);

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // ── Import / Export ────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Import() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("file", "Будь ласка, оберіть файл (.xlsx або .docx).");
            return View();
        }

        try
        {
            var effectiveContentType = file.ContentType;
            if (!_dataPortFactory.IsContentTypeSupported(effectiveContentType))
            {
                effectiveContentType = Path.GetExtension(file.FileName).ToLowerInvariant() switch
                {
                    ".xlsx" => FolderDataPortServiceFactory.ExcelContentType,
                    ".docx" => FolderDataPortServiceFactory.DocxContentType,
                    _       => effectiveContentType,
                };
            }

            var importService = _dataPortFactory.GetImportService(effectiveContentType, CurrentUserId);
            using var stream  = file.OpenReadStream();
            await importService.ImportFromStreamAsync(stream, cancellationToken);
        }
        catch (NotImplementedException)
        {
            ModelState.AddModelError("file",
                $"Непідтримуваний формат «{Path.GetExtension(file.FileName)}». Оберіть .xlsx або .docx.");
            return View();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Помилка імпорту: {ex.InnerException?.Message ?? ex.Message}");
            return View();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Export(
        [FromQuery] string contentType = FolderDataPortServiceFactory.ExcelContentType,
        CancellationToken cancellationToken = default)
    {
        var exportService = _dataPortFactory.GetExportService(contentType, CurrentUserId);

        var ms = new MemoryStream();
        await exportService.WriteToAsync(ms, cancellationToken);
        await ms.FlushAsync(cancellationToken);
        ms.Position = 0;

        var ext      = contentType == FolderDataPortServiceFactory.DocxContentType ? "docx" : "xlsx";
        var fileName = $"folders_{DateTime.UtcNow:yyyy-MM-dd}.{ext}";

        return new FileStreamResult(ms, contentType) { FileDownloadName = fileName };
    }

    private bool FolderExists(int id) => _context.Folders.Any(e => e.Id == id);
}
