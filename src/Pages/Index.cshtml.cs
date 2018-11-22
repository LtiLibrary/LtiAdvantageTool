using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Pages.Platforms;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IList<PlatformModel> Platforms { get; set; }

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnGetAsync()
        {
            if (User.Identity.IsAuthenticated)
            {
                var user = await _context.GetUserAsync(User);
                if (user != null)
                {
                    Platforms = user.Platforms
                        .OrderBy(c => c.Name)
                        .Select(c => new PlatformModel
                        {
                            AccessTokenUrl = c.AccessTokenUrl,
                            Name = c.Name,
                            Id = c.Id,
                            Issuer = c.Issuer,
                            JwkSetUrl = c.JwkSetUrl
                        })
                        .ToList();
                }
            }
        }
    }
}
