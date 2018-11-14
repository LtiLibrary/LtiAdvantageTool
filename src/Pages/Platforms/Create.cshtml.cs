using System.Net.Http;
using System.Threading.Tasks;
using AdvantageTool.Data;
using IdentityModel.Client;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages.Platforms
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _appContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateModel(ApplicationDbContext appContext, 
            IHttpClientFactory httpClientFactory,
            UserManager<IdentityUser> userManager)
        {
            _appContext = appContext;
            _httpClientFactory = httpClientFactory;
            _userManager = userManager;
        }

        [BindProperty]
        public PlatformModel Platform { get; set; }

        public IActionResult OnGet()
        {
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

            var user = await _userManager.GetUserAsync(User);
            var platform = new Platform
            {
                AccessTokenUrl = Platform.AccessTokenUrl,
                ClientId = Platform.ClientId,
                ClientPrivateKey = Platform.ClientPrivateKey.IsPresent() 
                    ? Platform.ClientPrivateKey.Replace("\r\n\r\n", "\r\n")
                    : null,
                ClientSecret = Platform.ClientSecret,
                Name = Platform.Name,
                Issuer = Platform.Issuer,
                JsonWebKeySetUrl = Platform.JsonWebKeySetUrl,
                UserId = user.Id
            };

            _appContext.Platforms.Add(platform);
            await _appContext.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}