using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace Mariani
{
    public class MarianiContext : TapDbContext
    {
        public MarianiContext(string connectionString) : base(new DbContextOptionsBuilder<MarianiContext>().UseSqlServer(connectionString).Options) { }
        //public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }

        public DbSet<Site> Sites { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<Auction> Auctions { get; set; }

    }
    [Index(nameof(Name), IsUnique = true, Name = "NameUnique")]

    public class Site : ISite
    {
        public int SiteId { get; set; }
        public string Name { get; set; }
        //todo check estremi range
        [Range(DomainConstraints.MinTimeZone, DomainConstraints.MaxTimeZone)]
        public int Timezone { get; set; }
        [Range(0, int.MaxValue)]
        public int SessionExpirationInSeconds { get; set; }
        [Range(0, int.MaxValue)]
        public double MinimumBidIncrement { get; set; }
        public List<User> Users { get; set; }
        [NotMapped]
        public IAlarmClock AlarmClock { get; set; }
        [NotMapped]
        public string ConnectionString { get; set; }
        public Site(string name, int timezone, int sessionExpirationInSeconds, double minimumBidIncrement, IAlarmClock alarmClock, string connectionString)
        {
            Name = name;
            Timezone = timezone;
            SessionExpirationInSeconds = sessionExpirationInSeconds;
            MinimumBidIncrement = minimumBidIncrement;
            Users = new List<User>();
            AlarmClock = alarmClock;
            ConnectionString = connectionString;
        }

        public Site(string name, int timezone, int sessionExpirationInSeconds, double minimumBidIncrement)
        {
            Name = name;
            Timezone = timezone;
            SessionExpirationInSeconds = sessionExpirationInSeconds;
            MinimumBidIncrement = minimumBidIncrement;
            Users = new List<User>();
        }

        public IEnumerable<IUser> ToyGetUsers()
        {
            using (var db = new MarianiContext(ConnectionString))
            {
                try
                {
                    var i = db.Users.ToList();
                    return i;
                }//todo check exceptions
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable Db", e);
                }
                catch (DbUpdateException e)
                {
                    //TODO verify that codes etc confirm my guess
                    throw new AuctionSiteNameAlreadyInUseException("cannot insert");
                }
                catch (Exception e)
                {
                    throw new AuctionSiteConcurrentChangeException();
                }
            }
        }

        public IEnumerable<ISession> ToyGetSessions()
        {
            using (var db = new MarianiContext(ConnectionString))
            {
                try
                {
                    var i = db.Sessions.ToList();
                    return i;
                }//todo check exceptions
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable Db", e);
                }
                catch (DbUpdateException e)
                {
                    //TODO verify that codes etc confirm my guess
                    throw new AuctionSiteNameAlreadyInUseException("cannot insert");
                }
                catch (Exception e)
                {
                    throw new AuctionSiteConcurrentChangeException();
                }
            }
        }

        public IEnumerable<IAuction> ToyGetAuctions(bool onlyNotEnded)
        {
            using (var db = new MarianiContext(ConnectionString))
            {
                try
                {
                    var i = db.Auctions.ToList();
                    return i;
                }//todo check exceptions
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable Db", e);
                }
                catch (DbUpdateException e)
                {
                    //TODO verify that codes etc confirm my guess
                    throw new AuctionSiteNameAlreadyInUseException("cannot insert");
                }
                catch (Exception e)
                {
                    throw new AuctionSiteConcurrentChangeException();
                }
            }
        }

        public ISession? Login(string username, string password)
        {
            throw new NotImplementedException();
        }

        public void CreateUser(string username, string password)
        {
            throw new NotImplementedException();
        }

        public void Delete()
        {
            throw new NotImplementedException();
        }

        public DateTime Now()
        {
            if (AlarmClock == null) throw new AuctionSiteInvalidOperationException("error");
            return AlarmClock.Now;
        }
    }
    

    public class User : IUser
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public User(string username)
        {
            Username = username;
        }
        public IEnumerable<IAuction> WonAuctions()
        {
            throw new NotImplementedException();
        }

        public void Delete()
        {
            throw new NotImplementedException();
        }

    }

    public class Auction : IAuction
    {
        public int AuctionId { get; set; }
        public int Id { get; }
        public IUser Seller { get; }
        public string Description { get; }
        public DateTime EndsOn { get; }
        public IUser? CurrentWinner()
        {
            throw new NotImplementedException();
        }

        public double CurrentPrice()
        {
            throw new NotImplementedException();
        }

        public void Delete()
        {
            throw new NotImplementedException();
        }

        public bool Bid(ISession session, double offer)
        {
            throw new NotImplementedException();
        }

    }
}