using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using NoteInfrastructure;
using System;
using System.Linq;
using System.Threading.Tasks;
using File = NoteDomain.Model.File;

namespace NoteInfrastructure.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IdeApiController : ControllerBase
    {
        private readonly NotedbContext _context;

        public IdeApiController(NotedbContext context)
        {
            _context = context;
        }

        // 1. Отримання дерева файлів та папок
        [HttpGet("tree")]
        public async Task<IActionResult> GetTree()
        {
            var folders = await _context.Folders
                .Select(f => new { f.Id, f.Name, f.Parentfolderid, Type = "folder" })
                .ToListAsync();

            var files = await _context.Files
                .Select(f => new { f.Id, f.Name, f.Folderid, Type = "file" })
                .ToListAsync();

            return Ok(new { folders, files });
        }

        // 2. Створення папки
        [HttpPost("folders")]
        public async Task<IActionResult> CreateFolder([FromBody] CreateFolderDto dto)
        {
            var folder = new Folder { Name = dto.Name, Parentfolderid = dto.ParentId, Createdat = DateTime.UtcNow };
            _context.Folders.Add(folder);
            await _context.SaveChangesAsync();
            return Ok(new { id = folder.Id, name = folder.Name });
        }

        // 3. Створення файлу (одразу з 1-ю версією)
        [HttpPost("files")]
        public async Task<IActionResult> CreateFile([FromBody] CreateFileDto dto)
        {
            var file = new File { Name = dto.Name, Folderid = dto.FolderId, Createdat = DateTime.UtcNow };
            _context.Files.Add(file);
            await _context.SaveChangesAsync();

            // Створюємо початкову версію згідно з логікою
            var version = new Fileversion
            {
                Fileid = file.Id,
                Content = "// Початковий код\n",
                Versionnumber = 1,
                Createdat = DateTime.UtcNow
            };
            _context.Fileversions.Add(version);
            await _context.SaveChangesAsync();

            return Ok(new { id = file.Id, name = file.Name, versionId = version.Id });
        }

        // 4. Отримання деталей файлу (для редактора)
        [HttpGet("files/{id}")]
        public async Task<IActionResult> GetFileDetails(int id)
        {
            var file = await _context.Files
                .Include(f => f.Fileversions)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (file == null) return NotFound();

            var latestVersion = file.Fileversions.OrderByDescending(v => v.Versionnumber).FirstOrDefault();

            return Ok(new
            {
                file.Id,
                file.Name,
                file.Description,
                Versions = file.Fileversions.OrderByDescending(v => v.Createdat).Select(v => new { v.Id, v.Versionnumber, v.Createdat }),
                CurrentContent = latestVersion?.Content,
                CurrentVersionId = latestVersion?.Id
            });
        }

        // 5. Збереження в поточну версію
        [HttpPut("versions/{id}")]
        public async Task<IActionResult> UpdateVersion(int id, [FromBody] UpdateVersionDto dto)
        {
            var version = await _context.Fileversions.FindAsync(id);
            if (version == null) return NotFound();

            version.Content = dto.Content;
            await _context.SaveChangesAsync();
            return Ok();
        }

        // 6. Створення нової версії
        [HttpPost("versions")]
        public async Task<IActionResult> CreateVersion([FromBody] CreateVersionDto dto)
        {
            var lastVersion = await _context.Fileversions
                .Where(v => v.Fileid == dto.FileId)
                .OrderByDescending(v => v.Versionnumber)
                .FirstOrDefaultAsync();

            int nextNumber = (lastVersion?.Versionnumber ?? 0) + 1;

            var newVersion = new Fileversion
            {
                Fileid = dto.FileId,
                Content = dto.Content,
                Versionnumber = nextNumber,
                Createdat = DateTime.UtcNow
            };

            _context.Fileversions.Add(newVersion);
            await _context.SaveChangesAsync();
            return Ok(new { id = newVersion.Id, versionNumber = newVersion.Versionnumber });
        }

        // 7. Перейменування
        [HttpPut("rename")]
        public async Task<IActionResult> Rename([FromBody] RenameDto dto)
        {
            if (dto.Type == "folder")
            {
                var folder = await _context.Folders.FindAsync(dto.Id);
                if (folder != null) { folder.Name = dto.Name; await _context.SaveChangesAsync(); }
            }
            else
            {
                var file = await _context.Files.FindAsync(dto.Id);
                if (file != null) { file.Name = dto.Name; await _context.SaveChangesAsync(); }
            }
            return Ok();
        }

        // DTOs (Data Transfer Objects) для прийняття JSON
        public class CreateFolderDto { public string Name { get; set; } public int? ParentId { get; set; } }
        public class CreateFileDto { public string Name { get; set; } public int FolderId { get; set; } }
        public class UpdateVersionDto { public string Content { get; set; } }
        public class CreateVersionDto { public int FileId { get; set; } public string Content { get; set; } }
        public class RenameDto { public int Id { get; set; } public string Type { get; set; } public string Name { get; set; } }
    }
}