using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mist452SmithMayka.Data;
using Mist452SmithMayka.Models;

namespace Mist452SmithMayka
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddHttpClient();
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession();

            var connString = builder.Configuration.GetConnectionString("DefaultConnection");

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    options.UseSqlite(connString);
                }
                else
                {
                    options.UseSqlServer(connString);
                }
            });

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

            builder.Services.AddRazorPages();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                dbContext.Database.EnsureCreated();
                EnsureListingSoldColumnsAsync(dbContext).GetAwaiter().GetResult();

                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                SeedAdminAsync(roleManager, userManager).GetAwaiter().GetResult();
                SeedDemoDataAsync(dbContext, userManager).GetAwaiter().GetResult();
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseSession();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapRazorPages();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }

        private static async Task SeedAdminAsync(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
        {
            const string adminRole = "Admin";
            const string adminUserName = "admin";
            const string adminEmail = "admin@example.com";
            const string adminPassword = "Admin123!";

            if (!await roleManager.RoleExistsAsync(adminRole))
            {
                await roleManager.CreateAsync(new IdentityRole(adminRole));
            }

            var adminUser = await userManager.FindByNameAsync(adminUserName);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminUserName,
                    Email = adminEmail,
                    Name = "Site Admin"
                };

                await userManager.CreateAsync(adminUser, adminPassword);
            }

            if (!await userManager.IsInRoleAsync(adminUser, adminRole))
            {
                await userManager.AddToRoleAsync(adminUser, adminRole);
            }
        }

        private static async Task EnsureListingSoldColumnsAsync(ApplicationDbContext dbContext)
        {
            if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                await using var connection = dbContext.Database.GetDbConnection();
                await connection.OpenAsync();

                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA table_info(Listings);";
                    await using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        existingColumns.Add(reader.GetString(1));
                    }
                }

                if (!existingColumns.Contains("IsSold"))
                {
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE Listings ADD COLUMN IsSold INTEGER NOT NULL DEFAULT 0;");
                }

                if (!existingColumns.Contains("SoldDate"))
                {
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE Listings ADD COLUMN SoldDate TEXT NULL;");
                }

                if (!existingColumns.Contains("BuyerId"))
                {
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE Listings ADD COLUMN BuyerId TEXT NULL;");
                }

                return;
            }

            if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    "IF COL_LENGTH('Listings', 'IsSold') IS NULL ALTER TABLE Listings ADD IsSold bit NOT NULL DEFAULT 0;");
                await dbContext.Database.ExecuteSqlRawAsync(
                    "IF COL_LENGTH('Listings', 'SoldDate') IS NULL ALTER TABLE Listings ADD SoldDate datetime2 NULL;");
                await dbContext.Database.ExecuteSqlRawAsync(
                    "IF COL_LENGTH('Listings', 'BuyerId') IS NULL ALTER TABLE Listings ADD BuyerId nvarchar(450) NULL;");
            }
        }

        private static async Task SeedDemoDataAsync(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            var demoUsers = new[]
            {
                new ApplicationUser
                {
                    UserName = "alex",
                    Email = "alex@example.com",
                    Name = "Alex Turner",
                    City = "Raleigh",
                    State = "NC"
                },
                new ApplicationUser
                {
                    UserName = "morgan",
                    Email = "morgan@example.com",
                    Name = "Morgan Lee",
                    City = "Charlotte",
                    State = "NC"
                },
                new ApplicationUser
                {
                    UserName = "casey",
                    Email = "casey@example.com",
                    Name = "Casey Rivera",
                    City = "Durham",
                    State = "NC"
                }
            };

            foreach (var demoUser in demoUsers)
            {
                var existingUser = await userManager.FindByNameAsync(demoUser.UserName!);

                if (existingUser == null)
                {
                    await userManager.CreateAsync(demoUser, "Password123!");
                }
            }

            var alex = await userManager.FindByNameAsync("alex");
            var morgan = await userManager.FindByNameAsync("morgan");
            var casey = await userManager.FindByNameAsync("casey");

            var demoListings = new[]
            {
                new Listing
                {
                    Title = "Graphing Calculator",
                    Description = "TI-84 calculator in good condition with fresh batteries.",
                    Price = 45.00m,
                    CreatedDate = DateTime.Now.AddDays(-5),
                    SellerId = alex!.Id
                },
                new Listing
                {
                    Title = "Desk Lamp",
                    Description = "Adjustable LED desk lamp for a dorm or study space.",
                    Price = 18.00m,
                    CreatedDate = DateTime.Now.AddDays(-4),
                    SellerId = morgan!.Id
                },
                new Listing
                {
                    Title = "Mini Fridge",
                    Description = "Compact mini fridge, clean and works well.",
                    Price = 85.00m,
                    CreatedDate = DateTime.Now.AddDays(-3),
                    SellerId = casey!.Id
                },
                new Listing
                {
                    Title = "Intro Marketing Textbook",
                    Description = "Used textbook with light highlighting.",
                    Price = 32.50m,
                    CreatedDate = DateTime.Now.AddDays(-2),
                    SellerId = alex.Id
                },
                new Listing
                {
                    Title = "Bluetooth Speaker",
                    Description = "Portable speaker with charger included.",
                    Price = 24.99m,
                    CreatedDate = DateTime.Now.AddDays(-1),
                    SellerId = morgan.Id,
                    IsSold = true,
                    SoldDate = DateTime.Now.AddHours(-8),
                    BuyerId = casey.Id
                }
            };

            foreach (var listing in demoListings)
            {
                var listingExists = await dbContext.Listings.AnyAsync(l => l.Title == listing.Title);

                if (!listingExists)
                {
                    dbContext.Listings.Add(listing);
                }
            }

            await dbContext.SaveChangesAsync();
        }
    }
}
