using GPMS.Data;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace GPMS.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewBag.Projects = _context.Projects.Count();
            ViewBag.Modules = _context.Modules.Count();
            ViewBag.Tasks = _context.Tasks.Count();
            ViewBag.Assignments = _context.Assignments.Count();
            ViewBag.Employees = _context.Employees.Count();

            ViewBag.Completed = _context.Tasks
                .Where(t => t.TaskStatus == "Completed")
                .Count();

            ViewBag.Ongoing = _context.Tasks
                .Where(t => t.TaskStatus == "Ongoing")
                .Count();

            return View();
        }
    }
}