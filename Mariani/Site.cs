using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;
using System.Xml.Linq;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;
using System.Security.Cryptography;
using System.Text;

namespace Mariani
{
    
    [Index(nameof(Name), IsUnique = true, Name = "NameUnique")]
    public class Site : ISite
    {
        [NotMapped] public bool Status { get; set; }
        public int SiteId { get; set; }

        [MaxLength(DomainConstraints.MaxSiteName)]
        [MinLength(DomainConstraints.MinSiteName)]
        public string Name { get; set; }

        [Range(DomainConstraints.MinTimeZone, DomainConstraints.MaxTimeZone)]
        public int Timezone { get; set; }

        [Range(0, int.MaxValue)] public int SessionExpirationInSeconds { get; set; }
        [Range(0, int.MaxValue)] public double MinimumBidIncrement { get; set; }
        public List<User> Users { get; set; }
        [NotMapped] public IAlarmClock AlarmClock { get; set; }

        public Site(){}
        public Site(string name, int timezone, int sessionExpirationInSeconds, double minimumBidIncrement,
            IAlarmClock alarmClock)
        {
            Status = true;
            Name = name;
            Timezone = timezone;
            SessionExpirationInSeconds = sessionExpirationInSeconds;
            MinimumBidIncrement = minimumBidIncrement;
            Users = new List<User>();
            AlarmClock = alarmClock;
        }

        public void EventHandler()
        {
            /*var sessions = ToyGetSessions();
            foreach (var session in sessions)
            {
                if (AlarmClock.Now > session.ValidUntil)
                {
                    session.Logout();
                }
            }*/
        }

        public IEnumerable<IUser> ToyGetUsers()
        {
            if (!Status) throw new AuctionSiteInvalidOperationException("site doesn't exist");
            using (var db = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                {
                    List<IUser> allUsers = new List<IUser>();
                    var i = db.Users.Include(u => u.Site).Where(u => u.Site.Name == Name).ToList();
                    foreach (var item in i)
                    {
                        allUsers.Add(item);
                    }

                    return allUsers;
                } //todo check exceptions
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
            if (!Status) throw new AuctionSiteInvalidOperationException("site doesn't exist");
            List<Session> expiredSessions = new List<Session>();
            List<Session> validSessions = new List<Session>();

            using (var db = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                {
                    var ses = db.Sessions.Include(s => s.Site).Where(s => s.Site.Name == Name).ToList();

                    foreach (var item in ses)
                    {
                        if (AlarmClock.Now > item.ValidUntil)
                        {
                            expiredSessions.Add(item);
                            continue;
                        }

                        var user = db.Users.SingleOrDefault(u => u.UserId == item.UserId);
                        validSessions.Add(new Session(item.Id, item.ValidUntil, item.UserId,
                            user, item.SiteId, AlarmClock, MinimumBidIncrement, SessionExpirationInSeconds));
                    }


                } //todo check exceptions
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable Db", e);
                }
            }

            foreach (var item in expiredSessions)
            {
                item.Logout();
            }

            return validSessions;
        }


        public IEnumerable<IAuction> ToyGetAuctions(bool onlyNotEnded)
        {
            if (!Status) throw new AuctionSiteInvalidOperationException("site doesn't exist");

            using (var db = new MarianiContext(DomainConstraints.Connectionstring))
            {
                var auctions = new List<Auction>();
                var ac = db.Auctions.Include(a => a.Session).ThenInclude(s => s.Site)
                    .Where(a => a.Session.Site.Name == Name).ToList();
                foreach (var auction in ac)
                {
                    var user = db.Users.Single(u => u.Username == auction.SellerUsername);
                    var a = new Auction(user, user.Username, auction.Description, auction.EndsOn,
                        AlarmClock, auction.Price,
                        auction.MinimumBidIncrement,
                        auction.SessionId);
                    a.Id = auction.Id;
                    auctions.Add(a);
                }

                if (!onlyNotEnded) return auctions;
                var aux = new List<Auction>(auctions);
                foreach (var item in aux)
                {
                    if (AlarmClock.Now > item.EndsOn)
                        auctions.Remove(item);
                }

                return auctions;
            }
        }


