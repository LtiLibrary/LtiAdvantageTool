using System.Net.Http;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using IdentityModel.Client;
using LtiAdvantageLibrary.Utilities;
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
            var keyPair = RsaHelper.GenerateRsaKeyPair();

            Platform = new PlatformModel
            {
                ClientPrivateKey = keyPair.PrivateKey,
                ClientPublicKey = keyPair.PublicKey
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            await Platform.DiscoverEndpoints(_httpClientFactory);

            if (Platform.ClientPublicKey.IsMissing())
            {
                Platform.ClientPublicKey = RsaHelper.GetPublicKeyStringFromPrivateKey(Platform.ClientPrivateKey);
            }

            var user = await _userManager.GetUserAsync(User);
            var platform = new Platform { UserId = user.Id };
            Platform.FillEntity(platform);

            _appContext.Platforms.Add(platform);
            await _appContext.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}