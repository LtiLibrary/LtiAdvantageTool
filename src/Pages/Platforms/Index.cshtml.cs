using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

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

        public IList<PlatformModel> Platforms { get; set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            Platforms = await _context.Platforms
                .Where(c => c.UserId == user.Id)
                .OrderBy(c => c.Name)
                .Select(c => new PlatformModel
                {
                    AccessTokenUrl = c.AccessTokenUrl,
                    ClientId = c.ClientId,
                    Name = c.Name,
                    Id = c.Id,
                    Issuer = c.Issuer,
                    JsonWebKeysUrl = c.JsonWebKeysUrl
                })
                .ToListAsync();
        }
    }
}
