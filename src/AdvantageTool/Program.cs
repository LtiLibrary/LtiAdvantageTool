using AdvantageTool.Data;
using AdvantageTool.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddDbContext<StateDbContext>(options => options.UseInMemoryDatabase("LtiStates"));
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
});

builder.Services.AddHttpClient();
builder.Services.AddTransient<AccessTokenService>();
builder.Services.AddTransient<JwksService>();

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

// JWKS endpoint: the platform fetches our public key from here to verify
// JWTs signed by the tool (e.g. deep linking responses).
app.MapGet("/jwks/{platformId}", async (string platformId, JwksService svc) =>
{
    var jwks = await svc.GetJwksAsync(platformId);
    return jwks is null ? Results.NotFound() : Results.Json(jwks);
});

app.Run();
