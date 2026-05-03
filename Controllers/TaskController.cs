using GPMS.Data;
using GPMS.Models;
using GPMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

using TaskModel = GPMS.Models.Task;

namespace GPMS.Controllers
{
    [Authorize]
    public class TaskController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PermissionService _permissionService;

        public TaskController(AppDbContext context, PermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
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
        public async Task<IActionResult> Index(int? projectId, int? moduleId, string search)
        {
            var employeeId = GetEmployeeId();

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            var allTasks = await _context.Tasks
                .Include(t => t.Module)
                    .ThenInclude(m => m.Project)
                .ToListAsync();

            var filteredTasks = new List<TaskModel>();
            var taskPermissions = new Dictionary<int, List<string>>();

            foreach (var t in allTasks)
            {
                var projId = t.Module.ProjectId;

                bool isAssigned = await _context.Assignments
                    .AnyAsync(a => a.EmployeeId == employeeId && a.ProjectId == projId);

                bool canView = await _permissionService.HasPermission(employeeId, projId, "ViewTask");

                if (employee.IsAdmin || (isAssigned && canView))
                {
                    filteredTasks.Add(t);

                    var perms = await _permissionService.GetPermissions(employeeId, t.TaskId);
                    taskPermissions[t.TaskId] = perms;
                }
            }

            // Filters
            if (projectId.HasValue)
                filteredTasks = filteredTasks.Where(t => t.Module.ProjectId == projectId).ToList();

            if (moduleId.HasValue)
                filteredTasks = filteredTasks.Where(t => t.ModuleId == moduleId).ToList();

            if (!string.IsNullOrEmpty(search))
                filteredTasks = filteredTasks.Where(t => t.TaskName.Contains(search)).ToList();

            ViewBag.TaskPermissions = taskPermissions;
            ViewBag.IsAdmin = employee.IsAdmin;

            // ✅ FIX: Use List<SelectListItem> instead of SelectList
            ViewBag.Projects = await _context.Projects
                .Select(p => new SelectListItem
                {
                    Value = p.ProjectId.ToString(),
                    Text = p.ProjectName
                }).ToListAsync();

            ViewBag.Modules = await _context.Modules
                .Select(m => new SelectListItem
                {
                    Value = m.ModuleId.ToString(),
                    Text = m.ModuleName
                }).ToListAsync();

            return View(filteredTasks);
        }

        // =========================================
        // 🔥 DETAILS (UNCHANGED)
        // =========================================
        public async Task<IActionResult> Details(int id)
        {
            var employeeId = GetEmployeeId();

            var task = await _context.Tasks
                .Include(t => t.Module)
                    .ThenInclude(m => m.Project)
                .Include(t => t.Assignments)
                    .ThenInclude(a => a.Employee)
                .FirstOrDefaultAsync(t => t.TaskId == id);

            if (task == null)
                return NotFound();

            var projectId = task.Module.ProjectId;

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            bool isAssigned = await _context.Assignments
                .AnyAsync(a => a.EmployeeId == employeeId && a.ProjectId == projectId);

            bool canView = await _permissionService.HasPermission(employeeId, projectId, "ViewTask");

            if (!employee.IsAdmin && (!isAssigned || !canView))
                return Forbid();

            ViewBag.IsAdmin = employee.IsAdmin;

            ViewBag.CanEditTask = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, id, "EditTask");

            ViewBag.CanDeleteTask = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, id, "DeleteTask");

            ViewBag.CanCreateTask = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, projectId, "CreateTask");

            ViewBag.CanViewEmployee = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, projectId, "ViewAssignment");

            ViewBag.CanEditEmployee = employee.IsAdmin ||
                await _permissionService.HasPermission(employeeId, projectId, "EditAssignment");

            return View(task);
        }

        // =========================================
        // 🔥 EDIT (FIXED)
        // =========================================
        public async Task<IActionResult> Edit(int id)
        {
            var employeeId = GetEmployeeId();

            var task = await _context.Tasks
                .Include(t => t.Module)
                    .ThenInclude(m => m.Project)
                .FirstOrDefaultAsync(t => t.TaskId == id);

            if (task == null)
                return NotFound();

            var employee = await _context.Employees.FindAsync(employeeId);

            if (!employee.IsAdmin &&
                !await _permissionService.HasPermission(employeeId, id, "EditTask"))
                return Forbid();

            ViewBag.ModuleList = new SelectList(_context.Modules, "ModuleId", "ModuleName", task.ModuleId);

            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TaskModel task)
        {
            var employeeId = GetEmployeeId();

            var employee = await _context.Employees.FindAsync(employeeId);

            if (!employee.IsAdmin &&
                !await _permissionService.HasPermission(employeeId, task.TaskId, "EditTask"))
                return Forbid();

            if (ModelState.IsValid)
            {
                _context.Tasks.Update(task);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Task updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            return View(task);
        }


        // =========================================
        // 🔥 DELETE (FIXED)
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var employeeId = GetEmployeeId();

            var task = await _context.Tasks
                .Include(t => t.Module)
                .FirstOrDefaultAsync(t => t.TaskId == id);

            var employee = await _context.Employees.FindAsync(employeeId);

            if (!employee.IsAdmin &&
                !await _permissionService.HasPermission(employeeId, id, "DeleteTask"))
                return Forbid();

            var assignments = _context.Assignments.Where(a => a.TaskId == id);

            _context.Assignments.RemoveRange(assignments);
            _context.Tasks.Remove(task);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Task deleted successfully.";
            return RedirectToAction(nameof(Index));
        }


        // =========================================
        // AJAX
        // =========================================
        public JsonResult GetModulesByProject(int projectId)
        {
            var modules = _context.Modules
                .Where(m => m.ProjectId == projectId)
                .Select(m => new
                {
                    moduleId = m.ModuleId,
                    moduleName = m.ModuleName
                })
                .ToList();

            return Json(modules);
        }
    }
}