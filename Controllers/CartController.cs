using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mist452SmithMayka.Data;
using Mist452SmithMayka.Models;

namespace Mist452SmithMayka.Controllers
{
    public class CartController : Controller
    {
        private const string CartSessionKey = "CartListingIds";
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var cartIds = GetCartIds();
            var listings = await _context.Listings
                .Include(l => l.Seller)
                .Where(l => cartIds.Contains(l.ListingId))
                .ToListAsync();

            ViewBag.Total = listings.Sum(l => l.Price);
            return View(listings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(int listingId, string? returnUrl)
        {
            var cartIds = GetCartIds();

            if (!cartIds.Contains(listingId))
            {
                cartIds.Add(listingId);
                SaveCartIds(cartIds);
                TempData["SuccessMessage"] = "Item added to cart.";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(int listingId)
        {
            var cartIds = GetCartIds();
            cartIds.Remove(listingId);
            SaveCartIds(cartIds);

            TempData["SuccessMessage"] = "Item removed from cart.";
            return RedirectToAction(nameof(Index));
        }

        private List<int> GetCartIds()
        {
            var cartValue = HttpContext.Session.GetString(CartSessionKey);

            if (string.IsNullOrWhiteSpace(cartValue))
            {
                return new List<int>();
            }

            return cartValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList();
        }

        private void SaveCartIds(List<int> cartIds)
        {
            HttpContext.Session.SetString(CartSessionKey, string.Join(",", cartIds));
        }
    }
}
