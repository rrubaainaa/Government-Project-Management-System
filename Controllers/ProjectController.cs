using GPMS.Data;
using GPMS.Models;
using GPMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Security.Claims;

namespace GPMS.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PermissionService _permissionService;
        private readonly IWebHostEnvironment _environment;

        public ProjectController(
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
                throw new Exception("User not logged in properly.");

            return int.Parse(claim.Value);
        }

        // =========================================
        // 🔥 DETAILS (FIXED)
        // =========================================
        public async Task<IActionResult> Details(int id)
        {
            var employeeId = GetEmployeeId();

            var project = await _context.Projects
                .Include(p => p.Modules)
                    .ThenInclude(m => m.Tasks)
                .Include(p => p.Assignments)
                    .ThenInclude(a => a.Employee)
                .Include(p => p.Assignments)
                    .ThenInclude(a => a.Role)
                .FirstOrDefaultAsync(p => p.ProjectId == id);

            if (project == null)
                return NotFound();

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            bool isAssigned = await _context.Assignments
                .AnyAsync(a => a.EmployeeId == employeeId && a.ProjectId == id);

            bool canView = await _permissionService.HasPermission(employeeId, id, "ViewProject");

            if (!employee.IsAdmin && (!isAssigned || !canView))
                return Forbid();

            // ✅ ADMIN FLAG
            ViewBag.IsAdmin = employee.IsAdmin;

            // ✅ FIX: admin override
            ViewBag.CanEditProject = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, id, "EditProject");

            ViewBag.CanDeleteProject = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, id, "DeleteProject");

            // MODULE PERMISSIONS
            var modulePermissions = new Dictionary<int, List<string>>();

            foreach (var m in project.Modules)
            {
                var perms = await _permissionService.GetPermissions(employeeId, m.ModuleId);
                modulePermissions[m.ModuleId] = perms;
            }

            ViewBag.ModulePermissions = modulePermissions;

            // ✅ FIX: admin override
            ViewBag.CanCreateModule = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, id, "CreateModule");

            ViewBag.CanViewEmployee = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, id, "ViewEmployee");

            ViewBag.CanEditEmployee = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, id, "EditEmployee");

            return View(project);
        }

        // =========================================
        // 🔥 INDEX (FIXED ADMIN)
        // =========================================
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string status)
        {
            var employeeId = GetEmployeeId();

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            var allProjects = await _context.Projects
                .Include(p => p.Modules)
                .ToListAsync();

            var filteredProjects = new List<Project>();
            var projectPermissions = new Dictionary<int, List<string>>();

            foreach (var p in allProjects)
            {
                bool isAssigned = await _context.Assignments
                    .AnyAsync(a => a.EmployeeId == employeeId && a.ProjectId == p.ProjectId);

                bool canView = await _permissionService.HasPermission(employeeId, p.ProjectId, "ViewProject");

                if (employee.IsAdmin || (isAssigned && canView))
                {
                    filteredProjects.Add(p);

                    var perms = await _permissionService.GetPermissions(employeeId, p.ProjectId);
                    projectPermissions[p.ProjectId] = perms;
                }
            }

            // ✅ FIX: admin override
            ViewBag.CanCreate = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, null, "CreateProject");

            ViewBag.ProjectPermissions = projectPermissions;

            return View(filteredProjects);
        }

        // =========================================
        // 🔥 EDIT (GET)
        // =========================================
        public async Task<IActionResult> Edit(int id)
        {
            var employeeId = GetEmployeeId();
            var employee = await _context.Employees.FindAsync(employeeId);

            var project = await _context.Projects.FindAsync(id);

            if (project == null)
                return NotFound();

            // ✅ Permission check (admin override)
            if (!employee.IsAdmin &&
                !await _permissionService.HasPermission(employeeId, id, "EditProject"))
                return Forbid();

            return View(project);
        }

        // =========================================
        // 🔥 EDIT (POST)
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Project project)
        {
            var employeeId = GetEmployeeId();
            var employee = await _context.Employees.FindAsync(employeeId);

            // ✅ Permission check (admin override)
            if (!employee.IsAdmin &&
                !await _permissionService.HasPermission(employeeId, project.ProjectId, "EditProject"))
                return Forbid();

            if (!ModelState.IsValid)
                return View(project);

            var existingProject = await _context.Projects
                .FirstOrDefaultAsync(p => p.ProjectId == project.ProjectId);

            if (existingProject == null)
                return NotFound();

            // ✅ Update fields
            existingProject.ProjectName = project.ProjectName;
            existingProject.ProjectDetails = project.ProjectDetails;
            existingProject.ProjectStatus = project.ProjectStatus;
            existingProject.ProjectEndDate = project.ProjectEndDate;

            // ❌ Do NOT update StartDate (readonly in UI)

            await _context.SaveChangesAsync();

            TempData["Success"] = "Project updated successfully.";

            return RedirectToAction("Details", new { id = project.ProjectId });
        }

        // =========================================
        // 🔥 CREATE (FIXED ADMIN)
        // =========================================
        public async Task<IActionResult> Create()
        {
            var employeeId = GetEmployeeId();
            var employee = await _context.Employees.FindAsync(employeeId);

            if (!employee.IsAdmin &&
                !await _permissionService.HasPermission(employeeId, null, "CreateProject"))
                return Forbid();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Project project, IFormFile? documentFile)
        {
            var employeeId = GetEmployeeId();
            var employee = await _context.Employees.FindAsync(employeeId);

            if (!employee.IsAdmin &&
                !await _permissionService.HasPermission(employeeId, null, "CreateProject"))
                return Forbid();

            if (ModelState.IsValid)
            {
                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(project);
        }

        // =========================================
        // 🔥 DELETE (FIXED ADMIN)
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var employeeId = GetEmployeeId();
            var employee = await _context.Employees.FindAsync(employeeId);

            // ✅ FIX: admin override
            if (!employee.IsAdmin &&
                !await _permissionService.HasPermission(employeeId, id, "DeleteProject"))
                return Forbid();

            var project = await _context.Projects
                .Include(p => p.Modules)
                .FirstOrDefaultAsync(p => p.ProjectId == id);

            if (project == null)
                return NotFound();

            if (project.Modules.Any())
            {
                TempData["Error"] = "Cannot delete project. Delete modules first.";
                return RedirectToAction("Details", new { id });
            }

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Project deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}