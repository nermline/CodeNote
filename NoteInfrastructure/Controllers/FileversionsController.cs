using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using NoteInfrastructure.Helpers;

namespace NoteInfrastructure.Controllers;

public class FileversionsController : BaseUserController
{
    private readonly NotedbContext _context;
    private const    int PageSize = 15;

    public FileversionsController(NotedbContext context) => _context = context;

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

    private async Task<string?> GetRootUserIdForFolder(int folderId)
    {
        var current = await _context.Folders.FindAsync(folderId);
        while (current?.Parentfolderid != null)
            current = await _context.Folders.FindAsync(current.Parentfolderid);
        return current?.UserId;
    }

    private async Task<bool> VersionBelongsToCurrentUser(int versionId)
    {
        var v = await _context.Fileversions.Include(fv => fv.File).FirstOrDefaultAsync(fv => fv.Id == versionId);
        if (v?.File == null) return false;
        return await GetRootUserIdForFolder(v.File.Folderid) == CurrentUserId;
    }

    private async Task<bool> FileBelongsToCurrentUser(int fileId)
    {
        var file = await _context.Files.FindAsync(fileId);
        if (file == null) return false;
        return await GetRootUserIdForFolder(file.Folderid) == CurrentUserId;
    }

    private async Task<HashSet<int>> GetUserFolderIdsAsync()
    {
        var rootIds = await _context.Folders
            .Where(f => f.UserId == CurrentUserId)
            .Select(f => f.Id)
            .ToListAsync();

        var result    = new HashSet<int>(rootIds);
        var allFolders = await _context.Folders.Select(f => new { f.Id, f.Parentfolderid }).ToListAsync();
        var queue     = new Queue<int>(rootIds);

        while (queue.Count > 0)
        {
            var pid = queue.Dequeue();
            foreach (var child in allFolders.Where(f => f.Parentfolderid == pid))
                if (result.Add(child.Id)) queue.Enqueue(child.Id);
        }
        return result;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        ViewData["Search"] = search;

        var userFolderIds = await GetUserFolderIdsAsync();

        var query = _context.Fileversions
            .Include(fv => fv.File)
            .Where(fv => fv.File != null && userFolderIds.Contains(fv.File.Folderid));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(fv =>
                fv.Versionnumber.ToString().Contains(term) ||
                (fv.Changelog != null && fv.Changelog.ToLower().Contains(term)) ||
                (fv.File      != null && fv.File.Name.ToLower().Contains(term)));
        }

        query = query.OrderByDescending(f => f.Createdat);

        var result = await PaginatedList<Fileversion>.CreateAsync(query, page, PageSize, search);
        return View(result);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        if (!await VersionBelongsToCurrentUser(id.Value)) return Forbid();

        var fileversion = await _context.Fileversions
            .Include(f => f.File).ThenInclude(f => f.Folder)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (fileversion == null) return NotFound();
        if (fileversion.File?.Folder != null) await LoadFolderParentChain(fileversion.File.Folder);
        return View(fileversion);
    }

    public async Task<IActionResult> Create(int? fileId)
    {
        var userFolderIds = await GetUserFolderIdsAsync();
        ViewData["Fileid"] = new SelectList(
            _context.Files.Where(f => userFolderIds.Contains(f.Folderid)),
            "Id", "Name", fileId);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Fileid,Content,Versionnumber,Changelog,Createdat,Id")] Fileversion fileversion)
    {
        if (!await FileBelongsToCurrentUser(fileversion.Fileid)) return Forbid();

        if (ModelState.IsValid)
        {
            _context.Add(fileversion);
            await _context.SaveChangesAsync();
            return RedirectToAction("Details", "Files", new { id = fileversion.Fileid });
        }

        var userFolderIds = await GetUserFolderIdsAsync();
        ViewData["Fileid"] = new SelectList(
            _context.Files.Where(f => userFolderIds.Contains(f.Folderid)),
            "Id", "Name", fileversion.Fileid);
        return View(fileversion);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        if (!await VersionBelongsToCurrentUser(id.Value)) return Forbid();

        var fileversion = await _context.Fileversions
            .Include(f => f.File).ThenInclude(f => f.Folder)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (fileversion == null) return NotFound();
        if (fileversion.File?.Folder != null) await LoadFolderParentChain(fileversion.File.Folder);

        var userFolderIds = await GetUserFolderIdsAsync();
        ViewData["Fileid"] = new SelectList(
            _context.Files.Where(f => userFolderIds.Contains(f.Folderid)),
            "Id", "Name", fileversion.Fileid);
        return View(fileversion);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("Fileid,Content,Versionnumber,Changelog,Createdat,Id")] Fileversion fileversion)
    {
        if (id != fileversion.Id) return NotFound();
        if (!await VersionBelongsToCurrentUser(id)) return Forbid();

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

        var userFolderIds = await GetUserFolderIdsAsync();
        ViewData["Fileid"] = new SelectList(
            _context.Files.Where(f => userFolderIds.Contains(f.Folderid)),
            "Id", "Name", fileversion.Fileid);
        return View(fileversion);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        if (!await VersionBelongsToCurrentUser(id.Value)) return Forbid();

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
        if (!await VersionBelongsToCurrentUser(id)) return Forbid();

        var fileversion = await _context.Fileversions.FindAsync(id);
        if (fileversion != null) _context.Fileversions.Remove(fileversion);

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool FileversionExists(int id) => _context.Fileversions.Any(e => e.Id == id);
}
