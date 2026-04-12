using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mist452SmithMayka.Models
{
    public class Listing
    {
        [Key]
        public int ListingId { get; set; }

        [DisplayName("Item Title")]
        [Required(ErrorMessage = "Item Title MUST be provided")]
        [StringLength(100)]
        public string Title { get; set; }

        [DisplayName("Item Description")]
        [Required(ErrorMessage = "Item Description MUST be provided")]
        [StringLength(500)]
        public string Description { get; set; }

        [DisplayName("Price")]
        [Required(ErrorMessage = "Price MUST be provided")]
        [Range(0.01, 1000000)]
        public decimal Price { get; set; }

        public DateTime CreatedDate { get; set; }

        public string SellerId { get; set; }

        [ForeignKey("SellerId")]
        public ApplicationUser? Seller { get; set; }//navigational property
    }
}