        public ISession? Login(string username, string password)
        {
            if (!Status) throw new AuctionSiteInvalidOperationException("site doesn't exist");
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                throw new AuctionSiteArgumentNullException("credentials cannot be null");
            using (var c = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                {
                    var owner = c.Users.SingleOrDefault(u => u.Username == username);
                    if (owner == null) return null; //// check sul testo del progetto
                    var hashpassword = owner.HashPassword;
                    if (!PasswordWithPbkdf2.VerifyPassword(password, hashpassword)) return null;
                    var s = c.Sessions.SingleOrDefault(s => s.UserId == owner.UserId && s.SiteId == owner.SiteId);
                    if (s != null)
                    {
                        s.ValidUntil = AlarmClock.Now.AddSeconds(SessionExpirationInSeconds);
                        c.SaveChanges();
                        return s;
                    } //update sessions valid time

                    var randomGen = new Random();
                    var ses = new Session(randomGen.Next(10000000).ToString(),
                        AlarmClock.Now.AddSeconds(SessionExpirationInSeconds), owner.UserId, owner,
                        owner.SiteId, AlarmClock, MinimumBidIncrement, SessionExpirationInSeconds);
                    c.Sessions.Add(ses);
                    c.SaveChanges();
                    return ses;
                }
                catch (InvalidOperationException e) //non ho trovato utenti???
                {
                    return null;
                }
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable Db", e);
                }
                catch (DbUpdateException e)
                {
                    //TODO verify that codes etc confirm my guess
                    throw new AuctionSiteArgumentException("no user with these credentials", e);
                }
            }
        }

        public void CreateUser(string username, string password)
        {
            if (!Status) throw new AuctionSiteInvalidOperationException("site doesn't exist");
            if (String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password))
                throw new AuctionSiteArgumentNullException("credentials cannot be null");
            if (DomainConstraints.MinUserName > username.Length || DomainConstraints.MaxUserName < username.Length)
                throw new AuctionSiteArgumentException();
            if (DomainConstraints.MinUserPassword > password.Length)
                throw new AuctionSiteArgumentException();

            using (var c = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                {
                    var s = c.Sites.Single(s => s.Name == Name);
                    var hashpassword = PasswordWithPbkdf2.HashPasword(password);
                    var user = new User(username, hashpassword, s.SiteId);
                    c.Users.Add(user);
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
                    throw new AuctionSiteInexistentNameException("Unavailable Db");
                }
            }
        }

        public void Delete()
        {
            if (!Status) throw new AuctionSiteInvalidOperationException();
            Status = false;
            using (var c = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                {
                    var site = c.Sites.Single(s => s.Name == Name); //TODO deal with exceptions
                    c.Sites.Remove(site);
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
                    throw new AuctionSiteInvalidOperationException();
                }
            }
        }

        public DateTime Now()
        {
            if (!Status) throw new AuctionSiteInvalidOperationException("site doesn't exist");
            if (AlarmClock == null) throw new AuctionSiteInvalidOperationException("error");
            return AlarmClock.Now;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Site);
        }

        public bool Equals(Site other)
        {
            return other != null &&
                   SiteId == other.SiteId;
        }

        public override int GetHashCode()
        {
            return SiteId.GetHashCode();
        }

    }


    public class PasswordWithPbkdf2
    {
        private const int KeySize = 64;
        private const int Iterations = 350000;
        private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA512;
        private static readonly byte[] Salt = RandomNumberGenerator.GetBytes(KeySize);

        public static string HashPasword(string password)
        {

            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                Salt,
                Iterations,
                HashAlgorithm,
                KeySize);
            return Convert.ToHexString(hash);
        }

        public static bool VerifyPassword(string password, string hash)
        {
            var hashToCompare = Rfc2898DeriveBytes.Pbkdf2(password, Salt, Iterations, HashAlgorithm, KeySize);
            return hashToCompare.SequenceEqual(Convert.FromHexString(hash));
        }
    }

}