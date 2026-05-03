using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mist452SmithMayka.Data;
using Mist452SmithMayka.Models;

namespace Mist452SmithMayka.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new AdminDashboardViewModel
            {
                UserCount = await _userManager.Users.CountAsync(),
                ListingCount = await _context.Listings.CountAsync()
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            var viewModel = new List<AdminUserViewModel>();

            foreach (var user in users)
            {
                viewModel.Add(new AdminUserViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Name = user.Name,
                    IsAdmin = await _userManager.IsInRoleAsync(user, "Admin")
                });
            }

            return View(viewModel);
        }

        public async Task<IActionResult> Listings()
        {
            var listings = await _context.Listings
                .Include(l => l.Seller)
                .Include(l => l.Buyer)
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();

            return View(listings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var currentUserId = _userManager.GetUserId(User);

            if (id == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot delete the admin account you are signed in with.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            var userListings = await _context.Listings
                .Where(l => l.SellerId == id)
                .ToListAsync();

            _context.Listings.RemoveRange(userListings);
            await _context.SaveChangesAsync();

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "User deleted successfully.";
                return RedirectToAction(nameof(Users));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteListing(int id)
        {
            var listing = await _context.Listings.FindAsync(id);

            if (listing == null)
            {
                return NotFound();
            }

            _context.Listings.Remove(listing);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Listing deleted successfully.";
            return RedirectToAction(nameof(Listings));
        }
    }
}
