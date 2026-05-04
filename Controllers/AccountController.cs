using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mist452SmithMayka.Data;
using Mist452SmithMayka.Models;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace Mist452SmithMayka.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApplicationDbContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _context = context;
        }

        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByNameAsync(model.UserName);

                if (existingUser != null)
                {
                    ModelState.AddModelError("UserName", "That username is already taken.");
                    return View(model);
                }

                var user = new ApplicationUser()
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    Name = model.Name,
                    StreetAddress = model.StreetAddress,
                    City = model.City,
                    State = model.State,
                    PostalCode = model.PostalCode
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Account created successfully. Please sign in.";
                    return RedirectToAction("Login");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }

        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, false);

                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError("", "Invalid username or password.");
            }

            return View(model);
        }

        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                TempData["SuccessMessage"] = "If that email exists, a password reset email was created.";
                return RedirectToAction(nameof(Login));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action(
                nameof(ResetPassword),
                "Account",
                new { userId = user.Id, token },
                Request.Scheme);

            var fakeEmail = new FakeEmailViewModel
            {
                Email = model.Email,
                ResetLink = resetLink ?? string.Empty
            };

            return View("FakeResetEmail", fakeEmail);
        }

        [AllowAnonymous]
        public IActionResult ResetPassword(string userId, string token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                return BadRequest();
            }

            var model = new ResetPasswordViewModel
            {
                UserId = userId,
                Token = token
            };

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);

            if (user == null)
            {
                return NotFound();
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Password reset successfully. Please sign in.";
                return RedirectToAction(nameof(Login));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        [Authorize]
        public async Task<IActionResult> PaymentInfo()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var claims = await _userManager.GetClaimsAsync(user);
            var viewModel = new PaymentInfoViewModel
            {
                HasPaymentInfo = claims.Any(c => c.Type == "PaymentInfoAdded" && c.Value == "true"),
                StripeCustomerId = claims.FirstOrDefault(c => c.Type == "StripeCustomerId")?.Value
            };

            return View(viewModel);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPaymentInfo()
        {
            var stripeSecretKey = _configuration["Stripe:SecretKey"];

            if (string.IsNullOrWhiteSpace(stripeSecretKey))
            {
                TempData["ErrorMessage"] = "Stripe is not configured yet. Add your secret key to Stripe:SecretKey.";
                return RedirectToAction(nameof(PaymentInfo));
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var claims = await _userManager.GetClaimsAsync(user);
            var stripeCustomerId = claims.FirstOrDefault(c => c.Type == "StripeCustomerId")?.Value;

            if (string.IsNullOrWhiteSpace(stripeCustomerId))
            {
                stripeCustomerId = await CreateStripeCustomerAsync(user, stripeSecretKey);

                if (string.IsNullOrWhiteSpace(stripeCustomerId))
                {
                    TempData["ErrorMessage"] = "Unable to create a Stripe customer.";
                    return RedirectToAction(nameof(PaymentInfo));
                }

                await AddOrUpdateClaimAsync(user, "StripeCustomerId", stripeCustomerId);
            }

            var checkoutUrl = await CreateStripeSetupSessionAsync(stripeCustomerId, stripeSecretKey);

            if (string.IsNullOrWhiteSpace(checkoutUrl))
            {
                TempData["ErrorMessage"] = "Unable to start Stripe payment setup.";
                return RedirectToAction(nameof(PaymentInfo));
            }

            return Redirect(checkoutUrl);
        }

        [Authorize]
        public async Task<IActionResult> PaymentInfoSuccess()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            await AddOrUpdateClaimAsync(user, "PaymentInfoAdded", "true");
            await _signInManager.RefreshSignInAsync(user);

            TempData["SuccessMessage"] = "Payment info added successfully.";
            return RedirectToAction(nameof(PaymentInfo));
        }

        [Authorize]
        public IActionResult PaymentInfoCancel()
        {
            TempData["ErrorMessage"] = "Payment info setup was canceled.";
            return RedirectToAction(nameof(PaymentInfo));
        }

        [Authorize]
        public async Task<IActionResult> EditAccount()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var model = new EditAccountViewModel
            {
                Name = user.Name,
                StreetAddress = user.StreetAddress,
                City = user.City,
                State = user.State,
                PostalCode = user.PostalCode
            };

            ViewData["UserName"] = user.UserName;
            ViewData["Email"] = user.Email;

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAccount(EditAccountViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            ViewData["UserName"] = user.UserName;
            ViewData["Email"] = user.Email;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            user.Name = model.Name;
            user.StreetAddress = model.StreetAddress;
            user.City = model.City;
            user.State = model.State;
            user.PostalCode = model.PostalCode;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Account updated successfully.";
                return RedirectToAction(nameof(EditAccount));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public async Task<IActionResult> Details()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var orders = await _context.Listings
                .Include(l => l.Seller)
                .Where(l => l.BuyerId == user.Id)
                .OrderByDescending(l => l.SoldDate)
                .ToListAsync();

            ViewData["UserName"] = user.UserName;
            ViewData["Email"] = user.Email;
            ViewData["Name"] = user.Name;
            ViewData["City"] = user.City;
            ViewData["State"] = user.State;

            return View(orders);
        }

        [Authorize]
        public async Task<IActionResult> OrderDetail(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var listing = await _context.Listings
                .Include(l => l.Seller)
                .FirstOrDefaultAsync(l => l.ListingId == id && l.BuyerId == userId);

            if (listing == null) return NotFound();

            return View(listing);
        }

        private async Task AddOrUpdateClaimAsync(ApplicationUser user, string claimType, string claimValue)
        {
            var claims = await _userManager.GetClaimsAsync(user);
            var existingClaim = claims.FirstOrDefault(c => c.Type == claimType);
            var newClaim = new Claim(claimType, claimValue);

            if (existingClaim == null)
            {
                await _userManager.AddClaimAsync(user, newClaim);
                return;
            }

            await _userManager.ReplaceClaimAsync(user, existingClaim, newClaim);
        }

        private async Task<string?> CreateStripeCustomerAsync(ApplicationUser user, string stripeSecretKey)
        {
            var formValues = new List<KeyValuePair<string, string>>
            {
                new("email", user.Email ?? string.Empty),
                new("name", user.Name)
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/customers")
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

            if (json.RootElement.TryGetProperty("id", out var customerId))
            {
                return customerId.GetString();
            }

            return null;
        }

        private async Task<string?> CreateStripeSetupSessionAsync(string stripeCustomerId, string stripeSecretKey)
        {
            var successUrl = Url.Action(nameof(PaymentInfoSuccess), "Account", null, Request.Scheme);
            var cancelUrl = Url.Action(nameof(PaymentInfoCancel), "Account", null, Request.Scheme);
            var formValues = new List<KeyValuePair<string, string>>
            {
                new("mode", "setup"),
                new("customer", stripeCustomerId),
                new("success_url", successUrl ?? string.Empty),
                new("cancel_url", cancelUrl ?? string.Empty)
            };

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
