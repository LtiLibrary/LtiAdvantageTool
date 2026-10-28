# LTI Advantage Tool (sample)

A minimal LTI 1.3 / LTI Advantage Tool built with the
[LtiAdvantage](https://github.com/LtiLibrary/LtiAdvantage) library and
ASP.NET Core 10. Demonstrates:

- OIDC third-party login initiation
- Resource-link launch with full id_token validation against the platform's JWKS
- Deep linking response (signed by the tool)
- Assignment and Grade Services: line items, results
- Names and Role Provisioning Service: course members
- A per-platform JWKS endpoint so the platform can validate tool-signed JWTs

## Running locally

Prerequisites:

- .NET 10 SDK.
- A checkout of [LtiAdvantage](https://github.com/LtiLibrary/LtiAdvantage) next
  to this repository. The sample builds against the library source, so the
  folders must sit side by side:

  ```
  some-folder/
  ├── LtiAdvantage/
  └── LtiAdvantageTool/
  ```

  This is temporary: once LtiAdvantage 4.0.0 is on NuGet the sample goes back
  to package references.

```sh
cd src/AdvantageTool
dotnet run
```

Then in a browser:

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

A sample LTI Advantage platform lives at
[LtiAdvantagePlatform](https://github.com/LtiLibrary/LtiAdvantagePlatform); its
README walks through connecting the two samples. In short, with the platform
running on `http://localhost:5099`:

| Tool field        | Value                                     |
|-------------------|-------------------------------------------|
| Issuer            | `http://localhost:5099/` (keep the slash) |
| Authorize URL     | `http://localhost:5099/connect/authorize` |
| Access token URL  | `http://localhost:5099/connect/token`     |
| JWKS URL          | `http://localhost:5099/.well-known/jwks`  |
| Client ID         | the Client ID shown on the platform       |

The Issuer must match the `iss` claim in the platform's id_token exactly, so
copy it from the platform rather than typing it.

The IMS reference implementation (lti-ri.imsglobal.org) also works.
