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

    [Index(nameof(Username), IsUnique = true, Name = "UsernameUnique")]

    public class User : IUser
    {
        [NotMapped] public bool Status = true;
        public int UserId { get; set; }
        [MinLength(DomainConstraints.MinUserName)]
        [MaxLength(DomainConstraints.MaxUserName)]
        public string Username { get; }
        public string HashPassword { get; set; } = string.Empty;
        [NotMapped]
        public string ConnectionString { get; set; }
        public Site Site { get; set; }
        public int SiteId { get; set; }
        //public List<Auction> Auctions { get; set; }
        public User(){}

        public User(string username)
        {
            Username = username;
        }

        public User(string username, string hashPassword, string connectionString, int siteId)
        {
            Username = username;
            HashPassword = hashPassword;
            ConnectionString = connectionString;
            SiteId = siteId;
            //Auctions = new List<Auction>();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as IUser);
        }

        public bool Equals(IUser other)
        {
            return other != null &&
                   Username.Equals(other.Username);
        }

        public override int GetHashCode()
        {
            return UserId.GetHashCode();
        }
        public IEnumerable<IAuction> WonAuctions()
        {
            using (var db = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                {
                    //var i = db.Auctions.Where(a => a.CurrentlyWinner != null && a.CurrentlyWinner.Username == Username).ToList();
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
            }
        }

        public void Delete()
        {
            if (!Status) throw new AuctionSiteInvalidOperationException();
            Status = false;
            using (var c = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try {
                    var user = c.Users.Single(u => u.UserId == UserId); //TODO deal with exceptions
                    c.Users.Remove(user);
                    c.SaveChanges();
                }
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

    }

    public class Auction : IAuction
    {
        public int Id { get; set; }
        [NotMapped]
        public bool Status = true;
        [NotMapped] public IUser Seller { get; set; }
        
        public string Description { get; set; }
        public DateTime EndsOn { get; set; }
        public string SellerUsername { get; set; }
        [NotMapped]
        public User? CurrentlyWinner { get; set; }
        public string? WinnerUsername { get; set; }
        public Session Session { get; set; }
        public string SessionId { get; set; }
        [NotMapped]
        public string ConnectionString { get; set; }
        [NotMapped]
        IAlarmClock AlarmClock { get; set; }
        public double Price { get; set; }
        public double CurrentMaximumOffer { get; set; }
        public double MinimumBidIncrement { get; set; }

        public Auction(){}
        
        public Auction(IUser seller, string sellerUsername, string description, DateTime endsOn, 
            string connectionString, int? currentlyWinnerUserId, 
            IAlarmClock alarmClock, double startingPrice, 
            double minimumBidIncrement, string sessionId)
        {
            Seller = seller;
            SellerUsername = sellerUsername;
            Description = description;
            EndsOn = endsOn;
            ConnectionString = connectionString;
            //CurrentlyWinnerUserId = currentlyWinnerUserId;
            AlarmClock = alarmClock;
            Price = startingPrice;
            MinimumBidIncrement = minimumBidIncrement;
            CurrentMaximumOffer = 0;
            SessionId = sessionId;
            WinnerUsername = null;
        }

        public IUser? CurrentWinner()
        {
            using (var c = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                { 
                    var a = c.Auctions.Single(a => a.Id == Id);
                    if (a.WinnerUsername == null) return null; 
                    return new User(a.WinnerUsername);
                }
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

        public double CurrentPrice()
        {
            return Price;
            /*using (var c = new MarianiContext(ConnectionString))
            {
                var a = c.Auctions.Single(a => a.Id == Id);
                if (CurrentMaximumOffer == 0)//è la prima offerta
                {
                    return Price;//return starting price
                }

                return Price;
                //return Price + MinimumBidIncrement;
            }*/
        }

        public void Delete()
        {
            if (!Status) throw new AuctionSiteInvalidOperationException();
            Status = false;
            using (var db = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                {
                    db.Remove(this);
                    db.SaveChanges();
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

        private bool SessionIsExpired(ISession session)
        {
            if (session == null) throw new AuctionSiteInvalidOperationException();
            
            using (var c = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                {
                    var s = c.Sessions.SingleOrDefault(a => a.Id == session.Id);
                    if(s == null) return true;
                    if (AlarmClock.Now > s.ValidUntil)
                    {
                        s.Logout();
                        return true;
                    }

                    return false;
                }
                catch (Exception e)
                {
                    throw;
                }
                
            }
        }

        void UpdateValidSessionTime(Session session)
        {
            using (var c = new MarianiContext(DomainConstraints.Connectionstring))
            {
                var s = c.Sessions.Single(a => a.Id == session.Id);
                s.ValidUntil = AlarmClock.Now.AddHours(session.SessionExpirationInSeconds);
                session.ValidUntil = AlarmClock.Now.AddHours(session.SessionExpirationInSeconds);
                c.SaveChanges();
            }
        }

        public bool Bid(ISession session, double offer)
        {
            if (AuxBid(session, offer))
            {
                UpdateValidSessionTime((Session)session);
                return true;
            }

            return false;
        }
        public bool AuxBid(ISession session, double offer)
        {
            if (session == null) throw new AuctionSiteArgumentNullException();
            if (SessionIsExpired(session)) throw new AuctionSiteArgumentException();
            if (0>offer) throw new AuctionSiteArgumentOutOfRangeException();
            if (!Status) throw new AuctionSiteInvalidOperationException();
            using (var c = new MarianiContext(DomainConstraints.Connectionstring))
            {
                var auction = c.Auctions.Single(a => a.Id == Id);
                if (CurrentMaximumOffer == 0) // prima offerta
                {
                    if (offer < Price)// se < starting price
                    {
                        return false;
                    }
                    CurrentMaximumOffer = offer;
                    CurrentlyWinner = new User(session.User.Username);
                    auction.WinnerUsername = session.User.Username;
                    auction.Price = Price;
                    auction.CurrentMaximumOffer = CurrentMaximumOffer;
                    c.SaveChanges();
                    return true;
                }
                else if (session.User.Username == CurrentlyWinner?.Username)
                {
                    if (offer < CurrentMaximumOffer+MinimumBidIncrement)
                    {
                        return false;
                    }
                    CurrentMaximumOffer = offer;
                    auction.Price = Price;
                    auction.CurrentMaximumOffer = CurrentMaximumOffer;
                    c.SaveChanges();
                    return true;
                }
                else
                {
                    if (offer < Price + MinimumBidIncrement)
                    {
                        Price = offer+MinimumBidIncrement;
                        return false;
                    }

                    if (offer > CurrentMaximumOffer && offer < CurrentMaximumOffer+MinimumBidIncrement)
                    {
                        Price = offer;
                        CurrentMaximumOffer = offer;
                        CurrentlyWinner = new User(session.User.Username);
                        auction.WinnerUsername = session.User.Username;
                        auction.Price = Price;
                        auction.CurrentMaximumOffer = CurrentMaximumOffer;
                        c.SaveChanges();
                        return true;
                    }
                    if (offer > CurrentMaximumOffer)
                    {
                        Price = CurrentMaximumOffer+MinimumBidIncrement;
                        CurrentlyWinner = new User(session.User.Username);
                        auction.WinnerUsername = session.User.Username;
                        auction.Price = Price;
                        auction.CurrentMaximumOffer = CurrentMaximumOffer;
                        c.SaveChanges();
                        return true;
                    }

                    if (offer > Price+MinimumBidIncrement && offer < CurrentMaximumOffer) // offer-max < minimum
                    {
                        Price = offer+MinimumBidIncrement;
                        auction.Price = Price;
                        c.SaveChanges();
                        return true;
                    }
                    
                }
            }
            return false;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Auction);
        }

        public bool Equals(Auction other)
        {
            return other != null &&
                   Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

    }
}