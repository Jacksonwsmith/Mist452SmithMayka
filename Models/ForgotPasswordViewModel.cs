using System.ComponentModel.DataAnnotations;

namespace Mist452SmithMayka.Models
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Email MUST be provided")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
