using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mist452SmithMayka.Data;
using Mist452SmithMayka.Models;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace Mist452SmithMayka.Controllers
{
    public class CartController : Controller
    {
        private const string CartSessionKey = "CartListingIds";
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public CartController(ApplicationDbContext context, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Index()
        {
            var cartIds = GetCartIds();
            var listings = await _context.Listings
                .Include(l => l.Seller)
                .Where(l => cartIds.Contains(l.ListingId) && !l.IsSold)
                .ToListAsync();

            SaveCartIds(listings.Select(l => l.ListingId).ToList());
            ViewBag.Total = listings.Sum(l => l.Price);
            return View(listings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(int listingId, string? returnUrl)
        {
            var cartIds = GetCartIds();
            var listingIsAvailable = _context.Listings.Any(l => l.ListingId == listingId && !l.IsSold);

            if (!listingIsAvailable)
            {
                TempData["ErrorMessage"] = "That listing is already sold.";
            }
            else if (!cartIds.Contains(listingId))
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout()
        {
            var stripeSecretKey = _configuration["Stripe:SecretKey"];

            if (string.IsNullOrWhiteSpace(stripeSecretKey))
            {
                TempData["ErrorMessage"] = "Stripe is not configured yet. Add your secret key to Stripe:SecretKey.";
                return RedirectToAction(nameof(Index));
            }

            var cartIds = GetCartIds();
            var listings = await _context.Listings
                .Where(l => cartIds.Contains(l.ListingId) && !l.IsSold)
                .ToListAsync();

            if (!listings.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty.";
                return RedirectToAction(nameof(Index));
            }

            var checkoutUrl = await CreateStripeCheckoutSessionAsync(listings, stripeSecretKey);

            if (string.IsNullOrWhiteSpace(checkoutUrl))
            {
                TempData["ErrorMessage"] = "Unable to start Stripe checkout.";
                return RedirectToAction(nameof(Index));
            }

            return Redirect(checkoutUrl);
        }

        public async Task<IActionResult> CheckoutSuccess()
        {
            var cartIds = GetCartIds();
            var buyerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var listings = await _context.Listings
                .Where(l => cartIds.Contains(l.ListingId) && !l.IsSold)
                .ToListAsync();

            foreach (var listing in listings)
            {
                listing.IsSold = true;
                listing.SoldDate = DateTime.Now;
                listing.BuyerId = buyerId;
            }

            await _context.SaveChangesAsync();
            HttpContext.Session.Remove(CartSessionKey);
            TempData["SuccessMessage"] = "Payment completed successfully.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult CheckoutCancel()
        {
            TempData["ErrorMessage"] = "Checkout was canceled.";
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

        private async Task<string?> CreateStripeCheckoutSessionAsync(List<Listing> listings, string stripeSecretKey)
        {
            var successUrl = Url.Action(nameof(CheckoutSuccess), "Cart", null, Request.Scheme);
            var cancelUrl = Url.Action(nameof(CheckoutCancel), "Cart", null, Request.Scheme);
            var formValues = new List<KeyValuePair<string, string>>
            {
                new("mode", "payment"),
                new("success_url", successUrl ?? string.Empty),
                new("cancel_url", cancelUrl ?? string.Empty)
            };

            for (var i = 0; i < listings.Count; i++)
            {
                var listing = listings[i];
                var amountInCents = (long)Math.Round(listing.Price * 100, MidpointRounding.AwayFromZero);

                formValues.Add(new($"line_items[{i}][price_data][currency]", "usd"));
                formValues.Add(new($"line_items[{i}][price_data][product_data][name]", listing.Title));
                formValues.Add(new($"line_items[{i}][price_data][unit_amount]", amountInCents.ToString()));
                formValues.Add(new($"line_items[{i}][quantity]", "1"));
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions")
            {
                Content = new FormUrlEncodedContent(formValues)
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", stripeSecretKey);
            request.Headers.Add("Stripe-Version", _configuration["Stripe:ApiVersion"] ?? "2026-02-25.clover");

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(responseStream);

            if (json.RootElement.TryGetProperty("url", out var url))
            {
                return url.GetString();
            }

            return null;
        }
    }
}
