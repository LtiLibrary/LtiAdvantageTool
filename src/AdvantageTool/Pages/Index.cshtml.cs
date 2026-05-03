using AdvantageTool.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages;

public class IndexModel(ApplicationDbContext context) : PageModel
{
    public int PlatformCount { get; private set; }

    public async Task OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated != true) return;
        var user = await context.GetUserAsync(User);
        PlatformCount = user?.Platforms.Count ?? 0;
    }
}
