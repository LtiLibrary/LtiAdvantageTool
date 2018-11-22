using System;
using System.Linq;
using System.Threading.Tasks;
using AdvantageTool.Controllers;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages.Platforms
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public ClientModel Client { get; set; }
        public PlatformModel Platform { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.GetUserAsync(User);
            var platform = user.Platforms.SingleOrDefault(p => p.Id == id);
            if (platform == null)
            {
                return NotFound();
            }

            var jwksUrl = new Uri($"{Request.Scheme}://{Request.Host}").AbsoluteUri.EnsureTrailingSlash()
                          + JwksController.JwksUri;

            Client = new ClientModel(user.Client, jwksUrl);
            Platform = new PlatformModel(platform);

            return Page();
        }
    }
}
