using eBook_Library_Service.Data;
using eBook_Library_Service.Models;
using eBook_Library_Service.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<ShoppingCartService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<PayPalService>();
builder.Services.AddSingleton<StripeService>();
builder.Services.AddTransient<EmailService>();
builder.Services.AddHostedService<BorrowExpiryBackgroundService>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Add session support (if needed for shopping cart)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddIdentity<Users, IdentityRole>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Define the "AdminOnly" policy
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

// Validate PayPal configuration
var payPalClientId = builder.Configuration["PayPal:ClientId"];
var payPalClientSecret = builder.Configuration["PayPal:ClientSecret"];
if (string.IsNullOrEmpty(payPalClientId) || string.IsNullOrEmpty(payPalClientSecret))
{
    throw new InvalidOperationException("PayPal ClientId or ClientSecret is missing in appsettings.json.");
}

// Call the SeedAdminUserAsync method after building the application
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    var userManager = serviceProvider.GetRequiredService<UserManager<Users>>();
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    // Seed the admin user and roles
    await SeedAdminUserAsync(userManager, roleManager);

    // Add additional logic for checking the user
    var user = await userManager.FindByEmailAsync("admin@orig.il");
    if (user != null)
    {
        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Contains("Admin"))
        {
            await userManager.AddToRoleAsync(user, "Admin");
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts(); // Enable HTTP Strict Transport Security (HSTS)
}

app.UseHttpsRedirection(); // Ensure HTTPS redirection is enabled
app.UseStaticFiles();
app.UseSession(); // Enable session support

// Add status code pages middleware BEFORE routing
app.UseStatusCodePages(async context =>
{
    if (context.HttpContext.Response.StatusCode == 403)
    {
        context.HttpContext.Response.Redirect("/Account/AccessDenied");
    }
});

app.UseRouting();
app.UseAuthentication(); // Ensure authentication is called before authorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

async Task SeedAdminUserAsync(UserManager<Users> userManager, RoleManager<IdentityRole> roleManager)
{
    const string adminEmail = "admin@orig.il";
    const string adminPassword = "Admin@123";

    // Check if the admin role exists, if not, create it
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }

    // Check if the admin user exists, if not, create it
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var adminUser = new Users
        {
            FullName = "Admin User",
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
        else
        {
            Console.WriteLine($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
}