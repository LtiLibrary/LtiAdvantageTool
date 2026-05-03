using AdvantageTool.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages.Platforms;

public class CreateModel(ApplicationDbContext context) : PageModel
{
    [BindProperty] public PlatformInputModel Platform { get; set; } = new();

    public IActionResult OnGet()
    {
        Platform = new PlatformInputModel(Request, Url);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (context.Platforms.Any(p => p.Issuer == Platform.Issuer))
            ModelState.AddModelError($"{nameof(Platform)}.{nameof(Platform.Issuer)}", "Issuer already registered.");
        if (!ModelState.IsValid) return Page();

        var user = await context.GetUserAsync(User);
        var entity = new Platform { UserId = user!.Id };
        Platform.UpdateEntity(entity);
        context.Platforms.Add(entity);
        await context.SaveChangesAsync();
        return RedirectToPage("./Index");
    }
}
