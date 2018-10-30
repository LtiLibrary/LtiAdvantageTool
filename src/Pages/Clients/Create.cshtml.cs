using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AdvantageTool.Data;
using Microsoft.AspNetCore.Identity;

namespace AdvantageTool.Pages.Clients
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
        public ClientModel Client { get; set; }

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

            var user = await _userManager.GetUserAsync(User);
            var client = new Client
            {
                AccessTokenUrl = Client.AccessTokenUrl,
                ClientId = Client.ClientId,
                ClientName = Client.ClientName,
                Issuer = Client.Issuer,
                JsonWebKeysUrl = Client.JsonWebKeysUrl,
                UserId = user.Id
            };

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}