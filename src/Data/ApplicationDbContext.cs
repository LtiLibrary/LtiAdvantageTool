using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Data
{
    public class ApplicationDbContext : IdentityDbContext<AdvantageToolUser>
    {
        // ReSharper disable once SuggestBaseTypeForParameter
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Platform> Platforms { get; set; }
        
        /// <summary>
        /// Returns the fully populated <see cref="AdvantageToolUser"/> corresponding to the
        /// IdentityOptions.ClaimsIdentity.UserIdClaimType claim in the principal or null.
        /// </summary>
        /// <param name="principal">The principal which contains the user id claim.</param>
        /// <returns>The user corresponding to the IdentityOptions.ClaimsIdentity.UserIdClaimType claim in
        /// the principal or null</returns>
        public async Task<AdvantageToolUser> GetUserAsync(ClaimsPrincipal principal)
        {
            if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }
            var id = GetUserId(principal);
            return await GetUserAsync(id);
        }

        /// <summary>
        /// Returns the fully populated <see cref="AdvantageToolUser"/> corresponding to the
        /// IdentityOptions.ClaimsIdentity.UserIdClaimType claim in the principal or null.
        /// </summary>
        /// <param name="id">The user id.</param>
        /// <returns>The user corresponding to the user id.</returns>
        public async Task<AdvantageToolUser> GetUserAsync(string id)
        {
            if (id == null)
            {
                return null;
            }

            return await Users
                .Include(u => u.Platforms)
                .SingleOrDefaultAsync(u => u.Id == id);
        }

        public string GetUserId(ClaimsPrincipal principal)
        {
            if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }
            return principal.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        public async Task<Platform> GetPlatformByIssuerAsync(string issuer)
        {
            return await Platforms
                .Where(p => p.Issuer == issuer)
                .SingleOrDefaultAsync();
        }
    }
}
