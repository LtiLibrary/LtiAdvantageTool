using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Identity;

namespace AdvantageTool.Pages.Clients
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

        public IList<ClientModel> Clients { get; set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            Clients = await _context.Clients
                .Where(c => c.UserId == user.Id)
                .Select(c => new ClientModel
                {
                    PlatformAccessTokenUrl = c.PlatformAccessTokenUrl,
                    ClientId = c.ClientId,
                    Name = c.Name,
                    Id = c.Id,
                    PlatformIssuer = c.PlatformIssuer,
                    PlatformJsonWebKeysUrl = c.PlatformJsonWebKeysUrl
                })
                .ToListAsync();
        }
    }
}
