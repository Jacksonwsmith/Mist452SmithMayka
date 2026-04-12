using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Mist452SmithMayka.Data;
using Mist452SmithMayka.Models;

namespace Mist452SmithMayka.Controllers
{
    [Authorize]
    public class ListingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ListingsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Listing listing)
        {
            if (ModelState.IsValid)
            {
                listing.SellerId = _userManager.GetUserId(User);
                listing.CreatedDate = DateTime.Now;

                _context.Listings.Add(listing);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Listing created successfully.";
                return RedirectToAction("Index", "Home");
            }

            return View(listing);
        }
    }
}
