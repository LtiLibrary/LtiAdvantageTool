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
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly UserManager<AdvantageToolUser> _userManager;

        public EditModel(ApplicationDbContext context, 
            IHttpClientFactory httpClientFactory,
            UserManager<AdvantageToolUser> userManager)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _userManager = userManager;
        }

        public Client Client { get; set; }

        [BindProperty]
        public PlatformModel Platform { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var platform = user.Platforms.SingleOrDefault(p => p.Id == id);
            if (platform == null)
            {
                return NotFound();
            }

            Client = user.Client;
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

            var platform = await _context.Platforms.FindAsync(Platform.Id);
            Platform.UpdateEntity(platform);

            _context.Platforms.Update(platform);

            try
            {
                await _context.SaveChangesAsync();
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
            return _context.Platforms.Any(e => e.Id == id);
        }
    }
}
