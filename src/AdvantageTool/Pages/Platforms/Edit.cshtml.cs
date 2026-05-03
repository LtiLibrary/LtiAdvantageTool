using AdvantageTool.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages.Platforms;

public class EditModel(ApplicationDbContext context) : PageModel
{
    [BindProperty] public PlatformInputModel Platform { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await context.Platforms.FindAsync(id);
        if (entity is null) return NotFound();
        Platform = new PlatformInputModel(Request, Url, entity);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!ModelState.IsValid) return Page();
        var entity = await context.Platforms.FindAsync(id);
        if (entity is null) return NotFound();
        Platform.UpdateEntity(entity);
        await context.SaveChangesAsync();
        return RedirectToPage("./Index");
    }
}
