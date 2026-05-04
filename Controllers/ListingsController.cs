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
                .Where(l => !l.IsSold)
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
                .Include(l => l.Buyer)
                .FirstOrDefaultAsync(l => l.ListingId == id);

            if (listing == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (userId != null)
            {
                ViewData["AlreadyLiked"] = await _context.LikedListings
                    .AnyAsync(ll => ll.UserId == userId && ll.ListingId == id);
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

        [Authorize]
        public async Task<IActionResult> EditListing(int? id)
        {
            if (id == null) return NotFound();

            var listing = await _context.Listings.FindAsync(id);
            if (listing == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (listing.SellerId != userId) return Forbid();

            return View(listing);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> EditListing(int id, [Bind("ListingId,Title,Description,Price")] Listing listing)
        {
            if (id != listing.ListingId) return NotFound();

            var existing = await _context.Listings.FindAsync(id);
            if (existing == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (existing.SellerId != userId) return Forbid();

            ModelState.Remove(nameof(Listing.SellerId));

            if (!ModelState.IsValid) return View(listing);

            existing.Title = listing.Title;
            existing.Description = listing.Description;
            existing.Price = listing.Price;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Listing updated successfully.";
            return RedirectToAction(nameof(MyListings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteListing(int id)
        {
            var listing = await _context.Listings.FindAsync(id);
            if (listing == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (listing.SellerId != userId) return Forbid();

            var likedListings = await _context.LikedListings
                .Where(ll => ll.ListingId == id)
                .ToListAsync();
            _context.LikedListings.RemoveRange(likedListings);

            _context.Listings.Remove(listing);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Listing deleted successfully.";
            return RedirectToAction(nameof(MyListings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Like(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return Challenge();
            }

            var listing = await _context.Listings.FindAsync(id);
            if (listing == null)
            {
                return NotFound();
            }

            var alreadyLiked = await _context.LikedListings
                .AnyAsync(ll => ll.UserId == userId && ll.ListingId == id);

            if (alreadyLiked)
            {
                TempData["ErrorMessage"] = "You have already liked this listing.";
            }
            else
            {
                _context.LikedListings.Add(new Mist452SmithMayka.Models.LikedListing
                {
                    UserId = userId,
                    ListingId = id
                });
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Listing added to your liked listings!";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Unlike(int id, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return Challenge();
            }

            var likedListing = await _context.LikedListings
                .FirstOrDefaultAsync(ll => ll.UserId == userId && ll.ListingId == id);

            if (likedListing == null)
            {
                TempData["ErrorMessage"] = "That listing was not in your liked listings.";
            }
            else
            {
                _context.LikedListings.Remove(likedListing);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Listing removed from your liked listings.";
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize]
        public async Task<IActionResult> MyLikedListings()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return Challenge();
            }

            var likedListings = await _context.LikedListings
                .Include(ll => ll.Listing)
                    .ThenInclude(l => l!.Seller)
                .Where(ll => ll.UserId == userId)
                .OrderByDescending(ll => ll.LikedListingId)
                .Select(ll => ll.Listing)
                .Where(l => l != null)
                .ToListAsync();

            return View(likedListings);
        }
    }
}
