using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Services;
using Services.Wrappers;
using Xunit;

namespace Test.ServicesTests
{
    public class FriendsManagerTests
    {
        private readonly Mock<IFriendshipRepository> friendshipRepo;
        private readonly Mock<IAccountRepository> accountRepo;
        private readonly Mock<IOperationContextWrapper> ctx;
        private readonly Mock<IFriendsCallback> cbA; // user A callback
        private readonly Mock<IFriendsCallback> cbB; // user B callback
        private readonly FriendsManager sut;

        public FriendsManagerTests()
        {
            friendshipRepo = new Mock<IFriendshipRepository>();
            accountRepo = new Mock<IAccountRepository>();
            ctx = new Mock<IOperationContextWrapper>();
            cbA = new Mock<IFriendsCallback>();
            cbB = new Mock<IFriendsCallback>();
            // default return A; we will re-setup where needed
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(() => cbA.Object);

            sut = new FriendsManager(friendshipRepo.Object, accountRepo.Object, ctx.Object);
        }

        private static UserAccount MakeAccount(int accountId, int playerId, string nick)
        {
            return new UserAccount
            {
                Id = accountId,
                Nickname = nick,
                Player = new List<Player> { 
                    new Player { Id = playerId, UserAccountId = accountId, 
                        UserAccount = new UserAccount { Id = accountId, Nickname = nick } 
                    } 
                }
            };
        }


        [Fact]
        public async Task GetFriendsAsync_ShouldMapToDTOs()
        {
            var acc2 = MakeAccount(2, 20, "B");
            var acc3 = MakeAccount(3, 30, "C");
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(1))
                .ReturnsAsync(new List<UserAccount> { acc2, acc3 });

            var list = await sut.GetFriendsAsync(1);

            Assert.Equal(2, list.Count);
            Assert.Contains(list, f => f.PlayerId == 20 && f.Nickname == "B");
            Assert.Contains(list, f => f.PlayerId == 30 && f.Nickname == "C");
        }

