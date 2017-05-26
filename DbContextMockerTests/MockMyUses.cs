using DbContextMocker;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Linq;
using Xunit;

namespace DbContextMockerTests
{
   public class MockMyUses
    {
        readonly MusketeerDbContext db;

        public MockMyUses()
        {
            var users = new[] { new SecUser { UserId = 1, FirstName = "Test", Surname = "User", UserName = "TestUser" } };
            db = MockDbContext.For<MusketeerDbContext>().Add(x => x.Users, users).Create();
        }

        [Fact]
        public void GetUserAsync_UserMatches()
        {
            var user = db.Users.Where(u => u.UserName == "TestUser").First();
            Assert.Equal(1, user.UserId);
        }

        public void Dispose()
        {
            db?.Dispose();
        }
    }

    public class MusketeerDbContext : DbContext
    {
        public virtual IDbSet<IbsCommunity> Communities { get; set; }
        public virtual IDbSet<SecUser> Users { get; set; }
    }

    [Table("IBS_COMMUNITY")]
    public class IbsCommunity
    {
        [Key][Column("COMMUNITYID")]
        public int CommunityId { get; set; }

        [Column("SHORTDESCRIPTION")]
        public string ShortDescription { get; set; }

        public virtual ICollection<SecUser> Users { get; set; }
    }

    [Table("SEC_USER")]
    public class SecUser
    {
        [Key][Column("USERID")]
        public int UserId { get; set; }

        [Column("USERNAME", TypeName = "VARCHAR2")]
        [StringLength(100)]
        [Required(AllowEmptyStrings = false)]
        public string UserName { get; set; }

        [Column("FIRSTNAME", TypeName = "VARCHAR2")]
        [StringLength(50)]
        [Required(AllowEmptyStrings = false)]
        public string FirstName { get; set; }

        [Column("SURNAME", TypeName = "VARCHAR2")]
        [StringLength(50)]
        [Required(AllowEmptyStrings = false)]
        public string Surname { get; set; }

        [Column("LASTLOGGEDCOMMUNITYID")]
        [ForeignKey(nameof(LastLoggedCommunity))]
        public int? LastLoggedCommunityId { get; set; }

        [Column("INSTANCEID")]
        //[ForeignKey(nameof(Instance))]
        public int? InstanceId { get; set; }

        public virtual IbsCommunity LastLoggedCommunity { get; set; }
        //public virtual IbsInstance Instance { get; set; }
    }
}
