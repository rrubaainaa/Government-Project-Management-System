using GPMS.Data;
using GPMS.Models;
using GPMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace GPMS.Controllers
{
    [Authorize]
    public class EmployeeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PermissionService _permissionService;
        private readonly IPasswordHasher<Employee> _passwordHasher;
        private readonly EmailService _emailService;

        public EmployeeController(
            AppDbContext context,
            PermissionService permissionService,
            IPasswordHasher<Employee> passwordHasher,
            EmailService emailService)
        {
            _context = context;
            _permissionService = permissionService;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
        }

        private int GetEmployeeId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (claim == null)
                throw new Exception("User not logged in");

            return int.Parse(claim.Value);
        }

        private string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        public async Task<IActionResult> Index(string search)
        {
            var employeeId = GetEmployeeId();

            if (!await _permissionService.HasPermission(employeeId, null, "ViewEmployee"))
                return Forbid();

            var employees = _context.Employees
                .Include(e => e.Designation)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                employees = employees.Where(e =>
                    e.EmployeeName.Contains(search) ||
                    e.Email.Contains(search) ||
                    e.Username.Contains(search));
            }

            ViewBag.CanCreate = await _permissionService.HasPermission(employeeId, null, "CreateEmployee");
            ViewBag.CanEdit = await _permissionService.HasPermission(employeeId, null, "EditEmployee");
            ViewBag.CanDelete = await _permissionService.HasPermission(employeeId, null, "DeleteEmployee");

            return View(await employees.ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var employeeId = GetEmployeeId();

            if (!await _permissionService.HasPermission(employeeId, null, "CreateEmployee"))
                return Forbid();

            await LoadDesignations();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee employee)
        {
            var employeeId = GetEmployeeId();

            if (!await _permissionService.HasPermission(employeeId, null, "CreateEmployee"))
                return Forbid();

            if (ModelState.IsValid)
            {
                var tokenBytes = RandomNumberGenerator.GetBytes(32);

                var rawToken = Convert.ToBase64String(tokenBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");

                employee.ResetToken = HashToken(rawToken);
                employee.ResetTokenExpiry = DateTime.UtcNow.AddHours(24);

                employee.Epassword = null;
                employee.IsFirstLogin = true;
                employee.PasswordChangedAt = null;

                _context.Add(employee);
                await _context.SaveChangesAsync();

                var resetLink = Url.Action(
                    "ResetPassword",
                    "Account",
                    new { token = rawToken, email = employee.Email },
                    protocol: Request.Scheme);

                var body = $@"
                    <p>Hello {employee.EmployeeName},</p>
                    <p>Your account has been created.</p>
                    <p>Click below to set your password:</p>
                    <p><a href='{resetLink}'>Set Password</a></p>
                    <p>This link will expire in 24 hours.</p>";

                await _emailService.SendEmailAsync(
                    employee.Email,
                    "Set Your Password",
                    body
                );

                TempData["Success"] = "Employee created successfully. Password setup link sent to email.";
                return RedirectToAction(nameof(Index));
            }

            await LoadDesignations();
            return View(employee);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var employeeId = GetEmployeeId();

            if (!await _permissionService.HasPermission(employeeId, null, "EditEmployee"))
                return Forbid();

            var emp = await _context.Employees.FindAsync(id);

            if (emp == null)
                return NotFound();

            await LoadDesignations();
            return View(emp);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Employee emp)
        {
            var employeeId = GetEmployeeId();

            if (!await _permissionService.HasPermission(employeeId, null, "EditEmployee"))
                return Forbid();

            if (id != emp.EmployeeId)
                return NotFound();

            if (ModelState.IsValid)
            {
                var existing = await _context.Employees.FindAsync(id);

                if (existing == null)
                    return NotFound();

                existing.EmployeeName = emp.EmployeeName;
                existing.Email = emp.Email;
                existing.Username = emp.Username;
                existing.DesignationId = emp.DesignationId;
                existing.SystemRole = emp.SystemRole;
                existing.IsAdmin = emp.IsAdmin;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Employee updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            await LoadDesignations();
            return View(emp);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var employeeId = GetEmployeeId();

            if (!await _permissionService.HasPermission(employeeId, null, "DeleteEmployee"))
                return Forbid();

            var emp = await _context.Employees.FindAsync(id);

            if (emp == null)
                return NotFound();

            if (emp.IsAdmin)
            {
                TempData["Error"] = "Admin user cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            bool assignedToProject = await _context.Assignments
                .AnyAsync(a => a.EmployeeId == id && a.ProjectId != null);

            bool assignedToModule = await _context.Assignments
                .AnyAsync(a => a.EmployeeId == id && a.ModuleId != null);

            bool assignedToTask = await _context.Assignments
                .AnyAsync(a => a.EmployeeId == id && a.TaskId != null);

            if (assignedToProject)
            {
                TempData["Error"] = "Cannot delete employee. Assigned to a project.";
                return RedirectToAction(nameof(Index));
            }

            if (assignedToModule)
            {
                TempData["Error"] = "Cannot delete employee. Assigned to a module.";
                return RedirectToAction(nameof(Index));
            }

            if (assignedToTask)
            {
                TempData["Error"] = "Cannot delete employee. Assigned to a task.";
                return RedirectToAction(nameof(Index));
            }

            _context.Employees.Remove(emp);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Employee deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private async System.Threading.Tasks.Task LoadDesignations()
        {
            ViewBag.DesignationList = await _context.Designations
                .Select(d => new SelectListItem
                {
                    Value = d.DesignationId.ToString(),
                    Text = d.DesignationName
                })
                .ToListAsync();
        }
    }
}