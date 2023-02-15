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
using System.Reflection.Metadata;
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
        public IAlarmClockFactory? AlarmClockFactory { get; set; }
        public Host(string connectionString)
        {
            ConnectionString = connectionString;
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
            var site = new Site(name, timezone, sessionExpirationTimeInSeconds, minimumBidIncrement, AlarmClockFactory.InstantiateAlarmClock(timezone));
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
                    var alarmClock = AlarmClockFactory?.InstantiateAlarmClock(site.Timezone);
                    var alarm = alarmClock?.InstantiateAlarm(5 * 60 * 1000);
                    if (alarm != null) alarm.RingingEvent += site.EventHandler;
                    return new Site(site.Name,site.Timezone,site.SessionExpirationInSeconds,site.MinimumBidIncrement, alarmClock);
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

}