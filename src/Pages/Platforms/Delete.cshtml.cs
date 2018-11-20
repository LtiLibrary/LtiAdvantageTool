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
        private readonly UserManager<AdvantageToolUser> _userManager;

        public DeleteModel(ApplicationDbContext context, UserManager<AdvantageToolUser> userManager)
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

            var platform = await _context.Platforms.FindAsync(id);
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
