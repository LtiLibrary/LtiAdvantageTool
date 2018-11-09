using System.Threading.Tasks;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages.Platforms
{
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public DeleteModel(ApplicationDbContext context, UserManager<IdentityUser> userManager)
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
                AccessTokenUrl = client.AccessTokenUrl,
                ClientId = client.ClientId,
                Name = client.Name,
                Id = client.Id,
                Issuer = client.Issuer,
                JsonWebKeysUrl = client.JsonWebKeysUrl
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var client = await _context.Platforms.FindAsync(id);
            if (client != null)
            {
                _context.Platforms.Remove(client);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
