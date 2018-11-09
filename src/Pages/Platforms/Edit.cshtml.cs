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

            var client = await _appContext.Platforms.FindAsync(id);
            if (client == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (client.UserId != user.Id)
            {
                return NotFound();
            }
            
            Platform = new PlatformModel
            {
                Id = client.Id,
                AccessTokenUrl = client.AccessTokenUrl,
                ClientId = client.ClientId,
                ClientPrivateKey = client.ClientPrivateKey,
                ClientSecret = client.ClientSecret,
                Issuer = client.Issuer,
                JsonWebKeysUrl = client.JsonWebKeysUrl,
                Name = client.Name
            };

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
            if (Platform.AccessTokenUrl.IsMissing() || Platform.JsonWebKeysUrl.IsMissing())
            {
                var httpClient = _httpClientFactory.CreateClient();
                var disco = await httpClient.GetDiscoveryDocumentAsync(Platform.Issuer);
                if (!disco.IsError)
                {
                    Platform.AccessTokenUrl = disco.TokenEndpoint;
                    Platform.JsonWebKeysUrl = disco.JwksUri;
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
            platform.JsonWebKeysUrl = Platform.JsonWebKeysUrl;

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
