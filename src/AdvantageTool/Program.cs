using AdvantageTool.Data;
using AdvantageTool.Services;
using LtiAdvantage.IdentityModel.Validation;
using LtiAdvantage.Jwks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<AdvantageToolUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 4;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// Distinct cookie so this can run alongside other LTI samples on localhost.
builder.Services.ConfigureApplicationCookie(options => options.Cookie.Name = "AdvantageTool");

// Allow the tool pages to render inside an LMS iframe.
builder.Services.AddAntiforgery(options => options.SuppressXFrameOptionsHeader = true);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Platforms");
    options.Conventions.AuthorizePage("/DynamicRegistration");
    options.Conventions.AuthorizeFolder("/Proctor");
});

builder.Services.AddHttpClient();
builder.Services.AddTransient<AccessTokenService>();
builder.Services.AddControllers();
builder.Services.AddScoped<IJwksKeyStore, PlatformJwksKeyStore>();

// Launch validation. One JwksCache is shared by every launch, so the platform's JWKS is
// fetched once and refreshed when the platform rotates its key, not fetched per launch.
// It holds its HttpClient for the app's lifetime, so recycle connections to pick up DNS changes.
builder.Services.AddSingleton(new JwksCache(
    new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) }),
    cacheDuration: TimeSpan.FromHours(1),
    minimumRefreshInterval: TimeSpan.FromMinutes(1)));
builder.Services.AddSingleton<INonceStore, InMemoryNonceStore>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<LaunchStore>();
builder.Services.AddScoped<LaunchValidator>();
builder.Services.AddSingleton<ProctoringSessions>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapControllers();

// Older registrations point at a per-platform JWKS URL; every key now lives in the one JWKS.
app.MapGet("/jwks/{platformId}", () => Results.Redirect("/.well-known/jwks.json"));

app.Run();
