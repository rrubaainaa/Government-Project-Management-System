using GPMS.Data;
using GPMS.Models;
using GPMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Security.Claims;

// ALIAS
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
        // 🔥 EXPORT TO EXCEL (TASK) - FIXED
        // =========================================
        [HttpGet]
        public async Task<IActionResult> ExportToExcel(
            int? projectId,
            int? moduleId,
            string search,
            DateTime? startDate,
            DateTime? endDate,
            string status)
        {
            var employeeId = GetEmployeeId();

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            var allTasks = await _context.Tasks
                .Include(t => t.Module)
                    .ThenInclude(m => m.Project)
                .ToListAsync();

            var filteredTasks = new List<TaskModel>();

            foreach (var t in allTasks)
            {
                var projId = t.Module.ProjectId;

                bool isAssigned = await _context.Assignments
                    .AnyAsync(a => a.EmployeeId == employeeId && a.ProjectId == projId);

                bool canView = await _permissionService
                    .HasPermission(employeeId, projId, "ViewTask");

                if (employee.IsAdmin || (isAssigned && canView))
                {
                    bool match = true;

                    // ✅ Date filter (DateOnly fix)
                    if (startDate.HasValue)
                    {
                        var start = DateOnly.FromDateTime(startDate.Value);
                        if (t.TaskStartDate.HasValue && t.TaskStartDate.Value < start)
                            match = false;
                    }

                    if (endDate.HasValue)
                    {
                        var end = DateOnly.FromDateTime(endDate.Value);
                        if (t.TaskEndDate.HasValue && t.TaskEndDate.Value > end)
                            match = false;
                    }

                    // STATUS
                    if (!string.IsNullOrEmpty(status) && t.TaskStatus != status)
                        match = false;

                    if (match)
                        filteredTasks.Add(t);
                }
            }

            // BASIC FILTERS
            if (projectId.HasValue)
                filteredTasks = filteredTasks.Where(t => t.Module.ProjectId == projectId.Value).ToList();

            if (moduleId.HasValue)
                filteredTasks = filteredTasks.Where(t => t.ModuleId == moduleId.Value).ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                filteredTasks = filteredTasks.Where(t =>
                    t.TaskName.Contains(search) ||
                    (t.TaskDescription != null && t.TaskDescription.Contains(search)) ||
                    (t.TaskStatus != null && t.TaskStatus.Contains(search)) ||
                    (t.Module != null && t.Module.ModuleName.Contains(search))
                ).ToList();
            }

            // ✅ Excel generation
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Tasks");

            worksheet.Cells[1, 1].Value = "Task Name";
            worksheet.Cells[1, 2].Value = "Description";
            worksheet.Cells[1, 3].Value = "Status";
            worksheet.Cells[1, 4].Value = "Start Date";
            worksheet.Cells[1, 5].Value = "End Date";
            worksheet.Cells[1, 6].Value = "Module";
            worksheet.Cells[1, 7].Value = "Project";

            int row = 2;

            foreach (var t in filteredTasks)
            {
                worksheet.Cells[row, 1].Value = t.TaskName;
                worksheet.Cells[row, 2].Value = t.TaskDescription;
                worksheet.Cells[row, 3].Value = t.TaskStatus;

                // ✅ DateOnly? fix
                worksheet.Cells[row, 4].Value = t.TaskStartDate.HasValue
                    ? t.TaskStartDate.Value.ToString("yyyy-MM-dd")
                    : "";

                worksheet.Cells[row, 5].Value = t.TaskEndDate.HasValue
                    ? t.TaskEndDate.Value.ToString("yyyy-MM-dd")
                    : "";

                worksheet.Cells[row, 6].Value = t.Module?.ModuleName;
                worksheet.Cells[row, 7].Value = t.Module?.Project?.ProjectName;

                row++;
            }

            worksheet.Cells.AutoFitColumns();

            var stream = new MemoryStream();
            package.SaveAs(stream);
            stream.Position = 0;

            string fileName = $"Tasks_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // =========================================
        // 🔥 FULL INDEX (MERGED)
        // =========================================
        public async Task<IActionResult> Index(
            int? projectId,
            int? moduleId,
            string search,
            DateTime? startDate,
            DateTime? endDate,
            string status)
        {
            var employeeId = GetEmployeeId();

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            if (employee == null)
                return Unauthorized();

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
                    bool match = true;

                    // DATE FILTER
                    if (startDate.HasValue)
                    {
                        var start = DateOnly.FromDateTime(startDate.Value);
                        if (t.TaskStartDate.HasValue && t.TaskStartDate.Value < start)
                            match = false;
                    }

                    if (endDate.HasValue)
                    {
                        var end = DateOnly.FromDateTime(endDate.Value);
                        if (t.TaskEndDate.HasValue && t.TaskEndDate.Value > end)
                            match = false;
                    }

                    // STATUS FILTER
                    if (!string.IsNullOrEmpty(status) && t.TaskStatus != status)
                        match = false;

                    if (match)
                    {
                        filteredTasks.Add(t);

                        // ✅ FIX: use TaskId permissions (second code)
                        var perms = await _permissionService.GetPermissions(employeeId, t.TaskId);
                        taskPermissions[t.TaskId] = perms;
                    }
                }
            }

            // BASIC FILTERS
            if (projectId.HasValue)
                filteredTasks = filteredTasks.Where(t => t.Module.ProjectId == projectId.Value).ToList();

            if (moduleId.HasValue)
                filteredTasks = filteredTasks.Where(t => t.ModuleId == moduleId.Value).ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                filteredTasks = filteredTasks.Where(t =>
                    t.TaskName.Contains(search) ||
                    (t.TaskDescription != null && t.TaskDescription.Contains(search)) ||
                    (t.TaskStatus != null && t.TaskStatus.Contains(search)) ||
                    (t.Module != null && t.Module.ModuleName.Contains(search))
                ).ToList();
            }

            ViewBag.TaskPermissions = taskPermissions;
            ViewBag.IsAdmin = employee.IsAdmin;

            // ✅ DROPDOWNS FIXED (second code)
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

            // KEEP FILTER VALUES
            ViewBag.SelectedProjectId = projectId;
            ViewBag.SelectedModuleId = moduleId;
            ViewBag.Search = search;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.Status = status;

            return View(filteredTasks);
        }

        // =========================================
        // DETAILS
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

            var employee = await _context.Employees.FindAsync(employeeId);

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
        // EDIT
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
        // DELETE
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