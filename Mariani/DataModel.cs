using Microsoft.EntityFrameworkCore;
using TAP22_23.AuctionSite.Interface;

namespace Mariani;

public class MarianiContext : TapDbContext
{
    public MarianiContext(string connectionString) : base(new DbContextOptionsBuilder<MarianiContext>().UseSqlServer(connectionString).Options) { }
    //public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}