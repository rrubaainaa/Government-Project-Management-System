using GPMS.Data;
using GPMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GPMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;

        public AccountController(AppDbContext db)
        {
            _db = db;
        }

        public IActionResult Login()
        {
            var captcha = GenerateCaptcha();
            HttpContext.Session.SetString("CaptchaCode", captcha);

            return View(new LoginViewModel
            {
                CaptchaCode = captcha
            });
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            var sessionCaptcha = HttpContext.Session.GetString("CaptchaCode");

            if (model.Captcha != sessionCaptcha)
            {
                ModelState.AddModelError("", "Invalid captcha.");
                model.CaptchaCode = GenerateCaptcha();
                HttpContext.Session.SetString("CaptchaCode", model.CaptchaCode);
                return View(model);
            }

            var user = await _db.Employees
                .FirstOrDefaultAsync(x => x.Username == model.Username);

            if (user == null || user.Epassword != model.Password)
            {
                ModelState.AddModelError("", "Invalid username or password.");
                model.CaptchaCode = GenerateCaptcha();
                HttpContext.Session.SetString("CaptchaCode", model.CaptchaCode);
                return View(model);
            }

            HttpContext.Session.SetInt32("EmployeeId", user.EmployeeId);
            HttpContext.Session.SetString("EmployeeName", user.EmployeeName);

            return RedirectToAction("Index", "Dashboard");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        private string GenerateCaptcha()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 5)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}