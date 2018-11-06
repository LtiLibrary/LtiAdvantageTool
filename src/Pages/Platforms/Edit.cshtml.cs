using System.Linq;
using System.Threading.Tasks;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Pages.Platforms
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public EditModel(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
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

            var client = await _context.Platforms.FindAsync(id);
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
                PlatformAccessTokenUrl = client.PlatformAccessTokenUrl,
                ToolClientId = client.ClientId,
                PlatformName = client.Name,
                Id = client.Id,
                PlatformIssuer = client.PlatformIssuer,
                PlatformJsonWebKeysUrl = client.PlatformJsonWebKeysUrl
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var platform = await _context.Platforms.FindAsync(Platform.Id);
            platform.ClientId = Platform.ToolClientId;
            platform.Name = Platform.PlatformName;
            platform.PlatformAccessTokenUrl = Platform.PlatformAccessTokenUrl;
            platform.PlatformIssuer = Platform.PlatformIssuer;
            platform.PlatformJsonWebKeysUrl = Platform.PlatformJsonWebKeysUrl;
            if (!string.IsNullOrEmpty(Platform.ToolClientSecret))
            {
                platform.ClientSecret = Platform.ToolClientSecret;
            }

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
