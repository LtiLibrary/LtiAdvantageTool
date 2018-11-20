using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Pages.Platforms;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AdvantageToolUser> _userManager;

        public IList<PlatformModel> Platforms { get; set; }

        public IndexModel(ApplicationDbContext context, UserManager<AdvantageToolUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task OnGetAsync()
        {
            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
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
                            JsonWebKeySetUrl = c.JsonWebKeySetUrl
                        })
                        .ToListAsync();
                }
            }
        }
    }
}
