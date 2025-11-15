using Data.DAL.Implementations;
using Data.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace TestsEF.DataTests
{
    public class AccountRepositoryTests : DataTestsBase
    {
        private readonly AccountRepository _repository;

        public AccountRepositoryTests()
        {
            _repository = CreateAccountRepository();
        }

        [Fact]
        public async Task CreateAccountAsync_WhenAccountIsNew_ShouldSaveAccountAndPlayer()
        {
            var newAccount = CreateValidUser("new@test.com", "Newbie");

            await _repository.CreateAccountAsync(newAccount);

            Assert.Equal(1, Context.UserAccount.Count());
            Assert.Equal(1, Context.Player.Count());

            var playerInDb = Context.Player.Include("UserAccount").First();
            Assert.Equal("new@test.com", playerInDb.UserAccount.Email);
        }

        [Fact]
        public async Task CreateAccountAsync_WhenEmailExists_ShouldThrowDuplicateAccountException()
        {
            var existingAccount = CreateValidUser("existing@test.com", "OldUser");
            Context.UserAccount.Add(existingAccount);
            await Context.SaveChangesAsync();

            var newAccount = CreateValidUser("existing@test.com", "Newbie");

            var exception = await Assert.ThrowsAsync<Data.Exceptions.DuplicateAccountException>(() =>
                _repository.CreateAccountAsync(newAccount)
            );

            Assert.Equal("An account with the email 'existing@test.com' already exists.", exception.Message);
        }

        [Fact]
        public void AccountAlreadyExist_WhenEmailExists_ReturnsTrue()
        {
            var existingAccount = CreateValidUser("existing@test.com", "TestUser");
            Context.UserAccount.Add(existingAccount);
            Context.SaveChanges();

            var result = _repository.AccountAlreadyExist("existing@test.com");
            Assert.True(result);
        }

        [Fact]
        public void AccountAlreadyExist_WhenEmailDoesNotExist_ReturnsFalse()
        {
            var result = _repository.AccountAlreadyExist("non-existing@test.com");
            Assert.False(result);
        }

        [Fact]
        public void GetUserByEmail_WhenUserExists_ReturnsUserWithIncludes()
        {
            var user = CreateValidUser("user@test.com", "TestUser");
            Context.UserAccount.Add(user);
            Context.SaveChanges();

            var player = new Player { Id = 10, UserAccountId = user.Id };
            var social = new SocialAccount { Provider = "GitHub", Username = "TestUser", UserAccountId = user.Id };
            Context.Player.Add(player);
            Context.SocialAccount.Add(social);
            Context.SaveChanges();

            var result = _repository.GetUserByEmail("user@test.com");

            Assert.NotNull(result);
            Assert.Equal("user@test.com", result.Email);
            Assert.NotNull(result.Player);
            Assert.True(result.Player.Any(p => p.Id == player.Id));
            Assert.NotNull(result.SocialAccount);
            Assert.Equal(1, result.SocialAccount.Count);
            Assert.Equal("GitHub", result.SocialAccount.First().Provider);
        }

        [Fact]
        public void GetUserByEmail_WhenUserDoesNotExist_ReturnsNull()
        {
            var result = _repository.GetUserByEmail("nouser@test.com");
            Assert.Null(result);
        }

        [Fact]
        public void VerifyEmail_WhenUserExists_UpdatesEmailVerifiedAndReturnsTrue()
        {
            var user = CreateValidUser("verify@test.com", "VerifyUser");
            user.EmailVerified = false;
            Context.UserAccount.Add(user);
            Context.SaveChanges();

            var result = _repository.VerifyEmail("verify@test.com");

            Assert.True(result);
            var userInDb = Context.UserAccount.First(u => u.Email == "verify@test.com");
            Assert.True(userInDb.EmailVerified);
        }

        [Fact]
        public void VerifyEmail_WhenUserDoesNotExist_ReturnsFalse()
        {
            var result = _repository.VerifyEmail("nouser@test.com");

            Assert.False(result);
            Assert.Equal(0, Context.UserAccount.Count());
        }

        [Fact]
        public void UpdateUserProfile_WhenUserExists_UpdatesDataAndManagesSocials()
        {
            var user = CreateValidUser("user@test.com", "OldUser");
            user.FirstName = "OldFirst";
            user.LastName = "OldLast";
            user.SocialAccount = new List<SocialAccount>
            {
                new SocialAccount { Provider = "GitHub", Username = "OldGitHub" },
                new SocialAccount { Provider = "Twitter", Username = "OldTwitter" }
            };

            var player = new Player { Id = 10, UserAccount = user };
            Context.Player.Add(player);
            Context.SaveChanges();

            var savedPlayerId = player.Id;

            var updatedUserData = new UserAccount
            {
                FirstName = "NewFirst",
                LastName = "NewLast",
                PhotoId = (byte)123
            };
            var updatedSocials = new List<SocialAccount>
            {
                new SocialAccount { Provider = "GitHub", Username = "NewGitHub" },
                new SocialAccount { Provider = "LinkedIn", Username = "NewLinkedIn" }
            };

            var result = _repository.UpdateUserProfile(savedPlayerId, updatedUserData, updatedSocials);

            Assert.True(result);

            var userInDb = Context.UserAccount.Include("SocialAccount").First(u => u.Id == user.Id);

            Assert.Equal("NewFirst", userInDb.FirstName);
            Assert.Equal("NewLast", userInDb.LastName);
            Assert.Equal((byte?)123, userInDb.PhotoId);
            Assert.Equal(2, userInDb.SocialAccount.Count);
            Assert.True(userInDb.SocialAccount.Any(s => s.Provider == "LinkedIn" && s.Username == "NewLinkedIn"));
            Assert.True(userInDb.SocialAccount.Any(s => s.Provider == "GitHub" && s.Username == "NewGitHub"));
            Assert.False(userInDb.SocialAccount.Any(s => s.Provider == "Twitter"));
        }
    }
}