        [Fact]
        public async Task DeleteFriendAsync_WhenRepositoryReturnsTrue_ShouldNotifyBoth()
        {
            friendshipRepo.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(true);

            // subscribe both users (store by account id)
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(100); // account of user 10
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            await sut.SubscribeToFriendUpdatesAsync(110); // account of user 11

            // map player -> account ids
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(10)).ReturnsAsync(MakeAccount(100, 10, "A"));
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(11)).ReturnsAsync(MakeAccount(110, 11, "B"));

            var res = await sut.DeleteFriendAsync(10, 11);

            Assert.True(res);
            cbB.Verify(c => c.OnFriendRemoved(10), Times.Once); // friend gets notified about A removal
            cbA.Verify(c => c.OnFriendRemoved(11), Times.Once); // A gets notified about removing B
        }

        [Fact]
        public async Task DeleteFriendAsync_WhenRepositoryReturnsFalse_ShouldNotNotify()
        {
            friendshipRepo.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(false);

            var res = await sut.DeleteFriendAsync(10, 11);

            Assert.False(res);
            cbA.Verify(c => c.OnFriendRemoved(It.IsAny<int>()), Times.Never);
            cbB.Verify(c => c.OnFriendRemoved(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_WhenNoSubscriptionContext_ShouldReturnEmpty()
        {
            // no prior Subscribe -> callback not present in connected map
            var list = await sut.GetPendingRequestsAsync();
            Assert.Empty(list);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_ShouldMapRequests()
        {
            // subscribe as account 200 using cbA
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(200);

            var reqAcc = MakeAccount(201, 21, "Requester");
            var friendReq = new Friendship { RequesterId = 21, Player1 = reqAcc.Player.First() };
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(200))
                .ReturnsAsync(new List<Friendship> { friendReq });

            var list = await sut.GetPendingRequestsAsync();

            Assert.Single(list);
            Assert.Equal(21, list[0].PlayerId);
            Assert.Equal("Requester", list[0].Nickname);
        }

        [Fact]
        public async Task SendFriendRequestAsync_WhenNoCallbackUser_ShouldReturnFailed()
        {
            // no subscribe => GetUserAccountIdFromCallback returns 0
            var result = await sut.SendFriendRequestAsync("b@test.com");
            Assert.Equal(FriendRequestResult.Failed, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_FullHappyPath_ShouldNotifyAddressee()
        {
            // subscribe requester (account 300) with cbA
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            var requester = MakeAccount(300, 30, "A");
            var addressee = MakeAccount(310, 31, "B");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepo.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);

            // no friends, no pending
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(300))
                .ReturnsAsync(new List<UserAccount>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(310))
                .ReturnsAsync(new List<Friendship>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(300))
                .ReturnsAsync(new List<Friendship>());

            // create request ok
            friendshipRepo.Setup(r => r.CreateFriendRequestAsync(30, 31)).ReturnsAsync(true);

            // addressee subscribed (account 310) with cbB
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            await sut.SubscribeToFriendUpdatesAsync(310);

            var result = await sut.SendFriendRequestAsync("b@test.com");

            Assert.Equal(FriendRequestResult.Success, result);
            cbB.Verify(c => c.OnFriendRequestReceived(It.Is<FriendDTO>(d => d.PlayerId == 30 && d.Nickname == "A")), Times.Once);
        }

        [Fact]
        public async Task SendFriendRequestAsync_AddresseeNotFound_ShouldReturnUserNotFound()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(MakeAccount(300, 30, "A"));
            accountRepo.Setup(a => a.GetUserByEmailAsync("x@test.com")).ReturnsAsync((UserAccount)null);

            var result = await sut.SendFriendRequestAsync("x@test.com");
            Assert.Equal(FriendRequestResult.UserNotFound, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_RequesterWithoutPlayer_ShouldReturnFailed()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            var requesterNoPlayer = new UserAccount { Id = 300, Nickname = "A", Player = new List<Player>() };
            var addressee = MakeAccount(310, 31, "B");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requesterNoPlayer);
            accountRepo.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);

            var result = await sut.SendFriendRequestAsync("b@test.com");
            Assert.Equal(FriendRequestResult.Failed, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_CannotAddSelf_ShouldReturnCannotAddSelf()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            var requester = MakeAccount(300, 30, "A");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepo.Setup(a => a.GetUserByEmailAsync("a@test.com")).ReturnsAsync(requester);

            var result = await sut.SendFriendRequestAsync("a@test.com");
            Assert.Equal(FriendRequestResult.CannotAddSelf, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_AddresseeHasNoPlayer_ShouldReturnUserNotFound()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            var requester = MakeAccount(300, 30, "A");
            var addresseeNoPlayer = new UserAccount { Id = 310, Nickname = "B", Player = new List<Player>() };
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepo.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addresseeNoPlayer);

            var result = await sut.SendFriendRequestAsync("b@test.com");
            Assert.Equal(FriendRequestResult.UserNotFound, result);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RespondToFriendRequestAsync_ShouldNotifyWhenAccepted(bool accept)
        {
            // subscribe both sides
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300); // addressee account (will respond)
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            await sut.SubscribeToFriendUpdatesAsync(310); // requester account

            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            var requesterAcc = MakeAccount(310, 31, "Requester");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(31)).ReturnsAsync(requesterAcc);

            friendshipRepo.Setup(r => r.RespondToFriendRequestAsync(31, 30, accept)).ReturnsAsync(true);

            // Act (callback channel maps to addressee via SubscribeToFriendUpdatesAsync(300))
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.RespondToFriendRequestAsync(31, accept);

            if (accept)
            {
                // both should receive OnFriendAdded
                cbA.Verify(c => c.OnFriendAdded(It.Is<FriendDTO>(d => d.PlayerId == 31 && d.Nickname == "Requester")), Times.Once);
                cbB.Verify(c => c.OnFriendAdded(It.Is<FriendDTO>(d => d.PlayerId == 30 && d.Nickname == "Addressee")), Times.Once);
            }
            else
            {
                cbA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
                cbB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
            }
        }

        [Fact]
        public async Task ValidateFriendRequestAsync_Paths_ShouldReturnExpectedCodes()
        {
            // We'll call SendFriendRequestAsync and drive the validations underneath
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            var requester = MakeAccount(300, 30, "A");
            var addressee = MakeAccount(310, 31, "B");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepo.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);

            // Already friends
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(300))
                .ReturnsAsync(new List<UserAccount> { addressee });
            var result = await sut.SendFriendRequestAsync("b@test.com");
            Assert.Equal(FriendRequestResult.AlreadyFriends, result);

            // Request already sent
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(300))
                .ReturnsAsync(new List<UserAccount>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(310))
                .ReturnsAsync(new List<Friendship> { new Friendship { RequesterId = 30 } });
            result = await sut.SendFriendRequestAsync("b@test.com");
            Assert.Equal(FriendRequestResult.RequestAlreadySent, result);

            // Request already received
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(310))
                .ReturnsAsync(new List<Friendship>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(300))
                .ReturnsAsync(new List<Friendship> { new Friendship { RequesterId = 31 } });
            result = await sut.SendFriendRequestAsync("b@test.com");
            Assert.Equal(FriendRequestResult.RequestAlreadyReceived, result);
        }

        [Fact]
        public async Task CreateAndNotifyFriendRequestAsync_Failure_ShouldReturnFailed()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            var requester = MakeAccount(300, 30, "A");
            var addressee = MakeAccount(310, 31, "B");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepo.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);

            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(300)).ReturnsAsync(new List<UserAccount>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(310)).ReturnsAsync(new List<Friendship>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(300)).ReturnsAsync(new List<Friendship>());

            // simulate create failing
            friendshipRepo.Setup(r => r.CreateFriendRequestAsync(30, 31)).ReturnsAsync(false);

            var result = await sut.SendFriendRequestAsync("b@test.com");

            Assert.Equal(FriendRequestResult.Failed, result);
            cbB.Verify(c => c.OnFriendRequestReceived(It.IsAny<FriendDTO>()), Times.Never);
        }
    }
}
