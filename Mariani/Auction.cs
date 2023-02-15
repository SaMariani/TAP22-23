using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace Mariani;

public class Auction : IAuction
{
    public int Id { get; set; }
    [NotMapped] public bool Status = true;
    [NotMapped] public IUser Seller { get; set; }

    public string Description { get; set; }
    public DateTime EndsOn { get; set; }
    public string SellerUsername { get; set; }
    [NotMapped] public User? CurrentlyWinner { get; set; }
    public string? WinnerUsername { get; set; }
    public Session Session { get; set; }
    public string SessionId { get; set; }

    [NotMapped] IAlarmClock AlarmClock { get; set; }
    public double Price { get; set; }
    public double CurrentMaximumOffer { get; set; }
    public double MinimumBidIncrement { get; set; }

    public Auction() { }

    public Auction(IUser seller, string sellerUsername, string description, DateTime endsOn,
        IAlarmClock alarmClock, double startingPrice,
        double minimumBidIncrement, string sessionId)
    {
        Seller = seller;
        SellerUsername = sellerUsername;
        Description = description;
        EndsOn = endsOn;
        
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
        using (var c = new MarianiContext(DomainConstraints.Connectionstring))
        {
            try
            {
                var a = c.Auctions.Single(a => a.Id == Id);
                return a.Price;
            }
            catch (SqlException e)
            {
                throw new AuctionSiteUnavailableDbException("Unavailable Db", e);
            }
        }
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
                if (s == null) return true;
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
        if (0 > offer) throw new AuctionSiteArgumentOutOfRangeException();
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
                if (offer < CurrentMaximumOffer + MinimumBidIncrement)
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
                    Price = offer + MinimumBidIncrement;
                    return false;
                }

                if (offer > CurrentMaximumOffer && offer < CurrentMaximumOffer + MinimumBidIncrement)
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
                    Price = CurrentMaximumOffer + MinimumBidIncrement;
                    CurrentlyWinner = new User(session.User.Username);
                    auction.WinnerUsername = session.User.Username;
                    auction.Price = Price;
                    auction.CurrentMaximumOffer = CurrentMaximumOffer;
                    c.SaveChanges();
                    return true;
                }

                if (offer > Price + MinimumBidIncrement && offer < CurrentMaximumOffer)
                {
                    Price = offer + MinimumBidIncrement;
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