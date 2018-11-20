using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace AdvantageTool.Data
{
    public class AdvantageToolUser : IdentityUser
    {
        public Client Client { get; set; }
        public ICollection<Platform> Platforms { get; set; }
    }
}
