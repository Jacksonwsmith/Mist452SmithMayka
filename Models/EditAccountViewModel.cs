using System.ComponentModel.DataAnnotations;

namespace Mist452SmithMayka.Models
{
    public class EditAccountViewModel
    {
        [Required(ErrorMessage = "Name MUST be provided")]
        [Display(Name = "Full Name")]
        public string Name { get; set; }

        [Display(Name = "Street Address")]
        public string? StreetAddress { get; set; }

        public string? City { get; set; }

        public string? State { get; set; }

        [Display(Name = "Postal Code")]
        public string? PostalCode { get; set; }
    }
}
