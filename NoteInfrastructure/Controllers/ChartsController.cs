using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using File = NoteDomain.Model.File;

namespace NoteInfrastructure.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ChartsController : ControllerBase
{
    private record FilesByMonthItem(string Month, int Count);
    private record FilesByFolderItem(string Folder, int Count);
    private record TagUsageItem(string Tag, int Count);
    private record VersionsByMonthItem(string Month, int Count);
    private record TopFileByVersionsItem(string FileName, int Versions);

    private readonly NotedbContext _context;

    public ChartsController(NotedbContext context) => _context = context;

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Користувач не авторизований.");

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

    [HttpGet("filesByMonth")]
    public async Task<JsonResult> GetFilesByMonthAsync(CancellationToken cancellationToken)
    {
        var userFolderIds = await GetUserFolderIdsAsync();

        var raw = await _context.Files
            .Where(f => f.Createdat != null && userFolderIds.Contains(f.Folderid))
            .GroupBy(f => new { f.Createdat!.Value.Year, f.Createdat.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var items = raw
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .Select(x => new FilesByMonthItem($"{x.Year}-{x.Month:D2}", x.Count))
            .ToList();

        return new JsonResult(items);
    }

    [HttpGet("filesByFolder")]
    public async Task<JsonResult> GetFilesByFolderAsync(CancellationToken cancellationToken)
    {
        var userFolderIds = await GetUserFolderIdsAsync();

        var raw = await _context.Files
            .Where(f => userFolderIds.Contains(f.Folderid))
            .Select(f => f.Folder != null ? f.Folder.Name : "Без папки")
            .ToListAsync(cancellationToken);

        var items = raw
            .GroupBy(name => name)
            .Select(g => new FilesByFolderItem(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        return new JsonResult(items);
    }

    [HttpGet("tagsByUsage")]
    public async Task<JsonResult> GetTagsByUsageAsync(CancellationToken cancellationToken)
    {
        var raw = await _context.Tags
            .Where(t => t.UserId == CurrentUserId)
            .Select(t => new { t.Name, Count = t.Files.Count })
            .ToListAsync(cancellationToken);

        var items = raw
            .OrderByDescending(x => x.Count)
            .Take(15)
            .Select(x => new TagUsageItem(x.Name, x.Count))
            .ToList();

        return new JsonResult(items);
    }

    [HttpGet("versionsByMonth")]
    public async Task<JsonResult> GetVersionsByMonthAsync(CancellationToken cancellationToken)
    {
        var userFolderIds = await GetUserFolderIdsAsync();

        var raw = await _context.Fileversions
            .Where(v => v.Createdat != null &&
                        v.File != null &&
                        userFolderIds.Contains(v.File.Folderid))
            .GroupBy(v => new { v.Createdat!.Value.Year, v.Createdat.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var items = raw
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .Select(x => new VersionsByMonthItem($"{x.Year}-{x.Month:D2}", x.Count))
            .ToList();

        return new JsonResult(items);
    }

    [HttpGet("topFilesByVersions")]
    public async Task<JsonResult> GetTopFilesByVersionsAsync(CancellationToken cancellationToken)
    {
        var userFolderIds = await GetUserFolderIdsAsync();

        var raw = await _context.Files
            .Where(f => userFolderIds.Contains(f.Folderid))
            .Select(f => new { f.Name, Count = f.Fileversions.Count })
            .ToListAsync(cancellationToken);

        var items = raw
            .OrderByDescending(x => x.Count)
            .Take(10)
            .Select(x => new TopFileByVersionsItem(x.Name, x.Count))
            .ToList();

        return new JsonResult(items);
    }
}
