# Update LtiAdvantageTool to .NET 10 + current LtiAdvantage

## Goal

Bring the LtiAdvantageTool sample app current with the LtiAdvantage library
(now targeting net8.0/net10.0 with OpenIddict, System.Text.Json, IdentityModel
7) and .NET 10. The sample should build, run cross-platform, and demonstrate
the four LTI Advantage capabilities a tool implements: resource link launch,
deep linking, Assignment & Grade Services, and Names & Roles Provisioning.

## Non-goals

- No platform-side functionality (this is a tool sample).
- No automated end-to-end test against a live LTI platform — verification is
  build, migration generation, and a smoke test of the home / register /
  create-platform / JWKS pages.
- No multi-project restructure. Single Web project under `src/AdvantageTool`.

## Project layout

```
LtiAdvantageTool/
├── AdvantageTool.sln
├── README.md
├── .gitignore
└── src/AdvantageTool/
    ├── AdvantageTool.csproj          # net10.0, Microsoft.NET.Sdk.Web
    ├── Program.cs                    # WebApplication.CreateBuilder, top-level
    ├── appsettings.json
    ├── appsettings.Development.json
    ├── Data/                         # EF Core SQLite + Identity
    ├── Pages/                        # Razor Pages
    ├── Services/                     # AccessTokenService, JwksKeyStore
    ├── Utility/
    └── wwwroot/                      # bootstrap 5 from template
```

## Dependencies

### Add (NuGet)

- `LtiAdvantage` (latest stable)
- `LtiAdvantage.IdentityModel` (latest stable)
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- `Microsoft.AspNetCore.Identity.UI`
- `Microsoft.EntityFrameworkCore.Sqlite`
- `Microsoft.EntityFrameworkCore.Tools`
- `Microsoft.EntityFrameworkCore.Design`

### Remove

- `Microsoft.ApplicationInsights.AspNetCore`
- `BuildBundlerMinifier`
- `BouncyCastle.NetCore`
- `Newtonsoft.Json` (transitive only after removal)
- `RandomNameGeneratorLibrary`
- `IdentityModel` direct ref (comes transitively via `LtiAdvantage.IdentityModel`)
- `libman.json`, `bundleconfig.json`
- `Connected Services/` directory

## Hosting

- Replace `Startup.cs` + old `Program.cs` with a single minimal-hosting
  `Program.cs`.
- DI registrations: SQLite-backed `ApplicationDbContext`, in-memory
  `StateDbContext`, default Identity, Razor Pages with
  `AuthorizeFolder("/Platforms")`, `AddHttpClient`, `AccessTokenService`,
  `IJwksKeyStore`.
- Pipeline: HSTS + ExceptionHandler in production, dev exception page in
  Development, `UseStaticFiles`, `UseAuthentication`, `UseAuthorization`,
  Razor Pages, JWKS endpoint.
- Drop App Insights and the hardcoded instrumentation key.
- `appsettings.json` connection string: `Data Source=AdvantageTool.db`.
- `*.db` and `*.db-*` (SQLite WAL/SHM) added to `.gitignore`.

## Data

- Keep `AdvantageToolUser : IdentityUser`, `Platform`, `State`,
  `ApplicationDbContext`, `StateDbContext`. Same shapes.
- Delete the existing SQL Server EF migrations under
  `src/Data/Migrations/`. Generate a single SQLite `InitialCreate` migration.
- Auto-migrate on startup so first run does not require manual EF tooling.

## Tool side: launch (OidcLogin → Tool)

`OidcLogin.cshtml.cs`: largely unchanged. Trim dead code; build the
authorize redirect URL using `IdentityModel.Client.RequestUrl` (still
available via the transitive `IdentityModel` 7.x).

`Tool.cshtml.cs`:

- Use `System.Text.Json` everywhere; replace
  `JsonConvert.DeserializeObject<JsonWebKeySet>(json)` with
  `Microsoft.IdentityModel.Tokens.JsonWebKeySet` constructor or
  `JsonSerializer.Deserialize`.
