using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Identity;

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

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public Platform Platform { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Cleanup the public key
            Platform.PublicKey = Platform.PublicKey.Replace("\r\n\r\n", "\r\n");

            // Add the user ID
            var user = await _userManager.GetUserAsync(User);
            Platform.UserId = user.Id;

            await _context.Platforms.AddAsync(Platform);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}