using System.Linq;
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
        public void CreateFriendRequest_NewRequest_ReturnsTrue()
        {
            var (p1, _) = GetPlayerByEmail("user1@test.com");
            var (p2, _) = GetPlayerByEmail("user2@test.com");

            var result = _repository.CreateFriendRequest(p1, p2);
            Assert.True(result);

            using (var verify = NewContext())
            {
                Assert.Single(verify.Friendship.Where(f => f.RequesterId == p1 && f.AddresseeId == p2));
            }
        }

        [Fact]
        public void CreateFriendRequest_Duplicate_ReturnsFalse()
        {
            var (p1, _) = GetPlayerByEmail("user1@test.com");
            var (p2, _) = GetPlayerByEmail("user2@test.com");
            Assert.True(_repository.CreateFriendRequest(p1, p2));
            var duplicate = _repository.CreateFriendRequest(p1, p2);
            Assert.False(duplicate);
        }

        [Fact]
        public void GetPendingRequests_ForAddressee_ReturnsList()
        {
            var (requesterId, _) = GetPlayerByEmail("user1@test.com");
            var (addresseePlayerId, addresseeUserAccountId) = GetPlayerByEmail("user2@test.com");
            _repository.CreateFriendRequest(requesterId, addresseePlayerId);

            var pending = _repository.GetPendingRequests(addresseeUserAccountId);
            Assert.Single(pending);
            Assert.Equal(requesterId, pending[0].RequesterId);
            Assert.Equal(addresseePlayerId, pending[0].AddresseeId);
            Assert.Equal(0, pending[0].Status);
        }

        [Fact]
        public void RespondToFriendRequest_Accept_SetsStatusToAccepted()
        {
            var (p1, _) = GetPlayerByEmail("user1@test.com");
            var (p2, _) = GetPlayerByEmail("user2@test.com");
            _repository.CreateFriendRequest(p1, p2);
            var responded = _repository.RespondToFriendRequest(p1, p2, true);
            Assert.True(responded);

            using (var verify = NewContext())
            {
                var friendship = verify.Friendship.First(f => f.RequesterId == p1 && f.AddresseeId == p2);
                Assert.Equal(1, friendship.Status);
                Assert.NotNull(friendship.RespondedAt);
            }
        }

        [Fact]
        public void RespondToFriendRequest_Reject_RemovesRequest()
        {
            var (p1, _) = GetPlayerByEmail("user1@test.com");
            var (p2, _) = GetPlayerByEmail("user2@test.com");
            _repository.CreateFriendRequest(p1, p2);
            var responded = _repository.RespondToFriendRequest(p1, p2, false);
            Assert.True(responded);

            using (var verify = NewContext())
            {
                Assert.False(verify.Friendship.Any(f => f.RequesterId == p1 && f.AddresseeId == p2));
            }
        }

        [Fact]
        public void GetFriendsByUserAccountId_AfterAcceptance_ReturnsBothSides()
        {
            var (p1, u1) = GetPlayerByEmail("user1@test.com");
            var (p2, u2) = GetPlayerByEmail("user2@test.com");
            _repository.CreateFriendRequest(p1, p2);
            _repository.RespondToFriendRequest(p1, p2, true);

            var friendsOfUser1 = _repository.GetFriendsByUserAccountId(u1);
            var friendsOfUser2 = _repository.GetFriendsByUserAccountId(u2);

            Assert.Single(friendsOfUser1);
            Assert.Single(friendsOfUser2);
            Assert.Equal("user2@test.com", friendsOfUser1[0].Email);
            Assert.Equal("user1@test.com", friendsOfUser2[0].Email);
        }

        [Fact]
        public void DeleteFriendship_Existing_ReturnsTrueAndRemoves()
        {
            var (p1, _) = GetPlayerByEmail("user1@test.com");
            var (p2, _) = GetPlayerByEmail("user2@test.com");
            _repository.CreateFriendRequest(p1, p2);
            _repository.RespondToFriendRequest(p1, p2, true);

            var deleted = _repository.DeleteFriendship(p1, p2);
            Assert.True(deleted);

            using (var verify = NewContext())
            {
                Assert.False(verify.Friendship.Any(f => (f.RequesterId == p1 && f.AddresseeId == p2) || (f.RequesterId == p2 && f.AddresseeId == p1)));
            }
        }

        [Fact]
        public void DeleteFriendship_NonExisting_ReturnsFalse()
        {
            var (p1, _) = GetPlayerByEmail("user1@test.com");
            var (p3, _) = GetPlayerByEmail("user3@test.com");
            var deleted = _repository.DeleteFriendship(p1, p3);
            Assert.False(deleted);
        }
    }
}
