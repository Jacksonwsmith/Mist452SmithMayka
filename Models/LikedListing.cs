using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mist452SmithMayka.Models
{
    public class LikedListing
    {
        [Key]
        public int LikedListingId { get; set; }

        public string UserId { get; set; }

        public int ListingId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        [ForeignKey("ListingId")]
        public Listing? Listing { get; set; }
    }
}
