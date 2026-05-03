using AdvantageTool.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Pages.Platforms;

public class DetailsModel(ApplicationDbContext context) : PageModel
{
    public PlatformInputModel? Platform { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await context.Platforms.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null) return NotFound();
        Platform = new PlatformInputModel(Request, Url, entity);
        return Page();
    }
}
