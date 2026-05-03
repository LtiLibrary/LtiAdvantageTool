using System.Security.Claims;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<AdvantageToolUser>(options)
{
    public DbSet<Platform> Platforms => Set<Platform>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Platform>().HasIndex(p => p.PlatformId).IsUnique();
        builder.Entity<Platform>().HasIndex(p => p.Issuer).IsUnique();
    }

    public Task<AdvantageToolUser?> GetUserAsync(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return id is null
            ? Task.FromResult<AdvantageToolUser?>(null)
            : Users.Include(u => u.Platforms).SingleOrDefaultAsync(u => u.Id == id);
    }

    public Task<Platform?> GetPlatformByIssuerAsync(string issuer)
        => Platforms.SingleOrDefaultAsync(p => p.Issuer == issuer);

    public Task<Platform?> GetPlatformByPlatformIdAsync(string platformId)
        => Platforms.SingleOrDefaultAsync(p => p.PlatformId == platformId);
}
