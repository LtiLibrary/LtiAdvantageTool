using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AdvantageTool.Data;
using IdentityModel.Client;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Pages.Platforms
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _appContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly UserManager<IdentityUser> _userManager;

        public EditModel(ApplicationDbContext appContext, 
            IHttpClientFactory httpClientFactory,
            UserManager<IdentityUser> userManager)
        {
            _appContext = appContext;
            _httpClientFactory = httpClientFactory;
            _userManager = userManager;
        }

        [BindProperty]
        public PlatformModel Platform { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var platform = await _appContext.Platforms.FindAsync(id);
            if (platform == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (platform.UserId != user.Id)
            {
                return NotFound();
            }

            Platform = new PlatformModel(platform);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (Platform.ClientSecret.IsMissing() && Platform.ClientPrivateKey.IsMissing())
            {
                ModelState.AddModelError("Platform.ClientSecret", "Either Client Secret or Private Key is required.");
                ModelState.AddModelError("Platform.ClientPrivateKey", "Either Client Secret or Private Key is required.");
                return Page();
            }

            // Attempt to discover the platform urls
            if (Platform.AccessTokenUrl.IsMissing() || Platform.JsonWebKeySetUrl.IsMissing())
            {
                var httpClient = _httpClientFactory.CreateClient();
                var disco = await httpClient.GetDiscoveryDocumentAsync(Platform.Issuer);
                if (!disco.IsError)
                {
                    Platform.AccessTokenUrl = disco.TokenEndpoint;
                    Platform.JsonWebKeySetUrl = disco.JwksUri;
                }
            }

            var platform = await _appContext.Platforms.FindAsync(Platform.Id);
            platform.AccessTokenUrl = Platform.AccessTokenUrl;
            platform.ClientId = Platform.ClientId;
            platform.ClientPrivateKey = Platform.ClientPrivateKey.IsPresent() 
                ? Platform.ClientPrivateKey.Replace("\r\n\r\n", "\r\n")
                : null;
            platform.ClientSecret = Platform.ClientSecret;
            platform.Name = Platform.Name;
            platform.Issuer = Platform.Issuer;
            platform.JsonWebKeySetUrl = Platform.JsonWebKeySetUrl;

            _appContext.Platforms.Update(platform);

            try
            {
                await _appContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PlatformExists(platform.Id))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToPage("./Index");
        }

        private bool PlatformExists(int id)
        {
            return _appContext.Platforms.Any(e => e.Id == id);
        }
    }
}
