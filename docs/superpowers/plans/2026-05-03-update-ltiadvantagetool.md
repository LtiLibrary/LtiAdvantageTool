# Update LtiAdvantageTool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring the LtiAdvantageTool sample current with LtiAdvantage 3.0.0 / .NET 10, runnable on macOS, demonstrating launch / deep linking / AGS / NRPS / JWKS.

**Architecture:** Single ASP.NET Core 10 Razor Pages web app. Identity (user accounts) on SQLite. In-memory state context for OIDC nonces. Per-platform RSA key pair (PEM in DB). Minimal-API JWKS endpoint exposes the tool's public key. Pages: home, register/login (Identity UI), platforms CRUD, OidcLogin (launch initiation), Tool (launch handler + AGS/NRPS UI), Catalog (deep linking).

**Tech Stack:** .NET 10, ASP.NET Core 10 Razor Pages, EF Core 10 SQLite, ASP.NET Core Identity, LtiAdvantage 3.0.0, LtiAdvantage.IdentityModel 3.0.0 (transitively pulls IdentityModel 7), System.Text.Json, Bootstrap 5 (default template).

---

## Task 1: Reset src/ and scaffold a fresh net10.0 web app

**Files:**
- Delete: `src/AdvantageTool.csproj`, `src/Startup.cs`, `src/Program.cs`, `src/appsettings*.json`, `src/bundleconfig.json`, `src/libman.json`, `src/Properties/`, `src/Areas/`, `src/Pages/`, `src/Data/`, `src/Utility/`, `src/wwwroot/`, `src/Connected Services/`
- Modify: `AdvantageTool.sln` (will be regenerated)

- [ ] Step 1: Delete the old src content
```bash
cd /Users/sanderrijken/LtiAdvantageTool
git rm -rf src AdvantageTool.sln
```

- [ ] Step 2: Scaffold a fresh webapp with Identity + SQLite
```bash
dotnet new webapp -n AdvantageTool -o src/AdvantageTool --auth Individual --use-local-db false -f net10.0
dotnet new sln -n AdvantageTool
dotnet sln AdvantageTool.sln add src/AdvantageTool/AdvantageTool.csproj
```
Expected: project compiles with `dotnet build`.

- [ ] Step 3: Switch the scaffolded project to SQLite
The `--auth Individual` template defaults to SQLite when `--use-local-db false` is passed. Verify `appsettings.json` has `Data Source=AdvantageTool.db` and the csproj references `Microsoft.EntityFrameworkCore.Sqlite`.

- [ ] Step 4: Add gitignore entries for SQLite files
Append to `.gitignore`:
```
*.db
*.db-shm
*.db-wal
```

- [ ] Step 5: Build and commit
```bash
dotnet build
git add -A
git commit -m "Reset to fresh net10.0 webapp with Identity + SQLite"
```
Expected: build succeeds; clean baseline.

---

## Task 2: Add LtiAdvantage NuGet packages

**Files:**
- Modify: `src/AdvantageTool/AdvantageTool.csproj`

- [ ] Step 1: Add packages
```bash
cd src/AdvantageTool
dotnet add package LtiAdvantage --version 3.0.0
dotnet add package LtiAdvantage.IdentityModel --version 3.0.0
```

- [ ] Step 2: Build, commit
```bash
cd /Users/sanderrijken/LtiAdvantageTool
dotnet build
git add src/AdvantageTool/AdvantageTool.csproj
git commit -m "Reference LtiAdvantage 3.0.0 NuGet packages"
```

---

## Task 3: Domain types (AdvantageToolUser, Platform, State)

**Files:**
- Create: `src/AdvantageTool/Data/AdvantageToolUser.cs`
- Create: `src/AdvantageTool/Data/Platform.cs`
- Create: `src/AdvantageTool/Data/State.cs`

- [ ] Step 1: Write `Data/AdvantageToolUser.cs`
```csharp
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace AdvantageTool.Data;

public class AdvantageToolUser : IdentityUser
{
    public ICollection<Platform> Platforms { get; set; } = new List<Platform>();
}
```

- [ ] Step 2: Write `Data/Platform.cs`
```csharp
namespace AdvantageTool.Data;

public class Platform
{
    public int Id { get; set; }

    // Platform side
    public string Name { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string AuthorizeUrl { get; set; } = string.Empty;
    public string AccessTokenUrl { get; set; } = string.Empty;
    public string JwkSetUrl { get; set; } = string.Empty;
    public string PlatformId { get; set; } = string.Empty;

    // Tool side
    public string ClientId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
    public AdvantageToolUser? User { get; set; }
}
```

- [ ] Step 3: Write `Data/State.cs`
```csharp
namespace AdvantageTool.Data;

public class State
{
    public int Id { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
```

- [ ] Step 4: Build, commit
```bash
dotnet build
git add src/AdvantageTool/Data
git commit -m "Add Platform, State, and AdvantageToolUser domain types"
```

---

## Task 4: Replace template DbContext with our two contexts

**Files:**
- Delete: `src/AdvantageTool/Data/ApplicationDbContext.cs` (template version)
- Create: `src/AdvantageTool/Data/ApplicationDbContext.cs` (ours)
- Create: `src/AdvantageTool/Data/StateDbContext.cs`
- Modify: `src/AdvantageTool/Program.cs`

- [ ] Step 1: Replace `Data/ApplicationDbContext.cs`
```csharp
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Data;

public class ApplicationDbContext : IdentityDbContext<AdvantageToolUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Platform> Platforms => Set<Platform>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Platform>().HasIndex(p => p.PlatformId).IsUnique();
        builder.Entity<Platform>().HasIndex(p => p.Issuer).IsUnique();
    }

    public Task<AdvantageToolUser?> GetUserAsync(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return id is null ? Task.FromResult<AdvantageToolUser?>(null)
            : Users.Include(u => u.Platforms).SingleOrDefaultAsync(u => u.Id == id);
    }

    public Task<Platform?> GetPlatformByIssuerAsync(string issuer)
        => Platforms.SingleOrDefaultAsync(p => p.Issuer == issuer);

    public Task<Platform?> GetPlatformByPlatformIdAsync(string platformId)
        => Platforms.SingleOrDefaultAsync(p => p.PlatformId == platformId);
}
```

- [ ] Step 2: Create `Data/StateDbContext.cs`
```csharp
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Data;

public class StateDbContext : DbContext
{
    public StateDbContext(DbContextOptions<StateDbContext> options) : base(options) { }
    public DbSet<State> States => Set<State>();

    public void AddState(string nonce, string value)
    {
        States.Add(new State { Nonce = nonce, Value = value });
        SaveChanges();
    }

    public State? GetState(string nonce)
        => States.AsNoTracking().FirstOrDefault(s => s.Nonce == nonce);
}
```

