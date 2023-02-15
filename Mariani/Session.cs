using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace Mariani
{

    public class Session : ISession
    {
        [NotMapped] public bool Status = true;
        [Key]
        public string Id { get; set; }
        public DateTime ValidUntil { get; set; }
        [NotMapped] public IUser User { get; set; }
        public User Owner { get; set; }
        public int UserId { get; set; }
        public Site Site { get; set; }
        public int SiteId { get; set; }
        //public List<Auction> Auctions { get; set; }
        [NotMapped]
        IAlarmClock AlarmClock { get; set; }
        
        [NotMapped]
        public double MinumumBidIncrement { get; set; }
        [NotMapped]
        public int SessionExpirationInSeconds { get; set; }

        public Session() { }
        public Session(string id, DateTime validUntil, int userId, IUser owner, int siteId, IAlarmClock alarmClock, double minumumBidIncrement, int sessionExpirationInSeconds)
        {
            Id = id;
            ValidUntil = validUntil;
            //Auctions = new List<Auction>();
            UserId = userId;
            User = owner;
            SiteId = siteId;
            AlarmClock = alarmClock;
            MinumumBidIncrement = minumumBidIncrement;
            SessionExpirationInSeconds = sessionExpirationInSeconds;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Session);
        }

        public bool Equals(Session other)
        {
            return other != null &&
                   Id.Equals(other.Id);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public void Logout()
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

        public IAuction CreateAuction(string description, DateTime endsOn, double startingPrice)
        {
            if (!Status) throw new AuctionSiteInvalidOperationException("session expired");
            if (description == null || endsOn == null) throw new AuctionSiteArgumentNullException();
            if (String.Empty == description) throw new AuctionSiteArgumentException();
            if (endsOn < AlarmClock.Now) throw new AuctionSiteUnavailableTimeMachineException();
            if (0 > startingPrice) throw new AuctionSiteArgumentOutOfRangeException();
            if (ValidUntil < AlarmClock.Now)
            {
                Logout();
                throw new AuctionSiteInvalidOperationException();
            }

            using (var c = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                {
                    var seller = c.Users.Single(u => u.UserId == UserId);/*/*//*/*/
                    var a = new Auction(seller, seller.Username, description, endsOn, 1, AlarmClock, startingPrice, MinumumBidIncrement, Id);
                    c.Auctions.Add(a);
                    var s = c.Sessions.Single(s => s.Id == Id);
                    s.ValidUntil = AlarmClock.Now.AddSeconds(SessionExpirationInSeconds);
                    ValidUntil = AlarmClock.Now.AddSeconds(SessionExpirationInSeconds);
                    c.SaveChanges();
                    return a;
                }
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable Db", e);
                }
            }

            return null;
        }
        
    }

}