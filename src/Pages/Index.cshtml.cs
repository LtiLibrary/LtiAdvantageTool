using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Pages.Clients;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public IList<ClientModel> Clients { get; set; }

        public IndexModel(ApplicationDbContext context, UserManager<IdentityUser> userManager)
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
                    Clients = await _context.Clients
                        .Where(c => c.UserId == user.Id)
                        .Select(c => new ClientModel
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
    }
}
