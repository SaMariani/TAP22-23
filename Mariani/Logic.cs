using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mariani
{

    public class MarianiHostFactory : IHostFactory
    {
        public Host? Host { get; set; }
        public void CreateHost(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString))
                throw new AuctionSiteArgumentNullException("connection strings cannot be null or empty");
            DomainConstraints.Connectionstring = connectionString;
            using (var c = new MarianiContext(connectionString))
            {
                try
                {
                    c.Database.EnsureDeleted();
                    c.Database.EnsureCreated();
                    Host = new Host(connectionString);
                }
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable Db", e);
                }
                catch (Exception e)
                {
                    throw new AuctionSiteUnavailableDbException("Unexpected error", e);
                }
            }
        }

        public IHost LoadHost(string connectionString, IAlarmClockFactory alarmClockFactory)
        {
            if (String.IsNullOrEmpty(connectionString))
                throw new AuctionSiteArgumentNullException("connection strings cannot be null or empty");
            if (alarmClockFactory == null)
                throw new AuctionSiteArgumentNullException("AlarmClock cannot be null");
            using (var c = new MarianiContext(connectionString))
            {
                try
                {
                    c.Database.EnsureCreated();
                    if (Host == null) throw new AuctionSiteInvalidOperationException();
                    if (Host.ConnectionString != connectionString) throw new AuctionSiteInvalidOperationException();
                    Host.AlarmClockFactory = alarmClockFactory;
                    return Host;
                }
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable Db", e);
                }
                catch (Exception e)
                {
                    throw new AuctionSiteUnavailableDbException("Unexpected error", e);
                }
            }
        }
    }
    public class Host : IHost
    {
        public string ConnectionString { get; set; }
        public IAlarmClockFactory AlarmClockFactory { get; set; }
        public Host(string connectionString)
        {
            ConnectionString = connectionString;
            //AlarmClockFactory = alarmClockFactory;
        }
        public IEnumerable<(string Name, int TimeZone)> GetSiteInfos()
        {
            using (var db = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                {
                    var i = db.Sites
                        .Select(s =>
                            new ValueTuple<string, int>(s.Name, s.Timezone)
                        ).ToList();
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

        public void CreateSite(string name, int timezone, int sessionExpirationTimeInSeconds, double minimumBidIncrement)
        {
            if (DomainConstraints.MinTimeZone > timezone || DomainConstraints.MaxTimeZone < timezone) throw new AuctionSiteArgumentOutOfRangeException("timezone must be between -12, 12");
            if(string.Empty==name) throw new AuctionSiteArgumentException("names cannot be null or empty");
            if (0 >= minimumBidIncrement) throw new AuctionSiteArgumentOutOfRangeException("minimum bid increment must be positive");
            if (0 >= sessionExpirationTimeInSeconds) throw new AuctionSiteArgumentOutOfRangeException("session expiration time must be positive");
            if(null==name) throw new AuctionSiteArgumentNullException("");
            var site = new Site(name, timezone, sessionExpirationTimeInSeconds, minimumBidIncrement, AlarmClockFactory.InstantiateAlarmClock(timezone), ConnectionString);
            using (var c = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                {
                    c.Sites.Add(site);
                    c.SaveChanges();
                }
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable Db", e);
                }
                catch (DbUpdateException e)
                {
                    //TODO verify codes
                    throw new AuctionSiteNameAlreadyInUseException("cannot insert");
                }
                catch (Exception e)
                {
                    throw new AuctionSiteConcurrentChangeException();
                }
            }
        }

        public ISite LoadSite(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new AuctionSiteArgumentNullException("name cannot be null or empty");
            using (var c = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                {
                    var site = c.Sites.Single(u => u.Name == name);
                    var alarmClock = AlarmClockFactory.InstantiateAlarmClock(site.Timezone);
                    var alarm = alarmClock.InstantiateAlarm(5 * 60 * 1000);
                    return new Site(site.Name,site.Timezone,site.SessionExpirationInSeconds,site.MinimumBidIncrement, alarmClock, ConnectionString);
                }
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable Db", e);
                }
                catch (DbUpdateException e)
                {
                    //TODO verify codes
                    throw new AuctionSiteNameAlreadyInUseException("cannot insert");
                }
                catch (Exception e)
                {
                    throw new AuctionSiteInexistentNameException("Unavailable Db");
                }
            }
            
        }
    }

    public class Session : ISession
    {
        [NotMapped] public bool Status = true;
        [Key]
        public string Id { get; set; }
        public DateTime ValidUntil { get; set; }
        [NotMapped] public IUser User { get; set; }
        public User Owner { get; set; }
        public int  UserId { get; set; }
        public Site Site { get; set; }
        public int SiteId { get; set; }
        //public List<Auction> Auctions { get; set; }
        [NotMapped]
        IAlarmClock AlarmClock { get; set; }

        [NotMapped]
        public string ConnectionString { get; set; }
        [NotMapped]
        public double MinumumBidIncrement { get; set; }
        [NotMapped]
        public int SessionExpirationInSeconds { get; set; }

        public Session() { }
        public Session(string id, DateTime validUntil, string connectionString, int userId, IUser owner, int siteId, IAlarmClock alarmClock, double minumumBidIncrement, int sessionExpirationInSeconds)
        {
            Id = id;
            ValidUntil = validUntil;
            ConnectionString = connectionString;
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
                    var seller = c.Users.Single(u => u.UserId == UserId);
                    var a = new Auction(seller, seller.Username, description, endsOn, 
                        ConnectionString, 1, AlarmClock, startingPrice, MinumumBidIncrement, Id);
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

            return null;//////////////
        }

        /*public void UpdatesSessionsValidUntil()
        {
            ValidUntil = AlarmClock.Now.AddSeconds(Site.SessionExpirationInSeconds);
        }*/
    }
}