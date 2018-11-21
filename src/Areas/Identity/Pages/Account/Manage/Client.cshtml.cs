using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Areas.Identity.Pages.Account.Manage
{
    public class ClientModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ClientModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Client Client { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _context.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_context.GetUserId(User)}'.");
            }

            Client = user.Client;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            Client.PrivateKey = Client.PrivateKey.IsPresent()
                ? Client.PrivateKey.Replace("\r\n\r\n", "\r\n")
                : null;
            Client.PublicKey = Client.PublicKey.IsPresent()
                ? Client.PublicKey.Replace("\r\n\r\n", "\r\n")
                : null;

            _context.Attach(Client).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return Page();
        }
    }
}