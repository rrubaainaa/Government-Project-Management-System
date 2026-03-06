using System.ComponentModel.DataAnnotations;

namespace GPMS.ViewModels
{
    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        public string Captcha { get; set; }

        public string CaptchaCode { get; set; }
    }
}
