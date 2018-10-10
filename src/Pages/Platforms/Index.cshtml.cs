using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Identity;

namespace AdvantageTool.Pages.Platforms
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public IndexModel(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IList<Platform> Platforms { get;set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            Platforms = await _context.Platforms
                .Where(p => p.UserId == user.Id)
                .ToListAsync();
        }
    }
}
