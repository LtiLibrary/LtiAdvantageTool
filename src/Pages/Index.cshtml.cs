using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public IList<Client> Clients { get; set; }
        public IList<Platform> Platforms { get; set; }

        public IndexModel(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            Clients = await _context.Clients
                .Where(c => c.UserId == user.Id)
                .ToListAsync();
            Platforms = await _context.Platforms
                .Where(p => p.UserId == user.Id)
                .ToListAsync();
        }
    }
}
