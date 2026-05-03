using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using NoteInfrastructure.Helpers;
using File   = NoteDomain.Model.File;
using Folder = NoteDomain.Model.Folder;

namespace NoteInfrastructure.Controllers;

public class FilesController : BaseUserController
{
    private readonly NotedbContext _context;
    private const    int PageSize = 15;

    public FilesController(NotedbContext context) => _context = context;

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

    private async Task<string?> GetRootUserIdAsync(int folderId)
    {
        var current = await _context.Folders.FindAsync(folderId);
        while (current?.Parentfolderid != null)
            current = await _context.Folders.FindAsync(current.Parentfolderid);
        return current?.UserId;
    }

    private async Task<bool> FileBelongsToCurrentUser(int fileId)
    {
        var file = await _context.Files.FindAsync(fileId);
        if (file == null) return false;
        return await GetRootUserIdAsync(file.Folderid) == CurrentUserId;
    }

    private IQueryable<File> UserFiles =>
        _context.Files
            .Where(f => _context.Folders
                .Any(root => root.UserId == CurrentUserId &&
                             _context.Folders.Any(anc =>
                                 anc.Id == f.Folderid && (
                                     anc.Id == root.Id ||
                                     anc.Parentfolderid == root.Id ||
                                     _context.Folders.Any(p2 =>
                                         p2.Id == anc.Parentfolderid &&
                                         (p2.Id == root.Id ||
                                          p2.Parentfolderid == root.Id))))));

    private IQueryable<File> UserFilesSimple =>
        _context.Files
            .Join(_context.Folders,
                  f    => f.Folderid,
                  fold => fold.Id,
                  (f, fold) => new { File = f, Folder = fold })
            .Where(x => x.Folder.UserId == CurrentUserId ||
                        _context.Folders.Any(root =>
                            root.UserId == CurrentUserId &&
                            x.Folder.Parentfolderid == root.Id))
            .Select(x => x.File);

    private IQueryable<Folder> UserFolders =>
        _context.Folders.Where(f =>
            f.UserId == CurrentUserId ||
            _context.Folders.Any(root =>
                root.UserId == CurrentUserId &&
                (f.Parentfolderid == root.Id ||
                 _context.Folders.Any(p2 =>
                     p2.Id == f.Parentfolderid && p2.Parentfolderid == root.Id))));

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        ViewData["Search"] = search;

        var userFolderIds = await _context.Folders
            .Where(f => f.UserId == CurrentUserId)
            .Select(f => f.Id)
            .ToListAsync();

        var allFolderIds = await GetAllDescendantFolderIdsAsync(userFolderIds);

        var query = _context.Files
            .Include(f => f.Folder)
            .Include(f => f.Tags)
            .Where(f => allFolderIds.Contains(f.Folderid));

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(f => f.Name.ToLower().Contains(search.ToLower()));

        query = query.OrderBy(f => f.Name);

        var result = await PaginatedList<File>.CreateAsync(query, page, PageSize, search);
        return View(result);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        if (!await FileBelongsToCurrentUser(id.Value)) return Forbid();