- [ ] Step 3: Update `Program.cs` to register `StateDbContext` (in-memory)
Add after `AddDbContext<ApplicationDbContext>(...)`:
```csharp
builder.Services.AddDbContext<StateDbContext>(options =>
    options.UseInMemoryDatabase("LtiStates"));
```
And add the in-memory provider to the csproj:
```bash
dotnet add src/AdvantageTool package Microsoft.EntityFrameworkCore.InMemory
```

- [ ] Step 4: Build, commit
```bash
dotnet build
git add -A
git commit -m "Replace template DbContext with ApplicationDbContext + StateDbContext"
```

---

## Task 5: Drop template migrations and create a fresh InitialCreate

**Files:**
- Delete: `src/AdvantageTool/Data/Migrations/*` (template's)
- Create: fresh migration via `dotnet ef`

- [ ] Step 1: Remove the template migration folder
```bash
rm -rf src/AdvantageTool/Data/Migrations
```

- [ ] Step 2: Add a fresh migration
```bash
dotnet ef migrations add InitialCreate --project src/AdvantageTool --output-dir Data/Migrations
```
If `dotnet ef` is not installed: `dotnet tool install --global dotnet-ef --version 10.*`.

- [ ] Step 3: Auto-migrate on startup. Edit `Program.cs` so that after `var app = builder.Build();`:
```csharp
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}
```

- [ ] Step 4: Build, run briefly to confirm DB creation, commit
```bash
dotnet build
git add -A
git commit -m "Generate SQLite InitialCreate migration; auto-migrate on startup"
```

---

## Task 6: PemHelper utility

**Files:**
- Create: `src/AdvantageTool/Utility/PemHelper.cs`

- [ ] Step 1: Write `Utility/PemHelper.cs`
```csharp
using System;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Utility;

public static class PemHelper
{
    public sealed record RsaKeyPair(string PrivateKey, string PublicKey, string KeyId);

    public static RsaKeyPair GenerateRsaKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return new RsaKeyPair(
            rsa.ExportRSAPrivateKeyPem(),
            rsa.ExportSubjectPublicKeyInfoPem(),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant());
    }

    public static SigningCredentials SigningCredentialsFromPem(string privateKeyPem, string keyId)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var key = new RsaSecurityKey(rsa) { KeyId = keyId };
        return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }

    public static RSAParameters PublicParametersFromPem(string publicKeyPem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        return rsa.ExportParameters(false);
    }
}
```

- [ ] Step 2: Build, commit
```bash
dotnet build
git add src/AdvantageTool/Utility
git commit -m "Add PemHelper using built-in RSA APIs"
```

---

## Task 7: Identity user replacement + Layout cleanup

**Files:**
- Delete: scaffolded `src/AdvantageTool/Areas/Identity/Data/AdvantageToolUser.cs` (template-generated)
- Modify: `src/AdvantageTool/Program.cs` (point to our `AdvantageToolUser` in `Data/`)
- Delete: `src/AdvantageTool/Areas/Identity/Pages/_ViewStart.cshtml` if it references the wrong namespace

The `dotnet new webapp --auth Individual` template creates an `IdentityUser` subclass under `Areas/Identity/Data/`. We replace it with the one in `Data/`.

- [ ] Step 1: Inspect `src/AdvantageTool/Areas/Identity/Data/` and delete the template's user class file
```bash
ls src/AdvantageTool/Areas/Identity/Data 2>/dev/null
rm -rf src/AdvantageTool/Areas/Identity/Data
```

- [ ] Step 2: Inspect `Program.cs`. The template should read approximately:
```csharp
builder.Services.AddDefaultIdentity<AdvantageToolUser>(...)
    .AddEntityFrameworkStores<ApplicationDbContext>();
```
Make sure the `using AdvantageTool.Data;` line is present at the top (Identity uses our user class).

- [ ] Step 3: Loosen password rules (this is a sample) — replace the `AddDefaultIdentity<AdvantageToolUser>(...)` call:
```csharp
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
```

- [ ] Step 4: Set unique cookie name so it can run alongside other LTI samples
After the `AddDefaultIdentity` block:
```csharp
builder.Services.ConfigureApplicationCookie(options => options.Cookie.Name = "AdvantageTool");
```

- [ ] Step 5: Suppress `X-Frame-Options` so the tool can render inside an LMS iframe
```csharp
builder.Services.AddAntiforgery(options => options.SuppressXFrameOptionsHeader = true);
```

- [ ] Step 6: Re-add the migration (model changed: user is now ours)
```bash
rm -rf src/AdvantageTool/Data/Migrations
dotnet ef migrations add InitialCreate --project src/AdvantageTool --output-dir Data/Migrations
```

- [ ] Step 7: Build, commit
```bash
dotnet build
rm -f src/AdvantageTool/AdvantageTool.db*
git add -A
git commit -m "Wire AdvantageToolUser into Identity; relax password rules; cookie + iframe friendly"
```

---

## Task 8: AccessTokenService

**Files:**
- Create: `src/AdvantageTool/Services/AccessTokenService.cs`
- Modify: `src/AdvantageTool/Program.cs`

- [ ] Step 1: Write `Services/AccessTokenService.cs`
```csharp
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using IdentityModel;
using IdentityModel.Client;
using LtiAdvantage.IdentityModel.Client;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Services;

public class AccessTokenService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public AccessTokenService(ApplicationDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<TokenResponse> GetAccessTokenAsync(string issuer, string scope)
    {
        if (string.IsNullOrWhiteSpace(issuer))
            return new TokenResponse { Error = "Missing issuer" };
        if (string.IsNullOrWhiteSpace(scope))
            return new TokenResponse { Error = "Missing scope" };

        var platform = await _context.GetPlatformByIssuerAsync(issuer);
        if (platform is null)
            return new TokenResponse { Error = "Platform not registered" };

        var now = DateTime.UtcNow;
        var payload = new JwtPayload();
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, platform.ClientId));
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, platform.ClientId));
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Aud, platform.AccessTokenUrl));
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString()));
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Nbf, EpochTime.GetIntDate(now.AddSeconds(-5)).ToString()));
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Exp, EpochTime.GetIntDate(now.AddMinutes(5)).ToString()));
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, CryptoRandom.CreateUniqueId()));

        var credentials = PemHelper.SigningCredentialsFromPem(platform.PrivateKey, platform.KeyId);
        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));

        var http = _httpClientFactory.CreateClient();
        return await http.RequestClientCredentialsTokenWithJwtAsync(new JwtClientCredentialsTokenRequest
        {
            Address = platform.AccessTokenUrl,
            ClientId = platform.ClientId,
            Jwt = jwt,
            Scope = scope
        });
    }
}
```

- [ ] Step 2: Register in `Program.cs`
```csharp
builder.Services.AddHttpClient();
builder.Services.AddTransient<AccessTokenService>();
```
Add `using AdvantageTool.Services;` at the top.

- [ ] Step 3: Build, commit
```bash
dotnet build
git add -A
git commit -m "Add AccessTokenService for JWT client-credentials token flow"
```

---

## Task 9: Platforms management pages

**Files:**
- Create: `src/AdvantageTool/Pages/Platforms/PlatformInputModel.cs`
- Create: `src/AdvantageTool/Pages/Platforms/Index.cshtml(.cs)`
- Create: `src/AdvantageTool/Pages/Platforms/Create.cshtml(.cs)`
- Create: `src/AdvantageTool/Pages/Platforms/Edit.cshtml(.cs)`
- Create: `src/AdvantageTool/Pages/Platforms/Details.cshtml(.cs)`
- Create: `src/AdvantageTool/Pages/Platforms/Delete.cshtml(.cs)`
- Modify: `src/AdvantageTool/Program.cs` (authorize folder)

- [ ] Step 1: Add folder authorization
In `Program.cs`, replace `builder.Services.AddRazorPages();` with:
```csharp
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Platforms");
});
```

- [ ] Step 2: Write `Pages/Platforms/PlatformInputModel.cs`
```csharp
using System.ComponentModel.DataAnnotations;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using IdentityModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AdvantageTool.Pages.Platforms;

public class PlatformInputModel
{
    public PlatformInputModel() { }

    public PlatformInputModel(HttpRequest request, IUrlHelper url, Platform? platform = null)
    {
        if (platform is null)
        {
            PlatformId = CryptoRandom.CreateUniqueId(8);
            var pair = PemHelper.GenerateRsaKeyPair();
            PrivateKey = pair.PrivateKey;
            PublicKey = pair.PublicKey;
            KeyId = pair.KeyId;
        }
        else
        {
            Id = platform.Id;
            Name = platform.Name;
            Issuer = platform.Issuer;
            AuthorizeUrl = platform.AuthorizeUrl;
            AccessTokenUrl = platform.AccessTokenUrl;
            JwkSetUrl = platform.JwkSetUrl;
            PlatformId = platform.PlatformId;
            ClientId = platform.ClientId;
            KeyId = platform.KeyId;
            PrivateKey = platform.PrivateKey;
            PublicKey = platform.PublicKey;
        }

        LaunchUrl         = url.Page("/Tool",      null, new { platformId = PlatformId }, request.Scheme)!;
        LoginUrl          = url.Page("/OidcLogin", null, null, request.Scheme)!;
        DeepLinkingUrl    = url.Page("/Tool",      null, new { platformId = PlatformId }, request.Scheme)!;
        JwksUrl           = $"{request.Scheme}://{request.Host}/jwks/{PlatformId}";
    }

    public int Id { get; set; }

    [Required, Display(Name = "Display name")] public string Name { get; set; } = string.Empty;
    [Required, Display(Name = "Issuer")]       public string Issuer { get; set; } = string.Empty;
    [Required, Display(Name = "Authorize URL")] public string AuthorizeUrl { get; set; } = string.Empty;
    [Required, Display(Name = "Access token URL")] public string AccessTokenUrl { get; set; } = string.Empty;
    [Required, Display(Name = "JWKS URL")]     public string JwkSetUrl { get; set; } = string.Empty;
    [Required, Display(Name = "Client ID")]    public string ClientId { get; set; } = string.Empty;

    public string PlatformId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    [Required] public string PrivateKey { get; set; } = string.Empty;
    [Required] public string PublicKey { get; set; } = string.Empty;

    public string LaunchUrl { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = string.Empty;
    public string DeepLinkingUrl { get; set; } = string.Empty;
    public string JwksUrl { get; set; } = string.Empty;

    public void UpdateEntity(Platform p)
    {
        p.Name = Name;
        p.Issuer = Issuer;
        p.AuthorizeUrl = AuthorizeUrl;
        p.AccessTokenUrl = AccessTokenUrl;
        p.JwkSetUrl = JwkSetUrl;
        p.PlatformId = PlatformId;
        p.ClientId = ClientId;
        p.KeyId = KeyId;
        p.PrivateKey = PrivateKey;
        p.PublicKey = PublicKey;
    }
}
```

- [ ] Step 3: Create `Pages/Platforms/Index.cshtml.cs`
```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages.Platforms;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    public IndexModel(ApplicationDbContext context) => _context = context;

    public IList<Platform> Platforms { get; set; } = new List<Platform>();

    public async Task OnGetAsync()
    {
        var user = await _context.GetUserAsync(User);
        Platforms = user?.Platforms.OrderBy(p => p.Name).ToList() ?? new List<Platform>();
    }
}
```

- [ ] Step 4: Create `Pages/Platforms/Index.cshtml`
```html
@page
@model AdvantageTool.Pages.Platforms.IndexModel
@{ ViewData["Title"] = "Platform registrations"; }

<h1>Platform registrations</h1>
<p><a class="btn btn-primary" asp-page="Create">Add platform</a></p>

@if (!Model.Platforms.Any())
{
    <p>No platforms yet.</p>
}
else
{
    <table class="table">
        <thead><tr><th>Name</th><th>Issuer</th><th>Client ID</th><th></th></tr></thead>
        <tbody>
        @foreach (var p in Model.Platforms)
        {
            <tr>
                <td>@p.Name</td>
                <td>@p.Issuer</td>
                <td>@p.ClientId</td>
                <td>
                    <a asp-page="Details" asp-route-id="@p.Id">Details</a> |
                    <a asp-page="Edit" asp-route-id="@p.Id">Edit</a> |
                    <a asp-page="Delete" asp-route-id="@p.Id">Delete</a>
                </td>
            </tr>
        }
        </tbody>
    </table>
}
```

- [ ] Step 5: Create `Pages/Platforms/Create.cshtml.cs`
```csharp
using System.Linq;
using System.Threading.Tasks;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages.Platforms;

public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    public CreateModel(ApplicationDbContext context) => _context = context;

    [BindProperty] public PlatformInputModel Platform { get; set; } = new();

    public IActionResult OnGet()
    {
        Platform = new PlatformInputModel(Request, Url);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_context.Platforms.Any(p => p.Issuer == Platform.Issuer))
            ModelState.AddModelError($"{nameof(Platform)}.{nameof(Platform.Issuer)}", "Issuer already registered.");
        if (!ModelState.IsValid) return Page();

        var user = await _context.GetUserAsync(User);
        var entity = new Platform { UserId = user!.Id };
        Platform.UpdateEntity(entity);
        _context.Platforms.Add(entity);
        await _context.SaveChangesAsync();
        return RedirectToPage("./Index");
    }
}
```

- [ ] Step 6: Create `Pages/Platforms/Create.cshtml`
```html
@page
@model AdvantageTool.Pages.Platforms.CreateModel
@{ ViewData["Title"] = "Add platform"; }

<h1>Add platform</h1>

<form method="post">
    <div asp-validation-summary="All" class="text-danger"></div>

    <h4>Platform side (provided by the LMS)</h4>
    <div class="mb-3"><label asp-for="Platform.Name" class="form-label"></label><input asp-for="Platform.Name" class="form-control"/><span asp-validation-for="Platform.Name" class="text-danger"></span></div>
    <div class="mb-3"><label asp-for="Platform.Issuer" class="form-label"></label><input asp-for="Platform.Issuer" class="form-control"/><span asp-validation-for="Platform.Issuer" class="text-danger"></span></div>
    <div class="mb-3"><label asp-for="Platform.AuthorizeUrl" class="form-label"></label><input asp-for="Platform.AuthorizeUrl" class="form-control"/></div>
    <div class="mb-3"><label asp-for="Platform.AccessTokenUrl" class="form-label"></label><input asp-for="Platform.AccessTokenUrl" class="form-control"/></div>
    <div class="mb-3"><label asp-for="Platform.JwkSetUrl" class="form-label"></label><input asp-for="Platform.JwkSetUrl" class="form-control"/></div>
    <div class="mb-3"><label asp-for="Platform.ClientId" class="form-label"></label><input asp-for="Platform.ClientId" class="form-control"/></div>

    <h4 class="mt-4">Tool side (give these to the platform)</h4>
    <div class="mb-3">
        <label class="form-label">Login URL</label>
        <input class="form-control" value="@Model.Platform.LoginUrl" readonly/>
    </div>
    <div class="mb-3">
        <label class="form-label">Launch URL</label>
        <input class="form-control" value="@Model.Platform.LaunchUrl" readonly/>
    </div>
    <div class="mb-3">
        <label class="form-label">Deep linking URL</label>
        <input class="form-control" value="@Model.Platform.DeepLinkingUrl" readonly/>
    </div>
    <div class="mb-3">
        <label class="form-label">JWKS URL</label>
        <input class="form-control" value="@Model.Platform.JwksUrl" readonly/>
    </div>
    <div class="mb-3">
        <label class="form-label">Public key (PEM)</label>
        <textarea class="form-control" rows="6" readonly>@Model.Platform.PublicKey</textarea>
    </div>

    <input type="hidden" asp-for="Platform.PlatformId"/>
    <input type="hidden" asp-for="Platform.KeyId"/>
    <input type="hidden" asp-for="Platform.PrivateKey"/>
    <input type="hidden" asp-for="Platform.PublicKey"/>

    <button type="submit" class="btn btn-primary">Save</button>
    <a asp-page="Index" class="btn btn-secondary">Cancel</a>
</form>

@section Scripts { @{ await Html.RenderPartialAsync("_ValidationScriptsPartial"); } }
```

- [ ] Step 7: Create `Pages/Platforms/Details.cshtml(.cs)` — read-only display
`Details.cshtml.cs`:
```csharp
using System.Threading.Tasks;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Pages.Platforms;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    public DetailsModel(ApplicationDbContext context) => _context = context;

    public PlatformInputModel? Platform { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _context.Platforms.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null) return NotFound();
        Platform = new PlatformInputModel(Request, Url, entity);
        return Page();
    }
}
```
`Details.cshtml`:
```html
@page "{id:int}"
@model AdvantageTool.Pages.Platforms.DetailsModel
@{ ViewData["Title"] = "Platform details"; }
<h1>@Model.Platform!.Name</h1>
<dl class="row">
    <dt class="col-sm-3">Issuer</dt><dd class="col-sm-9">@Model.Platform.Issuer</dd>
    <dt class="col-sm-3">Authorize URL</dt><dd class="col-sm-9">@Model.Platform.AuthorizeUrl</dd>
    <dt class="col-sm-3">Access token URL</dt><dd class="col-sm-9">@Model.Platform.AccessTokenUrl</dd>
    <dt class="col-sm-3">JWKS URL</dt><dd class="col-sm-9">@Model.Platform.JwkSetUrl</dd>
    <dt class="col-sm-3">Client ID</dt><dd class="col-sm-9">@Model.Platform.ClientId</dd>
    <dt class="col-sm-3">Login URL (tool)</dt><dd class="col-sm-9">@Model.Platform.LoginUrl</dd>
    <dt class="col-sm-3">Launch URL (tool)</dt><dd class="col-sm-9">@Model.Platform.LaunchUrl</dd>
    <dt class="col-sm-3">JWKS URL (tool)</dt><dd class="col-sm-9">@Model.Platform.JwksUrl</dd>
</dl>
<a asp-page="Edit" asp-route-id="@Model.Platform.Id" class="btn btn-primary">Edit</a>
<a asp-page="Index" class="btn btn-secondary">Back</a>
```

- [ ] Step 8: Create `Pages/Platforms/Edit.cshtml(.cs)`
`Edit.cshtml.cs`:
```csharp
using System.Threading.Tasks;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Pages.Platforms;

public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    public EditModel(ApplicationDbContext context) => _context = context;

    [BindProperty] public PlatformInputModel Platform { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _context.Platforms.FindAsync(id);
        if (entity is null) return NotFound();
        Platform = new PlatformInputModel(Request, Url, entity);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!ModelState.IsValid) return Page();
        var entity = await _context.Platforms.FindAsync(id);
        if (entity is null) return NotFound();
        Platform.UpdateEntity(entity);
        await _context.SaveChangesAsync();
        return RedirectToPage("./Index");
    }
}
```
`Edit.cshtml` — same as `Create.cshtml` but route `@page "{id:int}"`. Copy the Create body.

- [ ] Step 9: Create `Pages/Platforms/Delete.cshtml(.cs)`
`Delete.cshtml.cs`:
```csharp
using System.Threading.Tasks;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages.Platforms;

public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;
    public DeleteModel(ApplicationDbContext context) => _context = context;

    [BindProperty] public Platform Platform { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _context.Platforms.FindAsync(id);
        if (entity is null) return NotFound();
        Platform = entity;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var entity = await _context.Platforms.FindAsync(id);
        if (entity is not null)
        {
            _context.Platforms.Remove(entity);
            await _context.SaveChangesAsync();
        }
        return RedirectToPage("./Index");
    }
}
```
`Delete.cshtml`:
```html
@page "{id:int}"
@model AdvantageTool.Pages.Platforms.DeleteModel
<h1>Delete @Model.Platform.Name?</h1>
<form method="post">
    <button type="submit" class="btn btn-danger">Delete</button>
    <a asp-page="Index" class="btn btn-secondary">Cancel</a>
</form>
```

- [ ] Step 10: Build, commit
```bash
dotnet build
git add -A
git commit -m "Add platform registration pages (CRUD) with auto-generated RSA keys"
```

---

## Task 10: JWKS endpoint

**Files:**
- Create: `src/AdvantageTool/Services/JwksService.cs`
- Modify: `src/AdvantageTool/Program.cs`

- [ ] Step 1: Write `Services/JwksService.cs`
```csharp
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Services;

public class JwksService
{
    private readonly ApplicationDbContext _context;
    public JwksService(ApplicationDbContext context) => _context = context;

    public async Task<object?> GetJwksAsync(string platformId)
    {
        var platform = await _context.GetPlatformByPlatformIdAsync(platformId);
        if (platform is null || string.IsNullOrEmpty(platform.PublicKey)) return null;

        var rsaParams = PemHelper.PublicParametersFromPem(platform.PublicKey);
        var key = new RsaSecurityKey(rsaParams) { KeyId = platform.KeyId };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.RsaSha256;
        return new { keys = new[] { jwk } };
    }
}
```

- [ ] Step 2: Register and map in `Program.cs`
After other DI:
```csharp
builder.Services.AddTransient<JwksService>();
```
After Razor Pages:
```csharp
app.MapGet("/jwks/{platformId}", async (string platformId, JwksService svc) =>
{
    var jwks = await svc.GetJwksAsync(platformId);
    return jwks is null ? Results.NotFound() : Results.Json(jwks);
});
```

- [ ] Step 3: Build, commit
```bash
dotnet build
git add -A
git commit -m "Expose tool JWKS endpoint per-platform"
```

---

## Task 11: OidcLogin page

**Files:**
- Create: `src/AdvantageTool/Pages/OidcLogin.cshtml(.cs)`

- [ ] Step 1: Write `Pages/OidcLogin.cshtml.cs`
```csharp
using System;
using System.Threading.Tasks;
using AdvantageTool.Data;
using IdentityModel;
using IdentityModel.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace AdvantageTool.Pages;

[IgnoreAntiforgeryToken]
public class OidcLoginModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly StateDbContext _state;
    private readonly ILogger<OidcLoginModel> _logger;

    public OidcLoginModel(ApplicationDbContext context, StateDbContext state, ILogger<OidcLoginModel> logger)
    {
        _context = context; _state = state; _logger = logger;
    }

    [BindProperty(Name = "iss", SupportsGet = true)] public string? Issuer { get; set; }
    [BindProperty(Name = "login_hint", SupportsGet = true)] public string? LoginHint { get; set; }
    [BindProperty(Name = "lti_message_hint", SupportsGet = true)] public string? LtiMessageHint { get; set; }
    [BindProperty(Name = "target_link_uri", SupportsGet = true)] public string? TargetLinkUri { get; set; }
    [BindProperty(Name = "client_id", SupportsGet = true)] public string? ClientId { get; set; }

    public Task<IActionResult> OnGetAsync()  => HandleAsync();
    public Task<IActionResult> OnPostAsync() => HandleAsync();

    private async Task<IActionResult> HandleAsync()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(LoginHint)
            || string.IsNullOrWhiteSpace(TargetLinkUri))
        {
            _logger.LogError("OIDC login missing required parameters.");
            return BadRequest();
        }

        var platform = await _context.GetPlatformByIssuerAsync(Issuer);
        if (platform is null)
        {
            _logger.LogError("Unknown issuer {Issuer}.", Issuer);
            return BadRequest();
        }

        if (!Uri.TryCreate(TargetLinkUri, UriKind.Absolute, out var target) || target.Host != Request.Host.Host)
        {
            _logger.LogError("Invalid target_link_uri {Target}.", TargetLinkUri);
            return BadRequest();
        }

        var nonce = CryptoRandom.CreateUniqueId();
        var state = CryptoRandom.CreateUniqueId();
        _state.AddState(nonce, state);

        var url = new RequestUrl(platform.AuthorizeUrl).CreateAuthorizeUrl(
            clientId: platform.ClientId,
            responseType: OidcConstants.ResponseTypes.IdToken,
            responseMode: OidcConstants.ResponseModes.FormPost,
            redirectUri: TargetLinkUri,
            scope: OidcConstants.StandardScopes.OpenId,
            state: state,
            loginHint: LoginHint,
            nonce: nonce,
            prompt: "none",
            extra: string.IsNullOrEmpty(LtiMessageHint) ? null : new { lti_message_hint = LtiMessageHint });

        return Redirect(url);
    }
}
```

- [ ] Step 2: Write `Pages/OidcLogin.cshtml`
```html
@page
@model AdvantageTool.Pages.OidcLoginModel
```
(Empty body — handlers always redirect or return BadRequest.)

- [ ] Step 3: Build, commit
```bash
dotnet build
git add -A
git commit -m "Add OIDC login initiation page"
```

---

## Task 12: Tool launch page (id_token validation + display)

**Files:**
- Create: `src/AdvantageTool/Pages/Tool.cshtml(.cs)`

- [ ] Step 1: Write `Pages/Tool.cshtml.cs`
```csharp
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AdvantageTool.Data;
using LtiAdvantage;
using LtiAdvantage.Lti;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Pages;

[IgnoreAntiforgeryToken]
public class ToolModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly StateDbContext _state;
    private readonly IHttpClientFactory _httpClientFactory;

    public ToolModel(ApplicationDbContext context, StateDbContext state, IHttpClientFactory httpClientFactory)
    {
        _context = context; _state = state; _httpClientFactory = httpClientFactory;
    }

    public string? Error { get; set; }
    public string? IdToken { get; set; }
    public JwtHeader? JwtHeader { get; set; }
    public LtiResourceLinkRequest? LtiRequest { get; set; }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync(
        string platformId,
        [FromForm(Name = "id_token")] string? idToken,
        [FromForm(Name = "state")] string? state)
    {
        if (string.IsNullOrEmpty(idToken)) { Error = "id_token missing"; return Page(); }

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(idToken)) { Error = "Cannot read id_token"; return Page(); }
        var jwt = handler.ReadJwtToken(idToken);
        JwtHeader = jwt.Header;

        var messageType = jwt.Claims.SingleOrDefault(c => c.Type == Constants.LtiClaims.MessageType)?.Value;
        if (string.IsNullOrEmpty(messageType)) { Error = "message_type claim missing"; return Page(); }

        var nonce = jwt.Claims.SingleOrDefault(c => c.Type == "nonce")?.Value;
        if (string.IsNullOrEmpty(nonce)) { Error = "nonce missing"; return Page(); }
        var memorized = _state.GetState(nonce);
        if (memorized is null) { Error = "Invalid nonce (possible replay)"; return Page(); }
        if (memorized.Value != state) { Error = "state mismatch"; return Page(); }

        var platform = await _context.GetPlatformByPlatformIdAsync(platformId);
        if (platform is null) { Error = "Unknown platform"; return Page(); }

        SecurityKey signingKey;
        try
        {
            var http = _httpClientFactory.CreateClient();
            var keySetJson = await http.GetStringAsync(platform.JwkSetUrl);
            var keySet = new JsonWebKeySet(keySetJson);
            var match = keySet.Keys.SingleOrDefault(k => k.Kid == jwt.Header.Kid);
            if (match is null) { Error = "Platform did not advertise key " + jwt.Header.Kid; return Page(); }
            signingKey = match;
        }
        catch (Exception e) { Error = e.Message; return Page(); }

        try
        {
            handler.ValidateToken(idToken, new TokenValidationParameters
            {
                ValidIssuer = platform.Issuer,
                ValidAudience = platform.ClientId,
                IssuerSigningKey = signingKey,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            }, out _);
        }
        catch (Exception e) { Error = e.Message; return Page(); }

        if (messageType == Constants.Lti.LtiDeepLinkingRequestMessageType)
            return AutoPost("./Catalog", new { idToken });

        IdToken = idToken;
        LtiRequest = new LtiResourceLinkRequest(jwt.Payload);
        return Page();
    }

    private ContentResult AutoPost(string url, object values)
    {
        var dict = values.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(values)?.ToString() ?? "");
        var sb = new StringBuilder();
        sb.Append("<html><body onload=\"document.forms[0].submit()\"><form method=\"post\" action=\"")
          .Append(url).Append("\">");
        foreach (var (k, v) in dict)
            sb.Append("<input type=\"hidden\" name=\"").Append(k).Append("\" value=\"").Append(System.Net.WebUtility.HtmlEncode(v)).Append("\"/>");
        sb.Append("</form></body></html>");
        return new ContentResult { Content = sb.ToString(), ContentType = "text/html", StatusCode = StatusCodes.Status200OK };
    }
}
```

- [ ] Step 2: Write `Pages/Tool.cshtml`
```html
@page "{platformId}"
@model AdvantageTool.Pages.ToolModel
@{ Layout = "_Layout"; ViewData["Title"] = Model.LtiRequest?.ResourceLink?.Title ?? "Launch"; }

@if (!string.IsNullOrEmpty(Model.Error))
{
    <div class="alert alert-danger">@Model.Error</div>
}

@if (Model.LtiRequest is not null)
{
    <h1>@Model.LtiRequest.ResourceLink?.Title</h1>

    <div class="row">
        <div class="col-md-4">
            <div class="card mb-3"><div class="card-header">Platform</div><div class="card-body">
                <div>@(Model.LtiRequest.Platform?.Name ?? "(no name)")</div>
                <div class="text-muted">@(Model.LtiRequest.Platform?.ProductFamilyCode) @(Model.LtiRequest.Platform?.Version)</div>
            </div></div>
        </div>
        <div class="col-md-4">
            <div class="card mb-3"><div class="card-header">User</div><div class="card-body">
                <div>@Model.LtiRequest.GivenName @Model.LtiRequest.FamilyName</div>
                <div class="text-muted">@Model.LtiRequest.Email</div>
                <div><small>@string.Join(", ", Model.LtiRequest.Roles ?? Array.Empty<string>())</small></div>
            </div></div>
        </div>
        <div class="col-md-4">
            <div class="card mb-3"><div class="card-header">Context</div><div class="card-body">
                <div>@(Model.LtiRequest.Context?.Title ?? "(no context)")</div>
                <div class="text-muted">@Model.LtiRequest.Context?.Label</div>
            </div></div>
        </div>
    </div>

    @if (Model.LtiRequest.AssignmentGradeServices is not null || Model.LtiRequest.NamesRoleService is not null)
    {
        @await Component.InvokeAsync("LineItems", Model.IdToken)
    }
}

<div class="row mt-4">
    <div class="col-md-6"><div class="card"><div class="card-header">JWT Header</div><pre class="card-body">@Model.JwtHeader?.SerializeToJson()</pre></div></div>
    <div class="col-md-6"><div class="card"><div class="card-header">JWT Payload</div><pre class="card-body">@Model.LtiRequest?.SerializeToJson()</pre></div></div>
</div>
```

- [ ] Step 3: Build, commit
```bash
dotnet build
git add -A
git commit -m "Add Tool page: id_token validation, payload display, AGS/NRPS dispatch"
```

---

## Task 13: LineItems view component (AGS + NRPS demo)

**Files:**
- Create: `src/AdvantageTool/Pages/Components/LineItems/LineItemsViewComponent.cs`
- Create: `src/AdvantageTool/Pages/Components/LineItems/LineItemsModel.cs`
- Create: `src/AdvantageTool/Pages/Components/LineItems/Default.cshtml`

- [ ] Step 1: Write `LineItemsModel.cs`
```csharp
using System.Collections.Generic;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.Lti;

namespace AdvantageTool.Pages.Components.LineItems;

public class LineItemsModel
{
    public LineItemsModel(string? idToken) { IdToken = idToken ?? string.Empty; }

    public string IdToken { get; set; }
    public string? Status { get; set; }
    public string? LineItemUrl { get; set; }
    public LtiResourceLinkRequest? LtiRequest { get; set; }
    public IList<MyLineItem> LineItems { get; set; } = new List<MyLineItem>();
    public IDictionary<string, string> Members { get; set; } = new Dictionary<string, string>();
}

public class MyLineItem
{
    public string Header { get; set; } = string.Empty;
    public LineItem AgsLineItem { get; set; } = default!;
    public ResultContainer? Results { get; set; }
}
```

- [ ] Step 2: Write `LineItemsViewComponent.cs`
```csharp
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using AdvantageTool.Services;
using IdentityModel.Client;
using LtiAdvantage;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.Lti;
using LtiAdvantage.NamesRoleProvisioningService;
using LtiAdvantage.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace AdvantageTool.Pages.Components.LineItems;

public class LineItemsViewComponent : ViewComponent
{
    private readonly AccessTokenService _tokens;
    private readonly IHttpClientFactory _httpClientFactory;

    public LineItemsViewComponent(AccessTokenService tokens, IHttpClientFactory httpClientFactory)
    {
        _tokens = tokens; _httpClientFactory = httpClientFactory;
    }

    public async Task<IViewComponentResult> InvokeAsync(string? idToken)
    {
        var model = new LineItemsModel(idToken);
        if (string.IsNullOrEmpty(idToken)) { model.Status = "id_token missing"; return View(model); }

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
        model.LtiRequest = new LtiResourceLinkRequest(jwt.Payload);
        var lti = model.LtiRequest;

        if (lti.AssignmentGradeServices is null) { model.Status = "AGS not present"; return View(model); }
        model.LineItemUrl = lti.AssignmentGradeServices.LineItemUrl;

        var scopes = string.Join(" ",
            Constants.LtiScopes.Ags.LineItem,
            Constants.LtiScopes.Ags.ResultReadonly,
            Constants.LtiScopes.Nrps.MembershipReadonly);
        var token = await _tokens.GetAccessTokenAsync(lti.Iss!, scopes);
        if (token.IsError) { model.Status = token.Error; return View(model); }

        var http = _httpClientFactory.CreateClient();
        http.SetBearerToken(token.AccessToken);

        try
        {
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(Constants.MediaTypes.LineItemContainer));
            var liJson = await http.GetStringAsync(lti.AssignmentGradeServices.LineItemsUrl);
            var lineItems = JsonSerializer.Deserialize<LineItem[]>(liJson, JsonOptions.DefaultOptions) ?? Array.Empty<LineItem>();
            model.LineItems = lineItems.Select(i => new MyLineItem
            {
                AgsLineItem = i,
                Header = i.Label ?? $"Tag: {i.Tag}"
            }).ToList();
        }
        catch (Exception e) { model.Status = e.Message; return View(model); }

        if (lti.NamesRoleService is not null)
        {
            try
            {
                http.DefaultRequestHeaders.Accept.Clear();
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(Constants.MediaTypes.MembershipContainer));
                var memJson = await http.GetStringAsync(lti.NamesRoleService.ContextMembershipUrl);
                var container = JsonSerializer.Deserialize<MembershipContainer>(memJson, JsonOptions.DefaultOptions);
                if (container?.Members is not null)
                    foreach (var m in container.Members.OrderBy(m => m.FamilyName).ThenBy(m => m.GivenName))
                        model.Members.TryAdd(m.UserId!, $"{m.FamilyName}, {m.GivenName}");
            }
            catch (Exception e) { model.Status = e.Message; }
        }

        try
        {
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(Constants.MediaTypes.ResultContainer));
            foreach (var li in model.LineItems)
            {
                var url = li.AgsLineItem.Id!.TrimEnd('/') + "/results";
                var json = await http.GetStringAsync(url);
                li.Results = JsonSerializer.Deserialize<ResultContainer>(json, JsonOptions.DefaultOptions);
            }
        }
        catch (Exception e) { model.Status = e.Message; }

        return View(model);
    }
}
```

- [ ] Step 3: Write `Default.cshtml`
```html
@model AdvantageTool.Pages.Components.LineItems.LineItemsModel

<div class="card mt-3">
    <div class="card-header">Assignment & Grade Services</div>
    <div class="card-body">
        @if (!string.IsNullOrEmpty(Model.Status))
        {
            <div class="alert alert-warning">@Model.Status</div>
        }
        else
        {
            <h5>Line items</h5>
            @if (Model.LineItems.Count == 0)
            {
                <p>No line items yet.</p>
            }
            else
            {
                <ul>
                    @foreach (var li in Model.LineItems)
                    {
                        <li>
                            <strong>@li.Header</strong>
                            @if (li.Results?.Count > 0)
                            {
                                <ul>
                                    @foreach (var r in li.Results)
                                    {
                                        <li>@(Model.Members.TryGetValue(r.UserId ?? "", out var n) ? n : r.UserId): @r.ResultScore / @r.ResultMaximum</li>
                                    }
                                </ul>
                            }
                        </li>
                    }
                </ul>
            }

            @if (Model.Members.Count > 0)
            {
                <h5>Members</h5>
                <ul>
                    @foreach (var m in Model.Members) { <li>@m.Value</li> }
                </ul>
            }
        }
    </div>
</div>
```

- [ ] Step 4: Build, commit
```bash
dotnet build
git add -A
git commit -m "Add LineItems view component: AGS line items + NRPS members + results"
```

---

## Task 14: Catalog page (deep linking response)

**Files:**
- Create: `src/AdvantageTool/Pages/Catalog.cshtml(.cs)`

- [ ] Step 1: Write `Pages/Catalog.cshtml.cs`
```csharp
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using LtiAdvantage.DeepLinking;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Pages;

[IgnoreAntiforgeryToken]
public class CatalogModel : PageModel
{
    private static readonly string[] SampleActivities =
    {
        "Reading comprehension drill",
        "Algebra warm-up",
        "World history quiz: 18th century",
        "Lab safety video",
        "Vocabulary builder",
        "Geometry challenge",
    };

    private readonly ApplicationDbContext _context;
    public CatalogModel(ApplicationDbContext context) => _context = context;

    [BindProperty] public string IdToken { get; set; } = string.Empty;
    [BindProperty] public IList<Activity> Activities { get; set; } = new List<Activity>();

    public LtiDeepLinkingRequest? LtiRequest { get; private set; }
    public string? Error { get; private set; }

    public IActionResult OnPost()
    {
        if (string.IsNullOrEmpty(IdToken)) { Error = "id_token missing"; return Page(); }
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(IdToken);
        LtiRequest = new LtiDeepLinkingRequest(jwt.Payload);
        Activities = SampleActivities
            .Select((title, i) => new Activity { Id = i, Title = title, Description = $"Sample LTI activity: {title}" })
            .ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAssignActivitiesAsync()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(IdToken);
        LtiRequest = new LtiDeepLinkingRequest(jwt.Payload);

        var response = new LtiDeepLinkingResponse
        {
            Data = LtiRequest.DeepLinkingSettings.Data,
            DeploymentId = LtiRequest.DeploymentId
        };

        var contentItems = Activities.Where(a => a.Selected).Select(a => (ContentItem)new LtiLinkItem
        {
            Title = a.Title,
            Text = a.Description,
            Url = Url.Page("/Tool", null, null, Request.Scheme),
            Custom = new Dictionary<string, string> { ["activity_id"] = a.Id.ToString() }
        }).ToArray();

        response.ContentItems = contentItems;
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, LtiRequest.Aud[0]));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Aud, LtiRequest.Iss));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, LtiRequest.Sub));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(DateTime.UtcNow).ToString()));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Nbf, EpochTime.GetIntDate(DateTime.UtcNow.AddSeconds(-5)).ToString()));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Exp, EpochTime.GetIntDate(DateTime.UtcNow.AddMinutes(5)).ToString()));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Nonce, Guid.NewGuid().ToString("N")));

        var platform = await _context.GetPlatformByIssuerAsync(LtiRequest.Iss!);
        var creds = PemHelper.SigningCredentialsFromPem(platform!.PrivateKey, platform.KeyId);
        var jwtOut = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(creds), response));

        return AutoPost("id_token", jwtOut, LtiRequest.DeepLinkingSettings.DeepLinkReturnUrl);
    }

    private static ContentResult AutoPost(string name, string value, string url)
    {
        var html = new StringBuilder();
        html.Append("<html><body onload=\"document.forms[0].submit()\"><form method=\"post\" action=\"")
            .Append(url).Append("\"><input type=\"hidden\" name=\"").Append(name)
            .Append("\" value=\"").Append(System.Net.WebUtility.HtmlEncode(value)).Append("\"/></form></body></html>");
        return new ContentResult { Content = html.ToString(), ContentType = "text/html", StatusCode = StatusCodes.Status200OK };
    }

    public class Activity
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }
}
```

- [ ] Step 2: Write `Pages/Catalog.cshtml`
```html
@page
@model AdvantageTool.Pages.CatalogModel
@{ ViewData["Title"] = "Pick activities"; }

@if (!string.IsNullOrEmpty(Model.Error))
{
    <div class="alert alert-danger">@Model.Error</div>
}

<h1>Pick activities</h1>
<form method="post" asp-page-handler="AssignActivities">
    <input type="hidden" asp-for="IdToken"/>
    @for (int i = 0; i < Model.Activities.Count; i++)
    {
        <div class="form-check">
            <input class="form-check-input" type="checkbox" asp-for="Activities[i].Selected"/>
            <input type="hidden" asp-for="Activities[i].Id"/>
            <input type="hidden" asp-for="Activities[i].Title"/>
            <input type="hidden" asp-for="Activities[i].Description"/>
            <label class="form-check-label">@Model.Activities[i].Title — <small class="text-muted">@Model.Activities[i].Description</small></label>
        </div>
    }
    <button type="submit" class="btn btn-primary mt-3">Assign</button>
</form>
```

- [ ] Step 3: Build, commit
```bash
dotnet build
git add -A
git commit -m "Add Catalog page for deep linking response"
```

---

## Task 15: Home page + Layout polish

**Files:**
- Modify: `src/AdvantageTool/Pages/Index.cshtml(.cs)`
- Modify: `src/AdvantageTool/Pages/Shared/_Layout.cshtml`

- [ ] Step 1: Replace `Pages/Index.cshtml.cs`
```csharp
using System.Threading.Tasks;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    public IndexModel(ApplicationDbContext context) => _context = context;

    public int PlatformCount { get; set; }

    public async Task OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _context.GetUserAsync(User);
            PlatformCount = user?.Platforms.Count ?? 0;
        }
    }
}
```

- [ ] Step 2: Replace `Pages/Index.cshtml`
```html
@page
@model AdvantageTool.Pages.IndexModel
@{ ViewData["Title"] = "Home"; }

<h1>LtiAdvantage sample tool</h1>
<p class="lead">A minimal LTI 1.3 / LTI Advantage tool built with the LtiAdvantage library.</p>

@if (User.Identity?.IsAuthenticated == true)
{
    <p>You have @Model.PlatformCount platform registration(s).</p>
    <a class="btn btn-primary" asp-page="/Platforms/Index">Manage platforms</a>
}
else
{
    <p>Register or log in, then add a platform to get launch / deep linking / JWKS URLs.</p>
    <a class="btn btn-primary" asp-area="Identity" asp-page="/Account/Register">Register</a>
    <a class="btn btn-secondary" asp-area="Identity" asp-page="/Account/Login">Log in</a>
}
```

- [ ] Step 3: Add the Platforms link to `_Layout.cshtml`'s nav (replace the Privacy link block with Platforms when authenticated)
Open the existing `Pages/Shared/_Layout.cshtml` and locate the `<ul class="navbar-nav flex-grow-1">` block. Replace the existing `<li>` content with:
```html
<li class="nav-item">
    <a class="nav-link text-dark" asp-area="" asp-page="/Index">Home</a>
</li>
@if (User.Identity?.IsAuthenticated == true)
{
    <li class="nav-item">
        <a class="nav-link text-dark" asp-area="" asp-page="/Platforms/Index">Platforms</a>
    </li>
}
```

- [ ] Step 4: Build, commit
```bash
dotnet build
git add -A
git commit -m "Polish home page and nav: surface platform management"
```

---

## Task 16: README + top-level cleanup

**Files:**
- Modify: `README.md`

- [ ] Step 1: Replace `README.md` with a useful description
```markdown
# LTI Advantage Tool (sample)

A minimal LTI 1.3 / LTI Advantage Tool built with the
[LtiAdvantage](https://github.com/LtiLibrary/LtiAdvantage) library and ASP.NET
Core 10. Demonstrates:

- OIDC third-party login initiation
- Resource-link launch (id_token validation, payload display)
- Deep linking response (signed by the tool)
- Assignment and Grade Services: line items, results
- Names and Role Provisioning Service: course members
- A per-platform JWKS endpoint so the platform can validate tool-signed JWTs

## Running

```sh
cd src/AdvantageTool
dotnet run
```

Then:

1. Open the printed URL.
2. Register a tool admin account (the password rules are deliberately lax —
   this is a sample).
3. Go to **Platforms → Add platform** and fill in your LMS's Issuer,
   Authorize URL, Access token URL, JWKS URL, and Client ID.
4. The page also shows the URLs to give the platform side: Login URL,
   Launch URL, Deep linking URL, JWKS URL, and the public key.

## Where to look first

- `Pages/OidcLogin.cshtml.cs` — third-party login initiation.
- `Pages/Tool.cshtml.cs` — resource-link launch handler with full id_token
  validation against the platform's JWKS.
- `Pages/Catalog.cshtml.cs` — deep linking response: select activities,
  build and sign the `LtiDeepLinkingResponse`, post it back.
- `Pages/Components/LineItems/LineItemsViewComponent.cs` — token-protected
  AGS + NRPS calls using the LtiAdvantage models.
- `Services/AccessTokenService.cs` — JWT client-credentials flow.
- `Services/JwksService.cs` and the `MapGet("/jwks/{platformId}", ...)` in
  `Program.cs` — the tool's JWKS endpoint.

## Platform side

A separate sample LTI Advantage platform is at
[LtiAdvantagePlatform](https://github.com/LtiLibrary/LtiAdvantagePlatform).
You can also point this tool at the IMS reference platform.
```

- [ ] Step 2: Commit
```bash
git add README.md
git commit -m "Rewrite README for current sample"
```

---

## Task 17: Build verification + smoke test

- [ ] Step 1: Clean rebuild
```bash
cd /Users/sanderrijken/LtiAdvantageTool
dotnet build -c Release
```
Expected: 0 errors, 0 warnings (or only template-generated warnings; investigate any new warnings).

- [ ] Step 2: Run, verify URLs respond. Start the app in the background, hit the home page, register endpoint, JWKS-not-found, and stop it.
```bash
dotnet run --project src/AdvantageTool --no-build &
APP_PID=$!
sleep 4
# Read launchSettings to find the URL — typically https://localhost:5xxx + http://localhost:5xxx
curl -ks https://localhost:7244/ -o /tmp/home.html ; echo "HOME: $(grep -c 'LtiAdvantage sample tool' /tmp/home.html)"
curl -ks -o /dev/null -w "JWKS-MISSING:%{http_code}\n" https://localhost:7244/jwks/does-not-exist
kill $APP_PID
```
Replace `7244` with whatever `launchSettings.json` shows.

- [ ] Step 3: If everything works, commit any final tweaks.

---

## Self-review checklist

After all tasks:

1. `dotnet build` — 0 errors.
2. `dotnet ef migrations list --project src/AdvantageTool` shows `InitialCreate`.
3. `LtiAdvantage` and `LtiAdvantage.IdentityModel` are the only LTI-related package refs.
4. No remaining references to `Newtonsoft.Json`, `BouncyCastle`, `BuildBundlerMinifier`, `RandomNameGeneratorLibrary`, `Microsoft.ApplicationInsights`.
5. `appsettings.json` has no instrumentation keys.
6. README does not start with "out of date".
