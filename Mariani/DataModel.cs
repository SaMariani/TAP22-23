using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using ISite = TAP22_23.AuctionSite.Interface.ISite;

namespace Mariani
{
    public class MarianiContext : TapDbContext
    {
        public MarianiContext(string connectionString) : base(new DbContextOptionsBuilder<MarianiContext>().UseSqlServer(connectionString).Options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //base.OnModelCreating(modelBuilder);
            var session = modelBuilder.Entity<Session>();
            var u = modelBuilder.Entity<User>();
            session.HasOne(c => c.Owner).WithOne().OnDelete(DeleteBehavior.NoAction);
        }

        public DbSet<Site> Sites { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<Auction> Auctions { get; set; }

    }

}