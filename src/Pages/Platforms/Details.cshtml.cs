using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AdvantageTool.Data;
using IdentityServer4.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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

        public PlatformModel Platform { get; set; }

        [Display(Name = "Tool Issuer", Description = "This is the Issuer for all messages that originate from this Tool.")]
        public string ToolIssuer { get; set; }

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
                PlatformAccessTokenUrl = client.PlatformAccessTokenUrl,
                ToolClientId = client.ClientId,
                PlatformName = client.Name,
                Id = client.Id,
                PlatformIssuer = client.PlatformIssuer,
                PlatformJsonWebKeysUrl = client.PlatformJsonWebKeysUrl
            };

            ToolIssuer = HttpContext.GetIdentityServerIssuerUri();

            return Page();
        }
    }
}
