using AdvantageTool.Data;
using AdvantageTool.Utility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Pages.Platforms;

public class DetailsModel(ApplicationDbContext context) : PageModel
{
    public PlatformInputModel? Platform { get; private set; }
    public string? PreviousKeyId { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await context.Platforms.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null) return NotFound();
        Platform = new PlatformInputModel(Request, Url, entity);
        PreviousKeyId = entity.PreviousKeyId;
        return Page();
    }

    /// <summary>
    /// Rotates the tool's key for this platform. The new key signs from now on; the old one
    /// stays in the JWKS so tokens already signed with it (and platforms that cached the
    /// JWKS) keep working until it is retired.
    /// </summary>
    public async Task<IActionResult> OnPostRotateKeyAsync(int id)
    {
        var entity = await context.Platforms.FindAsync(id);
        if (entity is null) return NotFound();

        var pair = PemHelper.GenerateRsaKeyPair();
        entity.PreviousKeyId = entity.KeyId;
        entity.PreviousPublicKey = entity.PublicKey;
        entity.KeyId = pair.KeyId;
        entity.PrivateKey = pair.PrivateKey;
        entity.PublicKey = pair.PublicKey;
        await context.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    /// <summary>Stops publishing the previous key once nothing signed with it is still in flight.</summary>
    public async Task<IActionResult> OnPostRetirePreviousKeyAsync(int id)
    {
        var entity = await context.Platforms.FindAsync(id);
        if (entity is null) return NotFound();

        entity.PreviousKeyId = null;
        entity.PreviousPublicKey = null;
        await context.SaveChangesAsync();
        return RedirectToPage(new { id });
    }
}
