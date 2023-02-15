using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;
using TAP22_23.AuctionSite.Interface;

namespace Mariani
{

    [Index(nameof(Username), IsUnique = true, Name = "UsernameUnique")]

    public class User : IUser
    {
        [NotMapped] public bool Status = true;
        public int UserId { get; set; }

        [MinLength(DomainConstraints.MinUserName)]
        [MaxLength(DomainConstraints.MaxUserName)]
        public string Username { get; }

        public string HashPassword { get; set; } = string.Empty;/// <summary>
        /// ////////////////////////////////////////////////////////////////////////////
        /// </summary>
        public Site Site { get; set; }
        public int SiteId { get; set; }

        //public List<Auction> Auctions { get; set; }
        public User()
        {
        }

        public User(string username)
        {
            Username = username;
        }

        public User(string username, string hashPassword, int siteId)
        {
            Username = username;
            HashPassword = hashPassword;
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
            return Username.GetHashCode();
        }

        public IEnumerable<IAuction> WonAuctions()
        {
            using (var db = new MarianiContext(DomainConstraints.Connectionstring))
            {
                try
                {
                    var i = db.Auctions.Where(a => a.WinnerUsername == Username).ToList();

                    return i;
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

}