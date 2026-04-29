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

        [AllowAnonymous]
        public async Task<IActionResult> Index(string? searchTerm)
        {
            var listingsQuery = _context.Listings
                .Include(l => l.Seller)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var normalizedSearchTerm = searchTerm.ToLower();
                listingsQuery = listingsQuery.Where(l => l.Title.ToLower().Contains(normalizedSearchTerm));
            }

            var listings = await listingsQuery
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();

            ViewData["SearchTerm"] = searchTerm;
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

        [AllowAnonymous]
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

        [Authorize]
        public IActionResult Create()
        {
            return View(new Listing());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create([Bind("Title,Description,Price")] Listing listing)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return Challenge();
            }

            listing.SellerId = userId;
            listing.CreatedDate = DateTime.Now;
            ModelState.Remove(nameof(Listing.SellerId));

            if (!ModelState.IsValid)
            {
                return View(listing);
            }

            _context.Listings.Add(listing);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Listing created successfully.";
            return RedirectToAction(nameof(MyListings));
        }
    }
}
