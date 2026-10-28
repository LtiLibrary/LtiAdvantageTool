# LTI Advantage Tool (sample)

A minimal LTI 1.3 / LTI Advantage Tool built with the
[LtiAdvantage](https://github.com/LtiLibrary/LtiAdvantage) library and
ASP.NET Core 10. Demonstrates:

- LTI Dynamic Registration (tool side)
- OIDC third-party login initiation
- Resource-link launch validated with the library's `LtiTokenValidator`, plus the
  launch checks the LTI 1.3 Security Framework expects (see below)
- Deep linking response (signed by the tool) with several content item types,
  line items and messages
- Assignment and Grade Services: line items (with filters and paging), scores,
  results
- Names and Role Provisioning Service: course members (with filters and paging)
- Submission Review: launches from the platform's gradebook
- Course Groups: group sets, groups and their members
- Proctoring Services: start proctoring, start assessment, end assessment,
  and the Assessment Control Service
- The tool's JWKS, published with the library's `JwksControllerBase`, with key
  rotation

## Running locally

Prerequisites:

- .NET 10 SDK.
- The [LtiAdvantage](https://github.com/LtiLibrary/LtiAdvantage) 4.0.0 packages,
  restored from NuGet.

```sh
cd src/AdvantageTool
dotnet run
```

Then in a browser:

1. Open the printed URL.
2. Register a tool admin account (the password rules are deliberately lax —
   this is a sample).
3. Go to **Platforms → Add platform** and fill in your LMS's Issuer,
   Authorize URL, Access token URL, JWKS URL, Client ID and Deployment ID(s).
4. The page also shows the URLs to give the platform side: Login URL,
   Launch URL, Deep linking URL, JWKS URL, and the public key.

### Dynamic Registration

Instead of copying URLs and IDs by hand, give the platform the tool's
registration URL: `http://localhost:5072/register`. The platform opens it (in
an iframe or a popup) with `openid_configuration` and `registration_token`.
After you sign in as a tool admin and confirm, the tool:

1. fetches the platform's OpenID configuration
   (`GetPlatformOpenIdConfigurationAsync`, which also checks the issuer matches
   the URL),
2. posts its `ToolConfiguration` / `LtiToolConfiguration` to the platform's
   registration endpoint (`RegisterToolAsync`),
3. stores the returned client ID and deployment ID as a new platform with its
   own key pair, and
4. closes with the `org.imsglobal.lti.close` postMessage (to `window.opener`
   for a popup, `window.parent` for an iframe).

See `Pages/DynamicRegistration.cshtml.cs`. In production, fetch the platform
configuration with an `HttpClient` that refuses private addresses: the URL
comes from the browser.

## Where to look first

- `Pages/DynamicRegistration.cshtml.cs` — Dynamic Registration (tool side).
- `Pages/OidcLogin.cshtml.cs` — third-party login initiation.
- `Services/LaunchValidator.cs` — every check a launch goes through.
- `Pages/Tool.cshtml.cs` — the launch URL: validates, then hands off by message type.
- `Pages/Catalog.cshtml.cs` — deep linking response: select content,
  build and sign the `LtiDeepLinkingResponse`, post it back.
- `Pages/Components/LineItems/LineItemsViewComponent.cs` — token-protected
  AGS + NRPS calls using the LtiAdvantage models.
- `Pages/Review.cshtml.cs` — Submission Review launches.
- `Pages/Components/CourseGroups/CourseGroupsViewComponent.cs` — Course Groups.
- `Pages/Proctoring.cshtml.cs` and `Pages/Proctor/Index.cshtml.cs` — proctoring.
- `Services/AccessTokenService.cs` — JWT client-credentials flow.
- `Controllers/JwksController.cs` and `Services/PlatformJwksKeyStore.cs` — the
  tool's JWKS endpoint.

## Launch security

Every launch goes through `Services/LaunchValidator.cs`:

- **Login initiation** looks the registration up by issuer *and* `client_id`,
  so one platform can register the tool several times (as real LMSs do).
- **State bound to the browser.** The login sets a short-lived
  `lti1p3-state-{state}` cookie (`SameSite=None; Secure; Partitioned`) holding the
  nonce. The launch must come back with that cookie, and the id_token's nonce
  must match it.
- **id_token** is checked by the library's `LtiTokenValidator`: signature
  against the platform's JWKS (cached in a shared `JwksCache`, refreshed when the
  platform rotates its key, so not fetched per launch), `iss`, `aud`/`azp`,
  `exp`/`iat`, LTI version, `message_type`, nonce replay (`INonceStore`), and
  `deployment_id` against the deployment IDs registered for the platform.
- **Errors go back to the platform.** When a launch fails and the platform sent
  `launch_presentation.return_url` (on the platform's own origin), the user is
  sent there with `lti_errormsg` / `lti_errorlog`. Error details are only shown
  in Development.
- **No re-posted tokens.** A validated launch is kept server-side for an hour;
  pages and AGS/NRPS buttons only carry its opaque id.

## Keys and JWKS

Each platform registration gets its own RSA key pair (with its own `kid`). The
tool publishes all public keys in one JWKS at `/.well-known/jwks.json`, served by
the library's `JwksControllerBase` (`Controllers/JwksController.cs`) from an
`IJwksKeyStore` backed by the database (`Services/PlatformJwksKeyStore.cs`).
The platform picks the key by the `kid` in the JWT header.

**Rotate key** on a platform's details page generates a new key pair. The tool
signs with the new key straight away, and keeps publishing the old one next to
it until you press **Retire previous key**, so tokens already signed with it and
platforms that cached the JWKS keep working.

The old per-platform URL `/jwks/{platformId}` redirects to the new one.

## Deep linking

A deep linking launch opens the catalog (`Pages/Catalog.cshtml.cs`):

- It offers `ltiResourceLink`, `link`, `html`, `file` and `image` items, but only
  the types in the request's `accept_types`.
- An LTI link can carry a `lineItem` (score maximum, label, tag, resourceId),
  so the platform creates a gradebook column for it.
- With `accept_multiple: false` only one item can be picked.
- Links can open in an `iframe` or `window`, if the platform lists that target in
  `accept_presentation_document_targets`.
- Nothing is pre-selected. **Add selected** with nothing selected says so;
  **Cancel** returns no items with `errormsg` / `errorlog`. A normal return
  carries `msg` / `log`. `data` is echoed back unchanged.

Image `width`/`height` and file `expiresAt` are not set: LtiAdvantage currently
serializes them with the wrong JSON names.

## AGS and NRPS

The services panel on a resource link launch
(`Pages/Components/LineItems/`) shows:

- **Line items**, filtered by `resource_link_id`, `tag` and `resource_id`. New
  line items can carry a tag and resource ID to filter on.
- **Scores** with a chosen `activityProgress`, `gradingProgress`, score and
  comment, for any member of the course.
- **Results**, for everyone or one user (`user_id`).
- **Members**, filtered by `role` (full role URI) and `rlid`.

`limit` sets the page size for line items and members. The tool then follows
the platform's `Link: <…>; rel="next"` headers (`Utility/Paging.cs`) and shows
how many pages it read.

## Submission Review

Line items the tool creates carry a `submissionReview` property
(`SubmissionReviewProperty`) pointing at the tool's launch URL. The platform can
then launch an `LtiSubmissionReviewRequest` from its gradebook. The launch URL
sends it to `Pages/Review.cshtml.cs`, which shows the user in the `for_user`
claim and the results the tool posted for them on that line item (AGS results
with `user_id`).

## Course Groups

When the launch has the course groups service claim
(`CourseGroupsServiceClaimValueType`), the launch page lists the course's group
sets and groups (`GroupSetsControllerBase` / `GroupsControllerBase` on the
platform) and the members of each group. Group membership comes from Names &
Roles with `groups=true`, which adds each member's `group_enrollments` when the
token also holds the groups scope. See `Pages/Components/CourseGroups/`.

## Proctoring

A minimal proctoring tool flow (Proctoring Services 1.0):

1. The platform launches `LtiStartProctoring`. The tool shows a pretend
   identity check (`Pages/Proctoring.cshtml.cs`).
2. On confirm, the tool posts a signed `LtiStartAssessment` (as `JWT`) to the
   platform's `start_assessment_url`, echoing `session_data`, `resource_link`
   and `attempt_number`, with `verified_user` and `end_assessment_return: true`.
   The platform continues into the assessment.
3. A tool admin opens **Proctor** (`/Proctor`) to see the attempts. Its
   buttons call the platform's Assessment Control Service
   (`assessment_control_url` from the `acs` claim, scope `control.all`) with an
   `AssessmentControlRequest` for the actions the platform listed (pause,
   resume, terminate, flag, update), and show the returned status.
4. When the attempt ends, the platform may launch `LtiEndAssessment`; the tool
   marks the attempt as ended and shows any error message.

Attempts are kept in memory, so restarting the tool forgets them.

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
| Deployment IDs    | the Deployment ID shown on the platform   |

The Issuer must match the `iss` claim in the platform's id_token exactly, so
copy it from the platform rather than typing it.

Or skip the copying: register the tool from the platform with Dynamic
Registration, using `http://localhost:5072/register` as the registration URL.

The IMS reference implementation (lti-ri.imsglobal.org) also works.
