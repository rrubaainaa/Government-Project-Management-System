using GPMS.Data;
using GPMS.Models;
using GPMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GPMS.Controllers
{
    [Authorize]
    public class DocumentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PermissionService _permissionService;
        private readonly IWebHostEnvironment _environment;

        public DocumentController(
            AppDbContext context,
            PermissionService permissionService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _permissionService = permissionService;
            _environment = environment;
        }

        private int GetEmployeeId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null)
                throw new Exception("User not logged in");

            return int.Parse(claim.Value);
        }

        // ===================== INDEX =====================
        public async Task<IActionResult> Index()
        {
            var employeeId = GetEmployeeId();

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            if (employee == null)
                return Unauthorized();

            var documents = await _context.Documents
                .Include(d => d.UploadedByEmployee)
                .Include(d => d.Assignment)
                    .ThenInclude(a => a.Project)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            var visibleDocuments = new List<Document>();

            foreach (var doc in documents)
            {
                int? projectId = doc.Assignment?.ProjectId;

                if (employee.IsAdmin)
                {
                    visibleDocuments.Add(doc);
                    continue;
                }

                if (projectId.HasValue)
                {
                    bool canView = await _permissionService.HasPermission(
                        employeeId,
                        projectId.Value,
                        "ViewDocument"
                    );

                    bool assigned = await _context.Assignments.AnyAsync(a =>
                        a.EmployeeId == employeeId &&
                        a.ProjectId == projectId.Value
                    );

                    if (assigned && canView)
                        visibleDocuments.Add(doc);
                }
            }

            ViewBag.CanUpload = employee.IsAdmin;

            if (!employee.IsAdmin)
            {
                var assignedProjectIds = await _context.Assignments
                    .Where(a => a.EmployeeId == employeeId && a.ProjectId != null)
                    .Select(a => a.ProjectId!.Value)
                    .Distinct()
                    .ToListAsync();

                foreach (var projectId in assignedProjectIds)
                {
                    if (await _permissionService.HasPermission(employeeId, projectId, "UploadDocument"))
                    {
                        ViewBag.CanUpload = true;
                        break;
                    }
                }
            }

            return View(visibleDocuments);
        }

        // ===================== UPLOAD GET =====================
        [HttpGet]
        public async Task<IActionResult> Upload()
        {
            var employeeId = GetEmployeeId();

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            if (employee == null)
                return Unauthorized();

            // ✅ Admin can see all projects (already correct)
            ViewBag.Projects = await _context.Projects.ToListAsync();

            return View();
        }

        // ===================== AJAX =====================
        public async Task<JsonResult> GetModules(int projectId)
        {
            var modules = await _context.Modules
                .Where(m => m.ProjectId == projectId)
                .Select(m => new
                {
                    module_id = m.ModuleId,
                    module_name = m.ModuleName
                })
                .ToListAsync();

            return Json(modules);
        }

        public async Task<JsonResult> GetTasks(int moduleId)
        {
            var tasks = await _context.Tasks
                .Where(t => t.ModuleId == moduleId)
                .Select(t => new
                {
                    task_id = t.TaskId,
                    task_name = t.TaskName
                })
                .ToListAsync();

            return Json(tasks);
        }

        // ===================== UPLOAD POST =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int taskId, IFormFile file)
        {
            var employeeId = GetEmployeeId();

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            if (employee == null)
                return Unauthorized();

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file.";
                return RedirectToAction(nameof(Upload));
            }

            var task = await _context.Tasks
                .Include(t => t.Module)
                .ThenInclude(m => m.Project)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);

            if (task == null)
            {
                TempData["Error"] = "Invalid task.";
                return RedirectToAction(nameof(Upload));
            }

            int projectId = task.Module.ProjectId;

            // ✅ Permission only (NO assignment)
            bool canUpload = employee.IsAdmin ||
                             await _permissionService.HasPermission(
                                 employeeId,
                                 projectId,
                                 "UploadDocument"
                             );

            if (!canUpload)
                return Forbid();

            // ✅ File validation
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Invalid file type.";
                return RedirectToAction(nameof(Upload));
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "documents");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // ✅ SAVE WITHOUT ASSIGNMENT
            var document = new Document
            {
                TaskId = taskId,
                DocumentName = Path.GetFileName(file.FileName),
                FilePath = $"/uploads/documents/{uniqueFileName}",
                UploadedAt = DateTime.Now,
                UploadedBy = employeeId
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document uploaded successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ===================== DOWNLOAD =====================
        public async Task<IActionResult> Download(int id)
        {
            var employeeId = GetEmployeeId();

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            if (employee == null)
                return Unauthorized();

            var document = await _context.Documents
                .Include(d => d.Assignment)
                .FirstOrDefaultAsync(d => d.DocumentId == id);

            if (document == null || string.IsNullOrEmpty(document.FilePath))
                return NotFound();

            int? projectId = document.Assignment?.ProjectId;

            bool canView = employee.IsAdmin;

            if (!canView && projectId.HasValue)
            {
                canView = await _permissionService.HasPermission(
                    employeeId,
                    projectId.Value,
                    "ViewDocument"
                );
            }

            if (!canView)
                return Forbid();

            var fullPath = Path.Combine(
                _environment.WebRootPath,
                document.FilePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
            );

            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            return PhysicalFile(fullPath, "application/octet-stream", document.DocumentName);
        }

        // ===================== DELETE =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var document = await _context.Documents.FindAsync(id);

            if (document == null)
                return NotFound();

            if (!string.IsNullOrEmpty(document.FilePath))
            {
                var fullPath = Path.Combine(
                    _environment.WebRootPath,
                    document.FilePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
                );

                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
            }

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}