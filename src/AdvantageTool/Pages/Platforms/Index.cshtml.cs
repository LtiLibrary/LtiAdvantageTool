using AdvantageTool.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages.Platforms;

public class IndexModel(ApplicationDbContext context) : PageModel
{
    public IList<Platform> Platforms { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var user = await context.GetUserAsync(User);
        Platforms = user?.Platforms.OrderBy(p => p.Name).ToList() ?? [];
    }
}
