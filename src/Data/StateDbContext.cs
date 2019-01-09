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

        public State GetState(string id)
        {
            var state = States.Find(id);
            if (state != null)
            {
                States.Remove(state);
                SaveChanges();
            }

            return state;
        }
    }
}