- Keep `JwtSecurityTokenHandler.ValidateToken` with platform-specific signing
  keys derived from the platform's JWKS.
- Build `LtiResourceLinkRequest` / `LtiDeepLinkingRequest` from
  `validatedToken.Payload`.
- Replace `JwtHeader.ToJsonString()` calls in views with
  `header.SerializeToJson()` / `payload.SerializeToJson()`.

## Tool side: JWKS endpoint (new)

A real LTI 1.3 platform needs the tool's public keys to verify deep linking
responses signed by the tool. The current sample never exposed its own JWKS.
We add it.

- `Services/PlatformJwksKeyStore.cs` implements `LtiAdvantage.Jwks.IJwksKeyStore`,
  taking a `platformId` (route parameter) and returning the tool's public key
  for that platform registration as a `JsonWebKey` with `Use="sig"`,
  `Alg="RS256"`, stable `Kid`.
- A minimal-API endpoint:
  `app.MapGet("/{platformId}/.well-known/jwks.json", ...)` returns the JWKS
  JSON. Per-platform path so each platform registration has its own key
  pair (matches existing per-platform `PrivateKey` storage).
- Surface this URL in `PlatformModel` so the management UI shows it
  alongside `LaunchUrl` / `LoginUrl`.

## Tool side: AGS, NRPS, Deep Linking

- `Pages/Components/LineItems/LineItemsViewComponent.cs` keeps shape;
  `JsonConvert.*` → `JsonSerializer.*` with
  `LtiAdvantage.Utilities.JsonOptions.Default`. AGS list / NRPS membership /
  results all flow through the library's models.
- `AccessTokenService` keeps shape; PEM signing rewritten on top of
  built-in `RSA.ImportFromPem`.
- `Pages/Catalog.cshtml.cs` (deep linking):
  - Replace `RandomNameGeneratorLibrary` with a small static seed list (a
    handful of made-up activities — enough to demonstrate the picker).
  - JWT signing via the built-in RSA helper.

## PEM / RSA utility

`Utility/PemHelper.cs` becomes ~15 lines:

```csharp
public static SigningCredentials SigningCredentialsFromPem(string pem)
{
    var rsa = RSA.Create();
    rsa.ImportFromPem(pem);
    return new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
}

public static RsaKeyPair GenerateRsaKeyPair()
{
    using var rsa = RSA.Create(2048);
    return new RsaKeyPair
    {
        PrivateKey = rsa.ExportRSAPrivateKeyPem(),
        PublicKey  = rsa.ExportSubjectPublicKeyInfoPem(),
        KeyId      = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)),
    };
}
```

BouncyCastle removed.

## Frontend

- Base layout and site CSS from the current `dotnet new webapp` template
  (Bootstrap 5, no jQuery, no FontAwesome, no CDNs).
- Tool / Catalog page UI keeps the same semantic shape (cards for platform,
  user, context, resource link, AGS, NRPS), restyled to Bootstrap 5.
- Drop the inline App Insights script from `Tool.cshtml`.
- Drop FontAwesome usages (just text or Bootstrap Icons if needed).

## README

Rewrite from the current "this code is out of date" stub. Cover:

- What the sample demonstrates.
- How to run it (`dotnet run`, register tool admin, create a platform
  registration, copy URLs).
- Where to look first: `Pages/OidcLogin.cshtml.cs`,
  `Pages/Tool.cshtml.cs`, `Pages/Catalog.cshtml.cs`,
  `Services/PlatformJwksKeyStore.cs`,
  `Pages/Components/LineItems/`.
- Pointer to the LtiAdvantage library docs and the platform sample.
- Note that the platform side is in a separate repo
  (`LtiAdvantagePlatform`).

## Verification

- `dotnet build` succeeds without warnings.
- `dotnet ef migrations list` shows a single `InitialCreate` migration.
- `dotnet run` starts; home page renders; register/login works;
  create-platform saves to SQLite; JWKS endpoint returns valid JSON;
  Tool page renders without an id_token (showing the missing-token error).
- End-to-end LTI flows are documented in the README and not automated.
