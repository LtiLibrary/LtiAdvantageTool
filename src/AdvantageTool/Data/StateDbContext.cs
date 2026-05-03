using Microsoft.EntityFrameworkCore;

namespace AdvantageTool.Data;

public class StateDbContext(DbContextOptions<StateDbContext> options) : DbContext(options)
{
    public DbSet<State> States => Set<State>();

    public void AddState(string nonce, string value)
    {
        States.Add(new State { Nonce = nonce, Value = value });
        SaveChanges();
    }

    public State? GetState(string nonce) => States.AsNoTracking().FirstOrDefault(s => s.Nonce == nonce);
}
