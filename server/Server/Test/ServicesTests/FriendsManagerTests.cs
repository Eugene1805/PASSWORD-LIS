using System.Data.Entity.Infrastructure;
using System.ServiceModel;
using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Services;
using Services.Wrappers;

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
                Player = [new Player { Id = playerId, UserAccountId = accountId, UserAccount = 
                new UserAccount { Id = accountId, Nickname = nick } }]
            };
        }

        [Fact]
        public async Task GetFriendsAsync_ShouldMapToDTOs()
        {
            // Arrange
            var acc2 = MakeAccount(2, 20, "B");
            var acc3 = MakeAccount(3, 30, "C");
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(1)).ReturnsAsync(new List<UserAccount> 
            { acc2, acc3 });
            // Act
            var list = await sut.GetFriendsAsync(1);
            // Assert
            var expected = new { Count = 2, HasB = true, HasC = true };
            var actual = new { Count = list.Count, HasB = list.Any(f => f.PlayerId == 20 && f.Nickname == "B"),
                HasC = list.Any(f => f.PlayerId == 30 && f.Nickname == "C") };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetFriendsAsync_ShouldReturnEmptyList_WhenNoFriends()
        {
            // Arrange
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(1)).ReturnsAsync(new List<UserAccount>());
            // Act
            var list = await sut.GetFriendsAsync(1);
            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public async Task GetFriendsAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {
            // Arrange
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(1)).ThrowsAsync(new Exception("Database error"));
            // Act
            FaultException<ServiceErrorDetailDTO>? ex = null;
            try 
            { 
                await sut.GetFriendsAsync(1); 
            } 
            catch (FaultException<ServiceErrorDetailDTO> e) 
            { 
                ex = e; 
            }
            // Assert
            Assert.Equal((Threw: true, Code: (ServiceErrorCode?)ServiceErrorCode.UnexpectedError), (Threw: ex != null,
                ex?.Detail.Code));
        }

        [Fact]
        public async Task GetFriendsAsync_ShouldThrowFault_WhenRepositoryThrowsDbUpdateException()
        {
            // Arrange
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(1)).ThrowsAsync(
                new DbUpdateException("DB error"));
            // Act
            FaultException<ServiceErrorDetailDTO>? ex = null;
            try 
            { 
                await sut.GetFriendsAsync(1); 
            } 
            catch (FaultException<ServiceErrorDetailDTO> e) 
            { 
                ex = e; 
            }
            // Assert
            Assert.Equal((Threw: true, Code: (ServiceErrorCode?)ServiceErrorCode.DatabaseError),
                (Threw: ex != null, ex?.Detail.Code));
        }

        [Fact]
        public async Task DeleteFriendAsync_WhenRepositoryReturnsTrue_ShouldNotifyBoth()
        {
            // Arrange
            friendshipRepo.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(true);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(100);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            sut.SubscribeToFriendUpdatesAsync(110);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(10)).ReturnsAsync(MakeAccount(100, 10, "A"));
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(11)).ReturnsAsync(MakeAccount(110, 11, "B"));
            // Act
            var res = await sut.DeleteFriendAsync(10, 11);
            // Assert
            var actual = new
            {
                Result = res,
                A_Removed11 = cbA.Invocations.
                Any(i => i.Method.Name == nameof(IFriendsCallback.OnFriendRemoved) && (int)i.Arguments[0] == 11),
                B_Removed10 = cbB.Invocations.
                Any(i => i.Method.Name == nameof(IFriendsCallback.OnFriendRemoved) && (int)i.Arguments[0] == 10)
            };
            Assert.Equal(new { Result = true, A_Removed11 = true, B_Removed10 = true }, actual);
        }

        [Fact]
        public async Task DeleteFriendAsync_WhenRepositoryReturnsFalse_ShouldNotNotify()
        {
            // Arrange
            friendshipRepo.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(false);
            // Act
            var res = await sut.DeleteFriendAsync(10, 11);
            // Assert
            var actual = new
            {
                Result = res,
                A_Notified = cbA.Invocations.Any(i => i.Method.Name == nameof(IFriendsCallback.OnFriendRemoved)),
                B_Notified = cbB.Invocations.Any(i => i.Method.Name == nameof(IFriendsCallback.OnFriendRemoved))
            };
            Assert.Equal(new { Result = false, A_Notified = false, B_Notified = false }, actual);
        }

        [Fact]
        public async Task DeleteFriendAsync_ShouldNotifyOnlyConnectedClients()
        {
            // Arrange
            friendshipRepo.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(true);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(100);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(10)).ReturnsAsync(MakeAccount(100, 10, "A"));
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(11)).ReturnsAsync(MakeAccount(110, 11, "B"));
            // Act
            var res = await sut.DeleteFriendAsync(10, 11);
            // Assert
            var actual = new
            {
                Result = res,
                A_Removed11 = cbA.Invocations.
                Any(i => i.Method.Name == nameof(IFriendsCallback.OnFriendRemoved) && (int)i.Arguments[0] == 11),
                B_Notified = cbB.Invocations.Any(i => i.Method.Name == nameof(IFriendsCallback.OnFriendRemoved))
            };
            Assert.Equal(new { Result = true, A_Removed11 = true, B_Notified = false }, actual);
        }

        [Fact]
        public async Task DeleteFriendAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {
            // Arrange
            friendshipRepo.Setup(r => r.DeleteFriendshipAsync(10, 11)).ThrowsAsync(new Exception("Error"));
            // Act
            FaultException<ServiceErrorDetailDTO>? ex = null;
            try 
            { 
                await sut.DeleteFriendAsync(10, 11); 
            } 
            catch (FaultException<ServiceErrorDetailDTO> e) 
            { 
                ex = e; 
            }
            // Assert
            Assert.Equal((Threw: true, Code: (ServiceErrorCode?)ServiceErrorCode.UnexpectedError),
                (Threw: ex != null, ex?.Detail.Code));
        }

        [Fact]
        public async Task GetPendingRequestsAsync_WhenNoSubscriptionContext_ShouldReturnEmpty()
        {
            // Arrange
            // Act
            var list = await sut.GetPendingRequestsAsync();
            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_ShouldMapRequests()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(200);
            var reqAcc = MakeAccount(201, 21, "Requester");
            var friendReq = new Friendship { RequesterId = 21, Player1 = reqAcc.Player.First() };
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(200)).ReturnsAsync(new List<Friendship> { friendReq });
            // Act
            var list = await sut.GetPendingRequestsAsync();
            // Assert
            var expected = (Count: 1, PlayerId: (int?)21, Nickname: (string?)"Requester");
            var actual = (list.Count, list.FirstOrDefault()?.PlayerId, list.FirstOrDefault()?.Nickname);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_ShouldReturnEmptyList_WhenNoPendingRequests()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(200);
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(200)).ReturnsAsync(new List<Friendship>());
            // Act
            var list = await sut.GetPendingRequestsAsync();
            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(200);
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(200)).ThrowsAsync(new Exception("Error"));
            // Act
            FaultException<ServiceErrorDetailDTO>? ex = null;
            try 
            { 
                await sut.GetPendingRequestsAsync(); 
            } 
            catch (FaultException<ServiceErrorDetailDTO> e) 
            { 
                ex = e; 
            }
            // Assert
            Assert.Equal((Threw: true, Code: (ServiceErrorCode?)ServiceErrorCode.UnexpectedError), 
                (Threw: ex != null, ex?.Detail.Code));
        }

        [Fact]
        public async Task SendFriendRequestAsync_WhenNoCallbackUser_ShouldReturnFailed()
        {
            // Arrange
            // Act
            var result = await sut.SendFriendRequestAsync("b@test.com");
            // Assert
            Assert.Equal(FriendRequestResult.Failed, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_FullHappyPath_ShouldNotifyAddressee()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
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
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            sut.SubscribeToFriendUpdatesAsync(310);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            // Act
            var result = await sut.SendFriendRequestAsync("b@test.com");
            // Assert
            Assert.Equal(FriendRequestResult.Success, result);
            cbB.Verify(c => c.OnFriendRequestReceived(It.Is<FriendDTO>(d => d.PlayerId == 30 && d.Nickname == "A")), 
                Times.Once);
        }

        [Fact]
        public async Task SendFriendRequestAsync_AddresseeNotFound_ShouldReturnUserNotFound()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(MakeAccount(300, 30, "A"));
            accountRepo.Setup(a => a.GetUserByEmailAsync("x@test.com")).ReturnsAsync((UserAccount?)null);
            // Act
            var result = await sut.SendFriendRequestAsync("x@test.com");
            // Assert
            Assert.Equal(FriendRequestResult.UserNotFound, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_RequesterWithoutPlayer_ShouldReturnFailed()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var requesterNoPlayer = new UserAccount { Id = 300, Nickname = "A", Player = new List<Player>() };
            var addressee = MakeAccount(310, 31, "B");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requesterNoPlayer);
            accountRepo.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);
            // Act
            var result = await sut.SendFriendRequestAsync("b@test.com");
            // Assert
            Assert.Equal(FriendRequestResult.Failed, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_CannotAddSelf_ShouldReturnCannotAddSelf()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var requester = MakeAccount(300, 30, "A");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepo.Setup(a => a.GetUserByEmailAsync("a@test.com")).ReturnsAsync(requester);
            // Act
            var result = await sut.SendFriendRequestAsync("a@test.com");
            // Assert
            Assert.Equal(FriendRequestResult.CannotAddSelf, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_AddresseeHasNoPlayer_ShouldReturnUserNotFound()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var requester = MakeAccount(300, 30, "A");
            var addresseeNoPlayer = new UserAccount { Id = 310, Nickname = "B", Player = new List<Player>() };
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepo.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addresseeNoPlayer);
            // Act
            var result = await sut.SendFriendRequestAsync("b@test.com");
            // Assert
            Assert.Equal(FriendRequestResult.UserNotFound, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300))
                .ThrowsAsync(new Exception("Error"));
            // Act
            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => sut.SendFriendRequestAsync("b@test.com")
            );
            // Assert
            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RespondToFriendRequestAsync_ShouldNotifyWhenAccepted(bool accept)
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            sut.SubscribeToFriendUpdatesAsync(310);
            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            var requesterAcc = MakeAccount(310, 31, "Requester");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(310)).ReturnsAsync(requesterAcc);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(31)).ReturnsAsync(requesterAcc);
            friendshipRepo.Setup(r => r.RespondToFriendRequestAsync(31, 30, accept)).ReturnsAsync(true);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            // Act
            await sut.RespondToFriendRequestAsync(31, accept);
            // Assert
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
            // Arrange
            // Act
            await sut.RespondToFriendRequestAsync(31, true);
            // Assert
            cbA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
            cbB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_WhenAddresseeNotFound_ShouldNotNotify()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync((UserAccount?)null);
            // Act
            await sut.RespondToFriendRequestAsync(31, true);
            // Assert
            cbA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_WhenAddresseeHasNoPlayer_ShouldNotNotify()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var addresseeNoPlayer = new UserAccount { Id = 300, Nickname = "A", Player = new List<Player>() };
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeNoPlayer);
            // Act
            await sut.RespondToFriendRequestAsync(31, true);
            // Assert
            cbA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_WhenRepositoryReturnsFalse_ShouldNotNotify()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            friendshipRepo.Setup(r => r.RespondToFriendRequestAsync(31, 30, true)).ReturnsAsync(false);
            // Act
            await sut.RespondToFriendRequestAsync(31, true);
            // Assert
            cbA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300))
                .ThrowsAsync(new Exception("Error"));
            // Act
            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => sut.RespondToFriendRequestAsync(31, true)
            );
            // Assert
            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
        }

        [Fact]
        public async Task ValidateFriendRequestAsync_Paths_ShouldReturnExpectedCodes()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var requester = MakeAccount(300, 30, "A");
            var addressee = MakeAccount(310, 31, "B");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepo.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(300))
                .ReturnsAsync(new List<UserAccount> { addressee });
            // Act
            var result = await sut.SendFriendRequestAsync("b@test.com");
            // Assert
            Assert.Equal(FriendRequestResult.AlreadyFriends, result);
            // Arrange
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(300))
                .ReturnsAsync(new List<UserAccount>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(310))
                .ReturnsAsync(new List<Friendship> { new Friendship { RequesterId = 30 } });
            // Act
            result = await sut.SendFriendRequestAsync("b@test.com");
            // Assert
            Assert.Equal(FriendRequestResult.RequestAlreadySent, result);
            // Arrange
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(310))
                .ReturnsAsync(new List<Friendship>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(300))
                .ReturnsAsync(new List<Friendship> { new Friendship { RequesterId = 31 } });
            // Act
            result = await sut.SendFriendRequestAsync("b@test.com");
            // Assert
            Assert.Equal(FriendRequestResult.RequestAlreadyReceived, result);
        }

        [Fact]
        public async Task CreateAndNotifyFriendRequestAsync_Failure_ShouldReturnFailed()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var requester = MakeAccount(300, 30, "A");
            var addressee = MakeAccount(310, 31, "B");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepo.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);
            friendshipRepo.Setup(r => r.GetFriendsByUserAccountIdAsync(300)).ReturnsAsync(new List<UserAccount>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(310)).ReturnsAsync(new List<Friendship>());
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(300)).ReturnsAsync(new List<Friendship>());
            friendshipRepo.Setup(r => r.CreateFriendRequestAsync(30, 31)).ReturnsAsync(false);
            // Act
            var result = await sut.SendFriendRequestAsync("b@test.com");
            // Assert
            Assert.Equal(FriendRequestResult.Failed, result);
            cbB.Verify(c => c.OnFriendRequestReceived(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task SubscribeToFriendUpdatesAsync_ShouldAddClientToConnected()
        {
            // Arrange
            var callback = new Mock<IFriendsCallback>();
            callback.As<ICommunicationObject>();
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callback.Object);
            sut.SubscribeToFriendUpdatesAsync(500);
            friendshipRepo.Setup(r => r.GetPendingRequestsAsync(500)).ReturnsAsync(new List<Friendship>());
            // Act
            var list = await sut.GetPendingRequestsAsync();
            // Assert
            Assert.NotNull(list);
        }

        [Fact]
        public async Task UnsubscribeFromFriendUpdatesAsync_ShouldRemoveClient()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(500);
            // Act
            await sut.UnsubscribeFromFriendUpdatesAsync(500);
            var list = await sut.GetPendingRequestsAsync();
            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public async Task NotifyOnRequestAcceptedAsync_WhenRequesterNotFound_ShouldNotNotify()
        {
            // Arrage
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            sut.SubscribeToFriendUpdatesAsync(310);
            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(31)).ReturnsAsync((UserAccount?)null);
            friendshipRepo.Setup(r => r.RespondToFriendRequestAsync(31, 30, true)).ReturnsAsync(true);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            // Act
            await sut.RespondToFriendRequestAsync(31, true);
            // Assert
            cbA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
            cbB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task NotifyOnRequestAcceptedAsync_WhenRequesterHasNoPlayer_ShouldNotNotify()
        {
            // Arrage
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            sut.SubscribeToFriendUpdatesAsync(310);
            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            var requesterNoPlayer = new UserAccount { Id = 310, Nickname = "B", Player = new List<Player>() };
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(31)).ReturnsAsync(requesterNoPlayer);
            friendshipRepo.Setup(r => r.RespondToFriendRequestAsync(31, 30, true)).ReturnsAsync(true);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            // Act
            await sut.RespondToFriendRequestAsync(31, true);
            // Assert
            cbA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
            cbB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task NotifyFriendRemovedAsync_WhenCurrentUserNotFound_ShouldNotifyOnlyFriend()
        {
            // Arrage
            friendshipRepo.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(true);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbB.Object);
            sut.SubscribeToFriendUpdatesAsync(110);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(10)).ReturnsAsync((UserAccount?)null);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(11)).ReturnsAsync(MakeAccount(110, 11, "B"));
            // Act
            var res = await sut.DeleteFriendAsync(10, 11);
            // Assert
            Assert.True(res);
            cbB.Verify(c => c.OnFriendRemoved(10), Times.Once);
        }

        [Fact]
        public async Task NotifyFriendRemovedAsync_WhenFriendNotFound_ShouldNotifyOnlyCurrentUser()
        {
            // Arrange
            friendshipRepo.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(true);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(100);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(10)).ReturnsAsync(MakeAccount(100, 10, "A"));
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(11)).ReturnsAsync((UserAccount?)null);
            // Act
            var res = await sut.DeleteFriendAsync(10, 11);
            // Assert
            Assert.True(res);
            cbA.Verify(c => c.OnFriendRemoved(11), Times.Once);
        }

        [Fact]
        public async Task SendFriendRequestAsync_ShouldNotNotifyAddressee_WhenNotSubscribed()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
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
            // Act
            var result = await sut.SendFriendRequestAsync("b@test.com");
            // Assert
            Assert.Equal(FriendRequestResult.Success, result);
            cbB.Verify(c => c.OnFriendRequestReceived(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_WhenAccepted_ShouldNotifyOnlyConnectedClients()
        {
            // Arrange
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            var requesterAcc = MakeAccount(310, 31, "Requester");
            accountRepo.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            accountRepo.Setup(a => a.GetUserByPlayerIdAsync(31)).ReturnsAsync(requesterAcc);
            friendshipRepo.Setup(r => r.RespondToFriendRequestAsync(31, 30, true)).ReturnsAsync(true);
            ctx.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(cbA.Object);
            // Act
            await sut.RespondToFriendRequestAsync(31, true);
            // Assert
            cbA.Verify(c => c.OnFriendAdded(It.Is<FriendDTO>(d => d.PlayerId == 31 && d.Nickname == "Requester")),
                Times.Once);
            cbB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }
    }
}
