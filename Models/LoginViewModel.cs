using System.ComponentModel.DataAnnotations;

namespace Mist452SmithMayka.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username MUST be provided")]
        [Display(Name = "Username")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Password MUST be provided")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }
    }
}
