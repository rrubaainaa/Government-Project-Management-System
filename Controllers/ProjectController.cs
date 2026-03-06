using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GPMS.Models;
using GPMS.Data;

namespace GPMS.Controllers
{
    public class ProjectController : Controller
    {
        private readonly AppDbContext _context;

        public ProjectController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Project
        public async Task<IActionResult> Index()
        {
            var projects = await _context.Projects
                                         .Include(p => p.Modules)
                                         .ToListAsync();

            return View(projects);
        }


        // GET: Project/Create
        public IActionResult Create()
        {
            return View();
        }


        // POST: Project/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Project project)
        {
            if (ModelState.IsValid)
            {
                _context.Add(project);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(project);
        }
    }
}