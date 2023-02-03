using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace Mariani
{

    public class MarianiHostFactory : IHostFactory
    {
        public Host? Host { get; set; }
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
            if (alarmClockFactory==null) 
                throw new AuctionSiteArgumentNullException("AlarmClock cannot be null");
            if (Host == null) throw new AuctionSiteUnavailableDbException("Db has not been initialized yet");
            if (Host.ConnectionString!=connectionString)
                throw new AuctionSiteUnavailableDbException("Unavailable Db");
            return Host;
        }
    }
    public class Host : IHost
    {
        public string ConnectionString { get; set; }
        public Host(string connectionString)
        {
            ConnectionString = connectionString;
        }
        public IEnumerable<(string Name, int TimeZone)> GetSiteInfos()
        {
            throw new NotImplementedException();
        }

        public void CreateSite(string name, int timezone, int sessionExpirationTimeInSeconds, double minimumBidIncrement)
        {
            throw new NotImplementedException();
        }

        public ISite LoadSite(string name)
        {
            throw new NotImplementedException();
        }
    }
}