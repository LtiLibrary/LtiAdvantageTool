using AdvantageTool.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages.Platforms;

public class DeleteModel(ApplicationDbContext context) : PageModel
{
    [BindProperty] public Platform Platform { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await context.Platforms.FindAsync(id);
        if (entity is null) return NotFound();
        Platform = entity;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var entity = await context.Platforms.FindAsync(id);
        if (entity is not null)
        {
            context.Platforms.Remove(entity);
            await context.SaveChangesAsync();
        }
        return RedirectToPage("./Index");
    }
}
