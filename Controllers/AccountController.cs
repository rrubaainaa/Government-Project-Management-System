using GPMS.Data;
using GPMS.Models;
using GPMS.Services;
using GPMS.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace GPMS.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<Employee> _passwordHasher;
        private readonly EmailService _emailService;

        public AccountController(
            AppDbContext db,
            IPasswordHasher<Employee> passwordHasher,
            EmailService emailService)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
        }

        // =========================================
        // HASH TOKEN
        // =========================================
        private string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        // =========================================
        // LOGIN GET
        // =========================================
        [HttpGet]
        public IActionResult Login()
        {
            var captcha = GenerateCaptcha();

            HttpContext.Session.SetString(
                "CaptchaCode",
                captcha
            );

            return View(new LoginViewModel
            {
                CaptchaCode = captcha
            });
        }

        // =========================================
        // LOGIN POST
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            var sessionCaptcha =
                HttpContext.Session.GetString("CaptchaCode");

            if (model.Captcha != sessionCaptcha)
            {
                ModelState.AddModelError("", "Invalid captcha.");
                return ReloadCaptcha(model);
            }

            var user = await _db.Employees
                .FirstOrDefaultAsync(x =>
                    x.Username == model.Username);

            if (user == null)
            {
                ModelState.AddModelError("",
                    "Invalid username or password.");

                return ReloadCaptcha(model);
            }

            if (string.IsNullOrWhiteSpace(user.Epassword))
            {
                ModelState.AddModelError("",
                    "Please set your password using the email link.");

                return ReloadCaptcha(model);
            }

            if (!string.IsNullOrWhiteSpace(user.ResetToken))
            {
                ModelState.AddModelError("",
                    "Please set your password using the email link.");

                return ReloadCaptcha(model);
            }

            // ✅ SERVER SIDE PBKDF2 VERIFICATION
            var result = _passwordHasher.VerifyHashedPassword(
                user,
                user.Epassword,
                model.Password
            );

            bool passwordValid =
                result != PasswordVerificationResult.Failed;

            if (!passwordValid)
            {
                ModelState.AddModelError("",
                    "Invalid username or password.");

                return ReloadCaptcha(model);
            }

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.Name,
                    user.EmployeeName
                ),

                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.EmployeeId.ToString()
                ),

                new Claim(
                    ClaimTypes.Role,
                    user.SystemRole ?? "User"
                ),

                new Claim(
                    "IsAdmin",
                    user.IsAdmin.ToString()
                )
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),

                new AuthenticationProperties
                {
                    IsPersistent = true
                }
            );

            HttpContext.Session.SetInt32(
                "EmployeeId",
                user.EmployeeId
            );

            HttpContext.Session.SetString(
                "EmployeeName",
                user.EmployeeName
            );

            HttpContext.Session.SetString(
                "UserRole",
                user.SystemRole ?? "User"
            );

            HttpContext.Session.SetString(
                "IsAdmin",
                user.IsAdmin.ToString()
            );

            bool passwordExpired =
                !user.PasswordChangedAt.HasValue ||
                user.PasswordChangedAt.Value
                    .AddMonths(4) <= DateTime.Now;

            if (user.IsFirstLogin || passwordExpired)
            {
                HttpContext.Session.SetString(
                    "ForcePasswordChange",
                    "true"
                );

                return RedirectToAction(
                    "ChangePassword"
                );
            }

            HttpContext.Session.Remove(
                "ForcePasswordChange"
            );

            return RedirectToAction(
                "Index",
                "Dashboard"
            );
        }

        // =========================================
        // FORGOT PASSWORD GET
        // =========================================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        // =========================================
        // FORGOT PASSWORD POST
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(
            ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _db.Employees
                .FirstOrDefaultAsync(e =>
                    e.Email == model.EmailOrUsername ||
                    e.Username == model.EmailOrUsername);

            if (user != null)
            {
                var tokenBytes =
                    RandomNumberGenerator.GetBytes(32);

                var rawToken =
                    Convert.ToBase64String(tokenBytes)
                        .Replace("+", "-")
                        .Replace("/", "_")
                        .Replace("=", "");

                // ✅ HASH TOKEN
                var hashedToken =
                    HashToken(rawToken);

                user.ResetToken = hashedToken;

                user.ResetTokenExpiry =
                    DateTime.UtcNow.AddHours(24);

                await _db.SaveChangesAsync();

                var resetLink = Url.Action(
                    "ResetPassword",
                    "Account",

                    new
                    {
                        token = rawToken,
                        email = user.Email
                    },

                    protocol: Request.Scheme
                );

                var body = $@"
                    <p>Hello {user.EmployeeName},</p>

                    <p>
                        Click below to set/reset your password:
                    </p>

                    <p>
                        <a href='{resetLink}'>
                            Set Password
                        </a>
                    </p>

                    <p>
                        This link will expire in 24 hours.
                    </p>";

                await _emailService.SendEmailAsync(
                    user.Email,
                    "Set Your Password",
                    body
                );
            }

            TempData["Success"] =
                "If the account exists, a reset link has been sent.";

            return RedirectToAction("Login");
        }

        // =========================================
        // RESET PASSWORD GET
        // =========================================
        [HttpGet]
        public IActionResult ResetPassword(
            string token,
            string email)
        {
            if (string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] =
                    "Invalid password reset link.";

                return RedirectToAction(
                    "ForgotPassword"
                );
            }

            return View(new ResetPasswordViewModel
            {
                Token = token,
                Email = email
            });
        }

        // =========================================
        // RESET PASSWORD POST
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);





            var user = await _db.Employees
                .FirstOrDefaultAsync(e =>
                    e.Email == model.Email);

            var incomingHash =
                HashToken(model.Token);

            if (user == null ||
                user.ResetToken != incomingHash ||
                !user.ResetTokenExpiry.HasValue ||
                user.ResetTokenExpiry.Value < DateTime.UtcNow)
            {
                TempData["Error"] =
                    "Invalid or expired link.";

                return RedirectToAction(
                    "ForgotPassword"
                );
            }

            // ✅ SERVER SIDE PBKDF2 HASHING
            user.Epassword =
                _passwordHasher.HashPassword(
                    user,
                    model.NewPassword
                );

            user.ResetToken = null;
            user.ResetTokenExpiry = null;

            user.IsFirstLogin = false;

            user.PasswordChangedAt =
                DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["Success"] =
                "Password reset successfully. Please log in.";

            return RedirectToAction("Login");
        }

        // =========================================
        // CHANGE PASSWORD GET
        // =========================================
        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        // =========================================
        // CHANGE PASSWORD POST
        // =========================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var claim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier
                );

            if (claim == null)
                return RedirectToAction("Login");

            int employeeId =
                int.Parse(claim.Value);

            var user =
                await _db.Employees.FindAsync(employeeId);

            if (user == null)
                return RedirectToAction("Login");

            // ✅ VERIFY HASHED PASSWORD
            var result =
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.Epassword,
                    model.CurrentPassword
                );

            bool valid =
                result != PasswordVerificationResult.Failed;

            if (!valid)
            {
                ModelState.AddModelError(
                    "CurrentPassword",
                    "Incorrect current password."
                );

                return View(model);
            }

            if (model.CurrentPassword ==
                model.NewPassword)
            {
                ModelState.AddModelError(
                    "NewPassword",
                    "New password must be different."
                );






            }

            // ✅ STORE NEW HASHED PASSWORD
            user.Epassword =
                _passwordHasher.HashPassword(
                    user,
                    model.NewPassword
                );

            user.IsFirstLogin = false;

            user.PasswordChangedAt =
                DateTime.Now;

            user.ResetToken = null;
            user.ResetTokenExpiry = null;

            await _db.SaveChangesAsync();

            HttpContext.Session.Remove(
                "ForcePasswordChange"
            );

            TempData["Success"] =
                "Password changed successfully.";

            return RedirectToAction(
                "Index",
                "Dashboard"
            );
        }

        // =========================================
        // LOGOUT
        // =========================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();

            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            return RedirectToAction("Login");
        }

        // =========================================
        // HELPERS
        // =========================================
        private IActionResult ReloadCaptcha(
            LoginViewModel model)
        {
            model.CaptchaCode =
                GenerateCaptcha();

            HttpContext.Session.SetString(
                "CaptchaCode",
                model.CaptchaCode
            );

            return View("Login", model);
        }

        private string GenerateCaptcha()
        {
            const string chars =
                "ABCDEFGHJKLMNPQRSTUVWXYZ23456789abcdefghjklmnpqrstuvwxyz";

            var random = new Random();

            return new string(
                Enumerable.Repeat(chars, 5)
                    .Select(s =>
                        s[random.Next(s.Length)])
                    .ToArray()
            );
        }

        private bool IsValidPassword(
            string password)
        {
            if (string.IsNullOrWhiteSpace(password) ||
                password.Length < 8)
            {
                return false;
            }

            bool hasUpper =
                password.Any(char.IsUpper);

            bool hasLower =
                password.Any(char.IsLower);

            bool hasDigit =
                password.Any(char.IsDigit);

            bool hasSpecial =
                password.Any(ch =>
                    !char.IsLetterOrDigit(ch));

            return hasUpper &&
                   hasLower &&
                   hasDigit &&
                   hasSpecial;
        }
    }
}