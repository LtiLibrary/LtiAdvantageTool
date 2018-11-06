using System.Threading.Tasks;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages.Platforms
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateModel(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public PlatformModel Platform { get; set; }

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (string.IsNullOrEmpty(Platform.ToolClientSecret))
            {
                ModelState.AddModelError("Platform.ToolClientSecret", "The Client Secret field is required.");
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            var client = new Platform
            {
                PlatformAccessTokenUrl = Platform.PlatformAccessTokenUrl,
                ClientId = Platform.ToolClientId,
                ClientSecret = Platform.ToolClientSecret,
                Name = Platform.PlatformName,
                PlatformIssuer = Platform.PlatformIssuer,
                PlatformJsonWebKeysUrl = Platform.PlatformJsonWebKeysUrl,
                UserId = user.Id
            };

            _context.Platforms.Add(client);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}