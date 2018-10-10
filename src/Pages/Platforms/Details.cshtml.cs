using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Identity;

namespace AdvantageTool.Pages.Platforms
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public DetailsModel(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public Platform Platform { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            Platform = await _context.Platforms
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == user.Id);

            if (Platform == null)
            {
                return NotFound();
            }
            return Page();
        }
    }
}