        var file = await _context.Files
            .Include(f => f.Folder)
            .Include(f => f.Tags)
            .Include(f => f.Fileversions)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (file == null) return NotFound();
        await LoadFolderParentChain(file.Folder);
        return View(file);
    }

    public async Task<IActionResult> Create(int? folderId)
    {
        var allFolderIds = await GetAllDescendantFolderIdsAsync(
            await _context.Folders.Where(f => f.UserId == CurrentUserId)
                .Select(f => f.Id).ToListAsync());

        ViewData["Folderid"] = new SelectList(
            _context.Folders.Where(f => allFolderIds.Contains(f.Id)),
            "Id", "Name", folderId);

        ViewData["Tags"] = new MultiSelectList(
            _context.Tags.Where(t => t.UserId == CurrentUserId),
            "Id", "Name");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Name,Description,Folderid,Createdat,Id")] File file,
        int[] selectedTags)
    {

        if (await GetRootUserIdAsync(file.Folderid) != CurrentUserId) return Forbid();

        bool exists = _context.Files.Any(f => f.Name == file.Name && f.Folderid == file.Folderid);
        if (exists)
            ModelState.AddModelError("Name", "Файл з таким іменем вже існує в цій папці.");

        if (ModelState.IsValid)
        {
            if (selectedTags?.Length > 0)
                file.Tags = await _context.Tags
                    .Where(t => selectedTags.Contains(t.Id) && t.UserId == CurrentUserId)
                    .ToListAsync();

            _context.Add(file);
            await _context.SaveChangesAsync();
            return RedirectToAction("Details", "Folders", new { id = file.Folderid });
        }

        var allFolderIds = await GetAllDescendantFolderIdsAsync(
            await _context.Folders.Where(f => f.UserId == CurrentUserId).Select(f => f.Id).ToListAsync());

        ViewData["Folderid"] = new SelectList(
            _context.Folders.Where(f => allFolderIds.Contains(f.Id)), "Id", "Name", file.Folderid);
        ViewData["Tags"] = new MultiSelectList(
            _context.Tags.Where(t => t.UserId == CurrentUserId), "Id", "Name", selectedTags);
        return View(file);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        if (!await FileBelongsToCurrentUser(id.Value)) return Forbid();

        var file = await _context.Files
            .Include(f => f.Folder)
            .Include(f => f.Tags)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (file == null) return NotFound();
        await LoadFolderParentChain(file.Folder);

        var allFolderIds = await GetAllDescendantFolderIdsAsync(
            await _context.Folders.Where(f => f.UserId == CurrentUserId).Select(f => f.Id).ToListAsync());

        var selectedTagIds = file.Tags.Select(t => t.Id).ToArray();
        ViewData["Tags"] = new MultiSelectList(
            _context.Tags.Where(t => t.UserId == CurrentUserId), "Id", "Name", selectedTagIds);
        ViewData["Folderid"] = new SelectList(
            _context.Folders.Where(f => allFolderIds.Contains(f.Id)), "Id", "Name", file.Folderid);

        return View(file);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("Name,Description,Folderid,Createdat,Id")] File file,
        int[] selectedTags)
    {
        if (id != file.Id) return NotFound();
        if (!await FileBelongsToCurrentUser(id)) return Forbid();

        bool exists = _context.Files.Any(f =>
            f.Name == file.Name && f.Folderid == file.Folderid && f.Id != file.Id);
        if (exists)
            ModelState.AddModelError("Name", "Файл з таким іменем вже існує в цій папці.");

        if (ModelState.IsValid)
        {
            try
            {
                var fileToUpdate = await _context.Files.Include(f => f.Tags)
                    .FirstOrDefaultAsync(f => f.Id == id);

                if (fileToUpdate == null) return NotFound();

                fileToUpdate.Name        = file.Name;
                fileToUpdate.Description = file.Description;
                fileToUpdate.Folderid    = file.Folderid;

                fileToUpdate.Tags.Clear();
                if (selectedTags?.Length > 0)
                {
                    var newTags = await _context.Tags
                        .Where(t => selectedTags.Contains(t.Id) && t.UserId == CurrentUserId)
                        .ToListAsync();
                    foreach (var tag in newTags) fileToUpdate.Tags.Add(tag);
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

        var allFolderIds2 = await GetAllDescendantFolderIdsAsync(
            await _context.Folders.Where(f => f.UserId == CurrentUserId).Select(f => f.Id).ToListAsync());

        var folderForView = await _context.Folders.FindAsync(file.Folderid);
        await LoadFolderParentChain(folderForView);
        file.Folder = folderForView;

        ViewData["Tags"] = new MultiSelectList(
            _context.Tags.Where(t => t.UserId == CurrentUserId), "Id", "Name", selectedTags);
        ViewData["Folderid"] = new SelectList(
            _context.Folders.Where(f => allFolderIds2.Contains(f.Id)), "Id", "Name", file.Folderid);
        return View(file);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        if (!await FileBelongsToCurrentUser(id.Value)) return Forbid();

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
        if (!await FileBelongsToCurrentUser(id)) return Forbid();

        var file = await _context.Files.FindAsync(id);
        if (file != null) _context.Files.Remove(file);

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool FileExists(int id) => _context.Files.Any(e => e.Id == id);

    private async Task<HashSet<int>> GetAllDescendantFolderIdsAsync(IEnumerable<int> rootIds)
    {
        var result  = new HashSet<int>(rootIds);
        var allFolders = await _context.Folders.Select(f => new { f.Id, f.Parentfolderid }).ToListAsync();
        var queue   = new Queue<int>(rootIds);

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            foreach (var child in allFolders.Where(f => f.Parentfolderid == parentId))
            {
                if (result.Add(child.Id))
                    queue.Enqueue(child.Id);
            }
        }

        return result;
    }
}
