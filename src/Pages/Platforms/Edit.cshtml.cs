using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using IdentityModel.Client;
using LtiAdvantageLibrary.Utilities;
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

            await Platform.DiscoverEndpoints(_httpClientFactory);
            
            if (Platform.ClientPublicKey.IsMissing())
            {
                Platform.ClientPublicKey = RsaHelper.GetPublicKeyStringFromPrivateKey(Platform.ClientPrivateKey);
            }

            var platform = await _appContext.Platforms.FindAsync(Platform.Id);
            Platform.FillEntity(platform);

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
