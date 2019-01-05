using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Data
{
    public class StateDbContext : DbContext
    {
        public StateDbContext(DbContextOptions<StateDbContext> options) : base(options)
        {}

        public DbSet<State> States { get; set; }

        public void AddState(string id, string value)
        {
            States.Add(new State {Id = id, Value = value});
            SaveChanges();
        }
    }
}
