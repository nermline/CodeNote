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
using NoteInfrastructure.Services;

namespace NoteInfrastructure.Controllers
{
    public class FoldersController : Controller
    {
        private readonly NotedbContext _context;
        private readonly IDataPortServiceFactory<Folder> _dataPortFactory;
        private const int PageSize = 24;

        public FoldersController(NotedbContext context)
        {
            _context = context;
            _dataPortFactory = new FolderDataPortServiceFactory(context);
        }

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

        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            ViewData["Search"] = search;

            var query = _context.Folders
                .Include(f => f.Parentfolder)
                .Where(f => f.Parentfolderid == null);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(f => f.Name.ToLower().Contains(search.ToLower()));

            query = query.OrderBy(f => f.Name);

            var result = await PaginatedList<Folder>.CreateAsync(query, page, PageSize, search);
            return View(result);
        }

        private const int SubPageSize  = 20;
        private const int FilePageSize = 15;

        public async Task<IActionResult> Details(int? id, int subPage = 1, int filePage = 1)
        {
            if (id == null) return NotFound();

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

        public IActionResult Create(int? parentFolderId)
        {
            ViewData["Parentfolderid"] = new SelectList(_context.Folders, "Id", "Name", parentFolderId);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Parentfolderid,Createdat,Id")] Folder folder)
        {
            bool exists = _context.Folders.Any(f => f.Name == folder.Name && f.Parentfolderid == folder.Parentfolderid);
            if (exists)
                ModelState.AddModelError("Name", "Папка з таким іменем вже існує в цьому каталозі.");

            if (ModelState.IsValid)
            {
                _context.Add(folder);
                await _context.SaveChangesAsync();
                if (folder.Parentfolderid != null)
                    return RedirectToAction("Details", new { id = folder.Parentfolderid });
                return RedirectToAction(nameof(Index));
            }
            ViewData["Parentfolderid"] = new SelectList(_context.Folders, "Id", "Name", folder.Parentfolderid);
            return View(folder);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var folder = await _context.Folders.FindAsync(id);
            if (folder == null) return NotFound();

            ViewData["Parentfolderid"] = new SelectList(_context.Folders, "Id", "Name", folder.Parentfolderid);
            return View(folder);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Name,Parentfolderid,Createdat,Id")] Folder folder)
        {
            if (id != folder.Id) return NotFound();

            bool exists = _context.Folders.Any(f => f.Name == folder.Name && f.Parentfolderid == folder.Parentfolderid && f.Id != folder.Id);
            if (exists)
                ModelState.AddModelError("Name", "Папка з таким іменем вже існує в цьому каталозі.");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(folder);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FolderExists(folder.Id)) return NotFound();
                    else throw;
                }
                if (folder.Parentfolderid != null)
                    return RedirectToAction("Details", new { id = folder.Parentfolderid });
                return RedirectToAction(nameof(Index));
            }
            ViewData["Parentfolderid"] = new SelectList(_context.Folders, "Id", "Name", folder.Parentfolderid);
            return View(folder);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

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
            var folder = await _context.Folders.FindAsync(id);
            if (folder != null)
                _context.Folders.Remove(folder);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ──────────────────────────────────────────────
        // Імпорт / Експорт (Excel та Word — єдиний маршрут)
        // ──────────────────────────────────────────────

        [HttpGet]
        public IActionResult Import()
        {
            return View();
        }

        /// <summary>
        /// Єдина точка імпорту для Excel (.xlsx) і Word (.docx).
        /// Тип файлу визначається автоматично за <c>ContentType</c> завантаженого файлу;
        /// потрібний сервіс повертає <see cref="FolderDataPortServiceFactory"/>.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("file", "Будь ласка, оберіть файл (.xlsx або .docx) для завантаження.");
                return View();
            }

            try
            {
                // Деякі браузери/ОС надсилають application/octet-stream замість
                // правильного MIME-типу для .xlsx/.docx. Визначаємо тип за розширенням.
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

                var importService = _dataPortFactory.GetImportService(effectiveContentType);
                using var stream = file.OpenReadStream();
                await importService.ImportFromStreamAsync(stream, cancellationToken);
            }
            catch (NotImplementedException)
            {
                var ext = Path.GetExtension(file.FileName);
                ModelState.AddModelError("file",
                    $"Непідтримуваний формат файлу «{ext}». Оберіть .xlsx або .docx.");
                return View();
            }
            catch (Exception ex)
            {
                var detail = ex.InnerException?.Message ?? ex.Message;
                ModelState.AddModelError(string.Empty, $"Помилка імпорту: {detail}");
                return View();
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Єдина точка експорту для Excel (.xlsx) і Word (.docx).
        /// Формат задається параметром <paramref name="contentType"/> (за замовчуванням — Excel).
        /// Розширення файлу визначається автоматично.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Export(
            [FromQuery] string contentType =
                FolderDataPortServiceFactory.ExcelContentType,
            CancellationToken cancellationToken = default)
        {
            var exportService = _dataPortFactory.GetExportService(contentType);

            var memoryStream = new MemoryStream();
            await exportService.WriteToAsync(memoryStream, cancellationToken);
            await memoryStream.FlushAsync(cancellationToken);
            memoryStream.Position = 0;

            // Визначаємо розширення на основі content type, а не хардкодом
            var extension = contentType == FolderDataPortServiceFactory.DocxContentType
                ? "docx"
                : "xlsx";
            var fileName = $"folders_{DateTime.UtcNow:yyyy-MM-dd}.{extension}";

            return new FileStreamResult(memoryStream, contentType) { FileDownloadName = fileName };
        }

        private bool FolderExists(int id) => _context.Folders.Any(e => e.Id == id);
    }
}
