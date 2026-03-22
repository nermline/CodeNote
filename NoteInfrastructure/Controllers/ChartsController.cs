using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public ChartsController(NotedbContext context)
    {
        _context = context;
    }

    // GET /api/charts/filesByMonth
    [HttpGet("filesByMonth")]
    public async Task<JsonResult> GetFilesByMonthAsync(CancellationToken cancellationToken)
    {
        var raw = await _context.Files
            .Where(f => f.Createdat != null)
            .GroupBy(f => new { f.Createdat!.Value.Year, f.Createdat.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var items = raw
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .Select(x => new FilesByMonthItem($"{x.Year}-{x.Month:D2}", x.Count))
            .ToList();

        return new JsonResult(items);
    }

    // GET /api/charts/filesByFolder
    [HttpGet("filesByFolder")]
    public async Task<JsonResult> GetFilesByFolderAsync(CancellationToken cancellationToken)
    {
        var raw = await _context.Files
            .Select(f => f.Folder != null ? f.Folder.Name : "Без папки")
            .ToListAsync(cancellationToken);

        var items = raw
            .GroupBy(name => name)
            .Select(g => new FilesByFolderItem(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        return new JsonResult(items);
    }

    // GET /api/charts/tagsByUsage
    [HttpGet("tagsByUsage")]
    public async Task<JsonResult> GetTagsByUsageAsync(CancellationToken cancellationToken)
    {
        var raw = await _context.Tags
            .Select(t => new { t.Name, Count = t.Files.Count })
            .ToListAsync(cancellationToken);

        var items = raw
            .OrderByDescending(x => x.Count)
            .Take(15)
            .Select(x => new TagUsageItem(x.Name, x.Count))
            .ToList();

        return new JsonResult(items);
    }

    // GET /api/charts/versionsByMonth
    [HttpGet("versionsByMonth")]
    public async Task<JsonResult> GetVersionsByMonthAsync(CancellationToken cancellationToken)
    {
        var raw = await _context.Fileversions
            .Where(v => v.Createdat != null)
            .GroupBy(v => new { v.Createdat!.Value.Year, v.Createdat.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var items = raw
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .Select(x => new VersionsByMonthItem($"{x.Year}-{x.Month:D2}", x.Count))
            .ToList();

        return new JsonResult(items);
    }

    // GET /api/charts/topFilesByVersions
    [HttpGet("topFilesByVersions")]
    public async Task<JsonResult> GetTopFilesByVersionsAsync(CancellationToken cancellationToken)
    {
        var raw = await _context.Files
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
