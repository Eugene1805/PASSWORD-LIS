using System.Linq;
using System.Threading.Tasks;
using Data.DAL.Implementations;
using Xunit;

namespace TestsEF.DataTests
{
    public class FriendshipRepositoryTests : DataTestsBase
    {
        private readonly FriendshipRepository _repository;

        public FriendshipRepositoryTests()
        {
            _repository = CreateFriendshipRepository();
            SeedPlayers(3, emailPrefix: "user", nicknamePrefix: "User", pointsSelector: i => i * 10, emailVerified: true);
        }

        [Fact]
        public async Task CreateFriendRequest_NewRequest_ReturnsTrue()
        {
            var (p1, _) = GetPlayerByEmail("user1@test.com");
            var (p2, _) = GetPlayerByEmail("user2@test.com");

            var result = await _repository.CreateFriendRequestAsync(p1, p2);
            Assert.True(result);

            using (var verify = NewContext())
            {
                Assert.Single(verify.Friendship.Where(f => f.RequesterId == p1 && f.AddresseeId == p2));
            }
        }

        [Fact]
        public async Task CreateFriendRequest_Duplicate_ReturnsFalse()
        {
            var (p1, _) = GetPlayerByEmail("user1@test.com");
            var (p2, _) = GetPlayerByEmail("user2@test.com");
            Assert.True(await _repository.CreateFriendRequestAsync(p1, p2));
            var duplicate = await _repository.CreateFriendRequestAsync(p1, p2);
            Assert.False(duplicate);
        }

        [Fact]
        public async Task GetPendingRequests_ForAddressee_ReturnsList()
        {
            var (requesterId, _) = GetPlayerByEmail("user1@test.com");
            var (addresseePlayerId, addresseeUserAccountId) = GetPlayerByEmail("user2@test.com");
            await _repository.CreateFriendRequestAsync(requesterId, addresseePlayerId);

            var pending = await _repository.GetPendingRequestsAsync(addresseeUserAccountId);
            Assert.Single(pending);
            Assert.Equal(requesterId, pending[0].RequesterId);
            Assert.Equal(addresseePlayerId, pending[0].AddresseeId);
            Assert.Equal(0, pending[0].Status);
        }

        [Fact]
        public async Task RespondToFriendRequest_Accept_SetsStatusToAccepted()
        {
            var (p1, _) = GetPlayerByEmail("user1@test.com");
            var (p2, _) = GetPlayerByEmail("user2@test.com");
            await _repository.CreateFriendRequestAsync(p1, p2);
            var responded = await  _repository.RespondToFriendRequestAsync(p1, p2, true);
            Assert.True(responded);

            using (var verify = NewContext())
            {
                var friendship = verify.Friendship.First(f => f.RequesterId == p1 && f.AddresseeId == p2);
                Assert.Equal(1, friendship.Status);
                Assert.NotNull(friendship.RespondedAt);
            }
        }

        [Fact]
        public async Task RespondToFriendRequest_Reject_RemovesRequest()
        {
            var (p1, _) = GetPlayerByEmail("user1@test.com");
            var (p2, _) = GetPlayerByEmail("user2@test.com");
            await _repository.CreateFriendRequestAsync(p1, p2);
            var responded = await _repository.RespondToFriendRequestAsync(p1, p2, false);
            Assert.True(responded);

            using (var verify = NewContext())
            {
                Assert.False(verify.Friendship.Any(f => f.RequesterId == p1 && f.AddresseeId == p2));
            }
        }

        [Fact]
        public async Task GetFriendsByUserAccountId_AfterAcceptance_ReturnsBothSides()
        {
            var (p1, u1) = GetPlayerByEmail("user1@test.com");
            var (p2, u2) = GetPlayerByEmail("user2@test.com");
            await _repository.CreateFriendRequestAsync(p1, p2);
            await _repository.RespondToFriendRequestAsync(p1, p2, true);

            var friendsOfUser1 = await _repository.GetFriendsByUserAccountIdAsync(u1);
            var friendsOfUser2 = await _repository.GetFriendsByUserAccountIdAsync(u2);

            Assert.Single(friendsOfUser1);
            Assert.Single(friendsOfUser2);
            Assert.Equal("user2@test.com", friendsOfUser1[0].Email);
            Assert.Equal("user1@test.com", friendsOfUser2[0].Email);
        }

        [Fact]
        public async Task DeleteFriendship_Existing_ReturnsTrueAndRemoves()
        {
            var (p1, _) = GetPlayerByEmail("user1@test.com");
            var (p2, _) = GetPlayerByEmail("user2@test.com");
            await _repository.CreateFriendRequestAsync(p1, p2);
            await _repository.RespondToFriendRequestAsync(p1, p2, true);

            var deleted = await _repository.DeleteFriendshipAsync(p1, p2);
            Assert.True(deleted);

            using (var verify = NewContext())
            {
                Assert.False(verify.Friendship.Any(f => (f.RequesterId == p1 && f.AddresseeId == p2) || (f.RequesterId == p2 && f.AddresseeId == p1)));
            }
        }

        [Fact]
        public async Task DeleteFriendship_NonExisting_ReturnsFalse()
        {
            var (p1, _) = GetPlayerByEmail("user1@test.com");
            var (p3, _) = GetPlayerByEmail("user3@test.com");
            var deleted = await _repository.DeleteFriendshipAsync(p1, p3);
            Assert.False(deleted);
        }
    }
}
