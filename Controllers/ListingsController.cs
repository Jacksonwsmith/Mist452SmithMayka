using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mist452SmithMayka.Data;
using Mist452SmithMayka.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Mist452SmithMayka.Controllers
{
    public class ListingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ListingsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var listings = await _context.Listings
                .Include(l => l.Seller)
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();

            return View(listings);
        }

        [Authorize]
        public async Task<IActionResult> MyListings()
        {
            var userId = _userManager.GetUserId(User);

            var myListings = await _context.Listings
                .Include(l => l.Seller)
                .Where(l => l.SellerId == userId)
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();

            return View(myListings);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var listing = await _context.Listings
                .Include(l => l.Seller)
                .FirstOrDefaultAsync(l => l.ListingId == id);

            if (listing == null)
            {
                return NotFound();
            }

            return View(listing);
        }
    }
}
