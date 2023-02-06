using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;
using System;

namespace Mariani
{

    public class MarianiHostFactory : IHostFactory
    {
        public void CreateHost(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString))
                throw new AuctionSiteArgumentNullException("connection strings cannot be null or empty");
            using (var c = new MarianiContext(connectionString))
            {
                try
                {
                    c.Database.EnsureDeleted();
                    c.Database.EnsureCreated();
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
            return new Host(connectionString, alarmClockFactory);
        }
    }
    public class Host : IHost
    {
        public string ConnectionString { get; set; }
        public IAlarmClockFactory AlarmClockFactory { get; set; }
        public Host(string connectionString, IAlarmClockFactory alarmClockFactory)
        {
            ConnectionString = connectionString;
            AlarmClockFactory = alarmClockFactory;
        }
        public IEnumerable<(string Name, int TimeZone)> GetSiteInfos()
        {
            using (var db = new MarianiContext(ConnectionString))
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
            using (var c = new MarianiContext(ConnectionString))
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
            using (var c = new MarianiContext(ConnectionString))
            {
                try
                {
                    var site = c.Sites.Single(u => u.Name == name);
                    return new Site(site.Name,site.Timezone,site.SessionExpirationInSeconds,site.MinimumBidIncrement, AlarmClockFactory.InstantiateAlarmClock(site.Timezone), ConnectionString);
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
        public int SessionId { get; set; }
        public string Id { get; }
        public DateTime ValidUntil { get; }
        public IUser User { get; }

        public void Logout()
        {
            throw new NotImplementedException();
        }

        public IAuction CreateAuction(string description, DateTime endsOn, double startingPrice)
        {
            throw new NotImplementedException();
        }
    }
}