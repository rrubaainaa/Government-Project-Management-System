using GPMS.Data;
using GPMS.Models;
using GPMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GPMS.Controllers
{
    [Authorize]
    public class ModuleController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PermissionService _permissionService;
        private readonly IWebHostEnvironment _environment;

        public ModuleController(
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

        // =========================================
        // 🔥 INDEX (FIXED)
        // =========================================
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string status)
        {
            var employeeId = GetEmployeeId();

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            var allModules = await _context.Modules
                .Include(m => m.Project)
                .Include(m => m.Tasks)
                .ToListAsync();

            var filteredModules = new List<Module>();
            var modulePermissions = new Dictionary<int, List<string>>();

            foreach (var m in allModules)
            {
                bool isAssigned = await _context.Assignments
                    .AnyAsync(a => a.EmployeeId == employeeId && a.ProjectId == m.ProjectId);

                bool canView = await _permissionService.HasPermission(employeeId, m.ModuleId, "ViewModule");

                if (employee.IsAdmin || (isAssigned && canView))
                {
                    filteredModules.Add(m);

                    // ✅ FIX: ModuleId
                    var perms = await _permissionService.GetPermissions(employeeId, m.ModuleId);
                    modulePermissions[m.ModuleId] = perms;
                }
            }

            ViewBag.ModulePermissions = modulePermissions;

            ViewBag.CanCreateModule = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, null, "CreateModule");

            ViewBag.Projects = await _context.Projects.ToListAsync();
            ViewBag.IsAdmin = employee.IsAdmin;

            return View(filteredModules);
        }

        // =========================================
        // 🔥 DETAILS (FIXED)
        // =========================================
        public async Task<IActionResult> Details(int id)
        {
            var employeeId = GetEmployeeId();

            var module = await _context.Modules
                .Include(m => m.Project)
                .Include(m => m.Tasks)
                .Include(m => m.Assignments)
                    .ThenInclude(a => a.Employee)
                .Include(m => m.Assignments)
                    .ThenInclude(a => a.Role)
                .FirstOrDefaultAsync(m => m.ModuleId == id);

            if (module == null)
                return NotFound();

            var employee = await _context.Employees.FindAsync(employeeId);

            bool isAssigned = await _context.Assignments
                .AnyAsync(a => a.EmployeeId == employeeId && a.ProjectId == module.ProjectId);

            bool canView = await _permissionService.HasPermission(employeeId, module.ModuleId, "ViewModule");

            if (!employee.IsAdmin && !(isAssigned && canView))
                return Forbid();

            ViewBag.IsAdmin = employee.IsAdmin;

            // TASK PERMISSIONS
            var taskPermissions = new Dictionary<int, List<string>>();

            foreach (var t in module.Tasks)
            {
                var perms = await _permissionService.GetPermissions(employeeId, t.TaskId);
                taskPermissions[t.TaskId] = perms;
            }

            ViewBag.TaskPermissions = taskPermissions;

            ViewBag.CanCreateTask = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, module.ProjectId, "CreateTask");

            ViewBag.CanViewEmployee = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, module.ProjectId, "ViewEmployee");

            ViewBag.CanEditEmployee = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, module.ProjectId, "EditEmployee");

            return View(module);
        }

        // =========================================
        // 🔥 CREATE (FIXED)
        // =========================================
        public async Task<IActionResult> Create()
        {
            var employeeId = GetEmployeeId();

            var employee = await _context.Employees.FindAsync(employeeId);

            if (!employee.IsAdmin &&
                !await _permissionService.HasPermission(employeeId, null, "CreateModule"))
                return Forbid();

            List<Project> projects;

            if (employee.IsAdmin)
            {
                projects = await _context.Projects.ToListAsync();
            }
            else
            {
                var assignedProjectIds = await _context.Assignments
                    .Where(a => a.EmployeeId == employeeId)
                    .Select(a => a.ProjectId)
                    .Distinct()
                    .ToListAsync();

                projects = await _context.Projects
                    .Where(p => assignedProjectIds.Contains(p.ProjectId))
                    .ToListAsync();
            }

            ViewBag.ProjectList = new SelectList(projects, "ProjectId", "ProjectName");

            return View();
        }

        // =========================================
        // 🔥 EDIT (FIXED)
        // =========================================
        public async Task<IActionResult> Edit(int id)
        {
            var employeeId = GetEmployeeId();

            var module = await _context.Modules.FindAsync(id);
            if (module == null)
                return NotFound();

            var employee = await _context.Employees.FindAsync(employeeId);

            if (!employee.IsAdmin &&
                !await _permissionService.HasPermission(employeeId, id, "EditModule"))
                return Forbid();

            ViewBag.ProjectList = new SelectList(_context.Projects, "ProjectId", "ProjectName", module.ProjectId);

            return View(module);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Module module)
        {
            var employeeId = GetEmployeeId();

            var employee = await _context.Employees.FindAsync(employeeId);

            if (!employee.IsAdmin &&
                !await _permissionService.HasPermission(employeeId, module.ModuleId, "EditModule"))
                return Forbid();

            if (ModelState.IsValid)
            {
                _context.Modules.Update(module);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Module updated successfully.";
                return RedirectToAction("Details", "Project", new { id = module.ProjectId });
            }

            return View(module);
        }

        // =========================================
        // 🔥 DELETE (FIXED)
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var employeeId = GetEmployeeId();

            var module = await _context.Modules
                .Include(m => m.Tasks)
                .FirstOrDefaultAsync(m => m.ModuleId == id);

            if (module == null)
                return NotFound();

            var employee = await _context.Employees.FindAsync(employeeId);

            // ✅ FIX: admin override + ModuleId
            if (!employee.IsAdmin &&
                !await _permissionService.HasPermission(employeeId, id, "DeleteModule"))
                return Forbid();

            bool hasTasks = module.Tasks.Any();

            bool hasAssignments = await _context.Assignments
                .AnyAsync(a => a.ModuleId == id);

            if (hasTasks || hasAssignments)
            {
                int taskCount = module.Tasks.Count;

                int assignmentCount = await _context.Assignments
                    .CountAsync(a => a.ModuleId == id);

                TempData["Error"] = $"Cannot delete module. It has {taskCount} tasks and {assignmentCount} assignments.";

                return RedirectToAction("Details", "Project", new { id = module.ProjectId });
            }

            _context.Modules.Remove(module);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Module deleted successfully.";

            return RedirectToAction("Details", "Project", new { id = module.ProjectId });
        }
    }
}