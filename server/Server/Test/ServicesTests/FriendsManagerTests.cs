using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
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
        private readonly Mock<IFriendsCallback> cbA;
        private readonly Mock<IFriendsCallback> cbB;
        private readonly FriendsManager sut;

        public FriendsManagerTests()
        {
            friendshipRepo = new Mock<IFriendshipRepository>();
            accountRepo = new Mock<IAccountRepository>();
            ctx = new Mock<IOperationContextWrapper>();
            cbA = new Mock<IFriendsCallback>();
            cbB = new Mock<IFriendsCallback>();
            
            // Setup ICommunicationObject for cbA and cbB
            cbA.As<ICommunicationObject>();
            cbB.As<ICommunicationObject>();
            
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
        public async Task GetFriendsAsync_ShouldReturnEmptyList_WhenNoFriends()
        {
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(1))
                .ReturnsAsync(new List<UserAccount>());

            var list = await sut.GetFriendsAsync(1);

            Assert.Empty(list);
        }

        [Fact]
        public async Task GetFriendsAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(1))
                .ThrowsAsync(new Exception("Database error"));

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => sut.GetFriendsAsync(1)
            );

            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
        }

        [Fact]
        public async Task GetFriendsAsync_ShouldThrowFault_WhenRepositoryThrowsDbUpdateException()
        {
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(1))
                .ThrowsAsync(new DbUpdateException("DB error"));

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => sut.GetFriendsAsync(1)
            );

            Assert.Equal(ServiceErrorCode.DatabaseError, ex.Detail.Code);
        }

        [Fact]
        public async Task DeleteFriendAsync_WhenRepositoryReturnsTrue_ShouldNotifyBoth()
        {
            friendshipRepo.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(true);

            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(100);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            await sut.SubscribeToFriendUpdatesAsync(110);

            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(10)).ReturnsAsync(MakeAccount(100, 10, "A"));
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(11)).ReturnsAsync(MakeAccount(110, 11, "B"));

            var res = await sut.DeleteFriendAsync(10, 11);

            Assert.True(res);
            cbB.Verify(c => c.OnFriendRemoved(10), Times.Once);
            cbA.Verify(c => c.OnFriendRemoved(11), Times.Once);
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
        public async Task DeleteFriendAsync_ShouldNotifyOnlyConnectedClients()
        {
            friendshipRepo.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(true);

            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(100);

            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(10)).ReturnsAsync(MakeAccount(100, 10, "A"));
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(11)).ReturnsAsync(MakeAccount(110, 11, "B"));

            var res = await sut.DeleteFriendAsync(10, 11);

            Assert.True(res);
            cbA.Verify(c => c.OnFriendRemoved(11), Times.Once);
            cbB.Verify(c => c.OnFriendRemoved(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteFriendAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {
            friendshipRepo.Setup(r => r.DeleteFriendshipAsync(10, 11))
                .ThrowsAsync(new Exception("Error"));

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => sut.DeleteFriendAsync(10, 11)
            );

            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_WhenNoSubscriptionContext_ShouldReturnEmpty()
        {
            var list = await sut.GetPendingRequestsAsync();
            Assert.Empty(list);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_ShouldMapRequests()
        {
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
        public async Task GetPendingRequestsAsync_ShouldReturnEmptyList_WhenNoPendingRequests()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(200);

            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(200))
                .ReturnsAsync(new List<Friendship>());

            var list = await sut.GetPendingRequestsAsync();

            Assert.Empty(list);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(200);

            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(200))
                .ThrowsAsync(new Exception("Error"));

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => sut.GetPendingRequestsAsync()
            );

            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
        }

        [Fact]
        public async Task SendFriendRequestAsync_WhenNoCallbackUser_ShouldReturnFailed()
        {
            var result = await sut.SendFriendRequestAsync("b@test.com");
            Assert.Equal(FriendRequestResult.Failed, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_FullHappyPath_ShouldNotifyAddressee()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            var requester = MakeAccount(300, 30, "A");
            var addressee = MakeAccount(310, 31, "B");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepo.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);

            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(300))
                .ReturnsAsync(new List<UserAccount>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(310))
                .ReturnsAsync(new List<Friendship>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(300))
                .ReturnsAsync(new List<Friendship>());

            friendshipRepo.Setup(r => r.CreateFriendRequestAsync(30, 31)).ReturnsAsync(true);

            // Subscribe addressee before sending request
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            await sut.SubscribeToFriendUpdatesAsync(310);

            // Reset context back to requester for sending request
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
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

        [Fact]
        public async Task SendFriendRequestAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300))
                .ThrowsAsync(new Exception("Error"));

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => sut.SendFriendRequestAsync("b@test.com")
            );

            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RespondToFriendRequestAsync_ShouldNotifyWhenAccepted(bool accept)
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            await sut.SubscribeToFriendUpdatesAsync(310);

            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            var requesterAcc = MakeAccount(310, 31, "Requester");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(310)).ReturnsAsync(requesterAcc);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(31)).ReturnsAsync(requesterAcc);

            friendshipRepo.Setup(r => r.RespondToFriendRequestAsync(31, 30, accept)).ReturnsAsync(true);

            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.RespondToFriendRequestAsync(31, accept);

            if (accept)
            {
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
        public async Task RespondToFriendRequestAsync_WhenNoCallback_ShouldNotNotify()
        {
            await sut.RespondToFriendRequestAsync(31, true);
            
            cbA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
            cbB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_WhenAddresseeNotFound_ShouldNotNotify()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync((UserAccount)null);

            await sut.RespondToFriendRequestAsync(31, true);

            cbA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_WhenAddresseeHasNoPlayer_ShouldNotNotify()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            var addresseeNoPlayer = new UserAccount { Id = 300, Nickname = "A", Player = new List<Player>() };
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeNoPlayer);

            await sut.RespondToFriendRequestAsync(31, true);

            cbA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_WhenRepositoryReturnsFalse_ShouldNotNotify()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);

            friendshipRepo.Setup(r => r.RespondToFriendRequestAsync(31, 30, true)).ReturnsAsync(false);

            await sut.RespondToFriendRequestAsync(31, true);

            cbA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300))
                .ThrowsAsync(new Exception("Error"));

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => sut.RespondToFriendRequestAsync(31, true)
            );

            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
        }

        [Fact]
        public async Task ValidateFriendRequestAsync_Paths_ShouldReturnExpectedCodes()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            var requester = MakeAccount(300, 30, "A");
            var addressee = MakeAccount(310, 31, "B");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepo.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);

            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(300))
                .ReturnsAsync(new List<UserAccount> { addressee });
            var result = await sut.SendFriendRequestAsync("b@test.com");
            Assert.Equal(FriendRequestResult.AlreadyFriends, result);

            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(300))
                .ReturnsAsync(new List<UserAccount>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(310))
                .ReturnsAsync(new List<Friendship> { new Friendship { RequesterId = 30 } });
            result = await sut.SendFriendRequestAsync("b@test.com");
            Assert.Equal(FriendRequestResult.RequestAlreadySent, result);

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

            friendshipRepo.Setup(r => r.CreateFriendRequestAsync(30, 31)).ReturnsAsync(false);

            var result = await sut.SendFriendRequestAsync("b@test.com");

            Assert.Equal(FriendRequestResult.Failed, result);
            cbB.Verify(c => c.OnFriendRequestReceived(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task SubscribeToFriendUpdatesAsync_ShouldAddClientToConnected()
        {
            var callback = new Mock<IFriendsCallback>();
            callback.As<ICommunicationObject>();
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callback.Object);

            await sut.SubscribeToFriendUpdatesAsync(500);

            // Verify client is subscribed by checking pending requests don't throw null exception
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(500))
                .ReturnsAsync(new List<Friendship>());

            var list = await sut.GetPendingRequestsAsync();
            Assert.NotNull(list);
        }

        [Fact]
        public async Task UnsubscribeFromFriendUpdatesAsync_ShouldRemoveClient()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(500);

            await sut.UnsubscribeFromFriendUpdatesAsync(500);

            var list = await sut.GetPendingRequestsAsync();
            Assert.Empty(list);
        }

        [Fact]
        public async Task NotifyOnRequestAcceptedAsync_WhenRequesterNotFound_ShouldNotNotify()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            await sut.SubscribeToFriendUpdatesAsync(310);

            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(31)).ReturnsAsync((UserAccount)null);

            friendshipRepo.Setup(r => r.RespondToFriendRequestAsync(31, 30, true)).ReturnsAsync(true);

            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.RespondToFriendRequestAsync(31, true);

            cbA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
            cbB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task NotifyOnRequestAcceptedAsync_WhenRequesterHasNoPlayer_ShouldNotNotify()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            await sut.SubscribeToFriendUpdatesAsync(310);

            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            var requesterNoPlayer = new UserAccount { Id = 310, Nickname = "B", Player = new List<Player>() };
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(31)).ReturnsAsync(requesterNoPlayer);

            friendshipRepo.Setup(r => r.RespondToFriendRequestAsync(31, 30, true)).ReturnsAsync(true);

            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.RespondToFriendRequestAsync(31, true);

            cbA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
            cbB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task NotifyFriendRemovedAsync_WhenCurrentUserNotFound_ShouldNotifyOnlyFriend()
        {
            friendshipRepo.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(true);

            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            await sut.SubscribeToFriendUpdatesAsync(110);

            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(10)).ReturnsAsync((UserAccount)null);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(11)).ReturnsAsync(MakeAccount(110, 11, "B"));

            var res = await sut.DeleteFriendAsync(10, 11);

            Assert.True(res);
            cbB.Verify(c => c.OnFriendRemoved(10), Times.Once);
        }

        [Fact]
        public async Task NotifyFriendRemovedAsync_WhenFriendNotFound_ShouldNotifyOnlyCurrentUser()
        {
            friendshipRepo.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(true);

            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(100);

            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(10)).ReturnsAsync(MakeAccount(100, 10, "A"));
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(11)).ReturnsAsync((UserAccount)null);

            var res = await sut.DeleteFriendAsync(10, 11);

            Assert.True(res);
            cbA.Verify(c => c.OnFriendRemoved(11), Times.Once);
        }

        [Fact]
        public async Task SendFriendRequestAsync_ShouldNotNotifyAddressee_WhenNotSubscribed()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            var requester = MakeAccount(300, 30, "A");
            var addressee = MakeAccount(310, 31, "B");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepo.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);

            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(300))
                .ReturnsAsync(new List<UserAccount>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(310))
                .ReturnsAsync(new List<Friendship>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(300))
                .ReturnsAsync(new List<Friendship>());

            friendshipRepo.Setup(r => r.CreateFriendRequestAsync(30, 31)).ReturnsAsync(true);

            var result = await sut.SendFriendRequestAsync("b@test.com");

            Assert.Equal(FriendRequestResult.Success, result);
            cbB.Verify(c => c.OnFriendRequestReceived(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_WhenAccepted_ShouldNotifyOnlyConnectedClients()
        {
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.SubscribeToFriendUpdatesAsync(300);

            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            var requesterAcc = MakeAccount(310, 31, "Requester");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(31)).ReturnsAsync(requesterAcc);

            friendshipRepo.Setup(r => r.RespondToFriendRequestAsync(31, 30, true)).ReturnsAsync(true);

            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            await sut.RespondToFriendRequestAsync(31, true);

            cbA.Verify(c => c.OnFriendAdded(It.Is<FriendDTO>(d => d.PlayerId == 31 && d.Nickname == "Requester")), Times.Once);
            cbB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }
    }
}
