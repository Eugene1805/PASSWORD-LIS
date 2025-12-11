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
        private readonly Mock<IFriendshipRepository> friendshipRepository;
        private readonly Mock<IAccountRepository> accountRepository;
        private readonly Mock<IOperationContextWrapper> contextWrapper;
        private readonly Mock<IFriendsCallback> callbackA;
        private readonly Mock<IFriendsCallback> callbackB;
        private readonly FriendsManager sut;

        public FriendsManagerTests()
        {
            friendshipRepository = new Mock<IFriendshipRepository>();
            accountRepository = new Mock<IAccountRepository>();
            contextWrapper = new Mock<IOperationContextWrapper>();
            callbackA = new Mock<IFriendsCallback>();
            callbackB = new Mock<IFriendsCallback>();
            callbackA.As<ICommunicationObject>();
            callbackB.As<ICommunicationObject>();
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(() => callbackA.Object);
            sut = new FriendsManager(friendshipRepository.Object, accountRepository.Object, contextWrapper.Object);
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

            var account2 = MakeAccount(2, 20, "B");
            var account3 = MakeAccount(3, 30, "C");
            friendshipRepository.Setup(r => r.GetFriendsByUserAccountIdAsync(1)).ReturnsAsync(new List<UserAccount> 
            { account2, account3 });

            var list = await sut.GetFriendsAsync(1);

            var expected = new { Count = 2, HasB = true, HasC = true };
            var actual = new { Count = list.Count, HasB = list.Any(f => f.PlayerId == 20 && f.Nickname == "B"),
                HasC = list.Any(f => f.PlayerId == 30 && f.Nickname == "C") };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetFriendsAsync_ShouldReturnEmptyList_WhenNoFriends()
        {

            friendshipRepository.Setup(r => r.GetFriendsByUserAccountIdAsync(1)).ReturnsAsync(new List<UserAccount>());

            var list = await sut.GetFriendsAsync(1);

            Assert.Empty(list);
        }

        [Fact]
        public async Task GetFriendsAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {

            friendshipRepository.Setup(r => r.GetFriendsByUserAccountIdAsync(1)).ThrowsAsync(new Exception("Database error"));

            FaultException<ServiceErrorDetailDTO>? ex = null;
            try 
            { 
                await sut.GetFriendsAsync(1); 
            } 
            catch (FaultException<ServiceErrorDetailDTO> e) 
            { 
                ex = e; 
            }

            Assert.Equal((Threw: true, Code: (ServiceErrorCode?)ServiceErrorCode.UnexpectedError), (Threw: ex != null,
                ex?.Detail.Code));
        }

        [Fact]
        public async Task GetFriendsAsync_ShouldThrowFault_WhenRepositoryThrowsDbUpdateException()
        {

            friendshipRepository.Setup(r => r.GetFriendsByUserAccountIdAsync(1)).ThrowsAsync(
                new DbUpdateException("DB error"));

            FaultException<ServiceErrorDetailDTO>? ex = null;
            try 
            { 
                await sut.GetFriendsAsync(1); 
            } 
            catch (FaultException<ServiceErrorDetailDTO> e) 
            { 
                ex = e; 
            }

            Assert.Equal((Threw: true, Code: (ServiceErrorCode?)ServiceErrorCode.DatabaseError),
                (Threw: ex != null, ex?.Detail.Code));
        }

        [Fact]
        public async Task DeleteFriendAsync_WhenRepositoryReturnsTrue_ShouldNotifyBoth()
        {
            friendshipRepository.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(true);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(100);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackB.Object);
            sut.SubscribeToFriendUpdatesAsync(110);
            accountRepository.Setup(a => a.GetUserByPlayerIdAsync(10)).ReturnsAsync(MakeAccount(100, 10, "A"));
            accountRepository.Setup(a => a.GetUserByPlayerIdAsync(11)).ReturnsAsync(MakeAccount(110, 11, "B"));

            var res = await sut.DeleteFriendAsync(10, 11);

            var actual = new
            {
                Result = res,
                ARemoved11 = callbackA.Invocations.
                Any(i => i.Method.Name == nameof(IFriendsCallback.OnFriendRemoved) && (int)i.Arguments[0] == 11),
                BRemoved10 = callbackB.Invocations.
                Any(i => i.Method.Name == nameof(IFriendsCallback.OnFriendRemoved) && (int)i.Arguments[0] == 10)
            };
            Assert.Equal(new { Result = true, ARemoved11 = true, BRemoved10 = true }, actual);
        }

        [Fact]
        public async Task DeleteFriendAsync_WhenRepositoryReturnsFalse_ShouldNotNotify()
        {

            friendshipRepository.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(false);

            var res = await sut.DeleteFriendAsync(10, 11);

            var actual = new
            {
                Result = res,
                ANotified = callbackA.Invocations.Any(i => i.Method.Name == nameof(IFriendsCallback.OnFriendRemoved)),
                BNotified = callbackB.Invocations.Any(i => i.Method.Name == nameof(IFriendsCallback.OnFriendRemoved))
            };
            Assert.Equal(new { Result = false, ANotified = false, BNotified = false }, actual);
        }

        [Fact]
        public async Task DeleteFriendAsync_ShouldNotifyOnlyConnectedClients()
        {

            friendshipRepository.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(true);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(100);
            accountRepository.Setup(a => a.GetUserByPlayerIdAsync(10)).ReturnsAsync(MakeAccount(100, 10, "A"));
            accountRepository.Setup(a => a.GetUserByPlayerIdAsync(11)).ReturnsAsync(MakeAccount(110, 11, "B"));

            var res = await sut.DeleteFriendAsync(10, 11);

            var actual = new
            {
                Result = res,
                ARemoved11 = callbackA.Invocations.
                Any(i => i.Method.Name == nameof(IFriendsCallback.OnFriendRemoved) && (int)i.Arguments[0] == 11),
                BNotified = callbackB.Invocations.Any(i => i.Method.Name == nameof(IFriendsCallback.OnFriendRemoved))
            };
            Assert.Equal(new { Result = true, ARemoved11 = true, BNotified = false }, actual);
        }

        [Fact]
        public async Task DeleteFriendAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {

            friendshipRepository.Setup(r => r.DeleteFriendshipAsync(10, 11)).ThrowsAsync(new Exception("Error"));

            FaultException<ServiceErrorDetailDTO>? ex = null;
            try 
            { 
                await sut.DeleteFriendAsync(10, 11); 
            } 
            catch (FaultException<ServiceErrorDetailDTO> e) 
            { 
                ex = e; 
            }

            Assert.Equal((Threw: true, Code: (ServiceErrorCode?)ServiceErrorCode.UnexpectedError),
                (Threw: ex != null, ex?.Detail.Code));
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
            
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(200);
            var reqAcc = MakeAccount(201, 21, "Requester");
            var friendReq = new Friendship { RequesterId = 21, Player1 = reqAcc.Player.First() };
            friendshipRepository.Setup(r => r.GetPendingRequestsAsync(200))
                .ReturnsAsync(new List<Friendship> { friendReq });
            
            var list = await sut.GetPendingRequestsAsync();
            
            var expected = (Count: 1, PlayerId: (int?)21, Nickname: (string?)"Requester");
            var actual = (list.Count, list.FirstOrDefault()?.PlayerId, list.FirstOrDefault()?.Nickname);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_ShouldReturnEmptyList_WhenNoPendingRequests()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(200);
            friendshipRepository.Setup(r => r.GetPendingRequestsAsync(200)).ReturnsAsync(new List<Friendship>());
            
            var list = await sut.GetPendingRequestsAsync();
            
            Assert.Empty(list);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(200);
            friendshipRepository.Setup(r => r.GetPendingRequestsAsync(200)).ThrowsAsync(new Exception("Error"));

            FaultException<ServiceErrorDetailDTO>? ex = null;
            try 
            { 
                await sut.GetPendingRequestsAsync(); 
            } 
            catch (FaultException<ServiceErrorDetailDTO> e) 
            { 
                ex = e; 
            }

            Assert.Equal((Threw: true, Code: (ServiceErrorCode?)ServiceErrorCode.UnexpectedError), 
                (Threw: ex != null, ex?.Detail.Code));
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

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var requester = MakeAccount(300, 30, "A");
            var addressee = MakeAccount(310, 31, "B");
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepository.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);
            friendshipRepository.Setup(r => r.GetFriendsByUserAccountIdAsync(300))
                .ReturnsAsync(new List<UserAccount>());
            friendshipRepository.Setup(r => r.GetPendingRequestsAsync(310))
                .ReturnsAsync(new List<Friendship>());
            friendshipRepository.Setup(r => r.GetPendingRequestsAsync(300))
                .ReturnsAsync(new List<Friendship>());
            friendshipRepository.Setup(r => r.CreateFriendRequestAsync(30, 31)).ReturnsAsync(true);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackB.Object);
            sut.SubscribeToFriendUpdatesAsync(310);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);

            var result = await sut.SendFriendRequestAsync("b@test.com");

            Assert.Equal(FriendRequestResult.Success, result);
            callbackB.Verify(c => c.OnFriendRequestReceived(It.Is<FriendDTO>(d => d.PlayerId == 30 && d.Nickname == "A")), 
                Times.Once);
        }

        [Fact]
        public async Task SendFriendRequestAsync_AddresseeNotFound_ShouldReturnUserNotFound()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(MakeAccount(300, 30, "A"));
            accountRepository.Setup(a => a.GetUserByEmailAsync("x@test.com")).ReturnsAsync((UserAccount?)null);

            var result = await sut.SendFriendRequestAsync("x@test.com");
            
            Assert.Equal(FriendRequestResult.UserNotFound, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_RequesterWithoutPlayer_ShouldReturnFailed()
        {
            
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var requesterNoPlayer = new UserAccount { Id = 300, Nickname = "A", Player = new List<Player>() };
            var addressee = MakeAccount(310, 31, "B");
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requesterNoPlayer);
            accountRepository.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);

            var result = await sut.SendFriendRequestAsync("b@test.com");

            Assert.Equal(FriendRequestResult.Failed, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_CannotAddSelf_ShouldReturnCannotAddSelf()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var requester = MakeAccount(300, 30, "A");
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepository.Setup(a => a.GetUserByEmailAsync("a@test.com")).ReturnsAsync(requester);

            var result = await sut.SendFriendRequestAsync("a@test.com");

            Assert.Equal(FriendRequestResult.CannotAddSelf, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_AddresseeHasNoPlayer_ShouldReturnUserNotFound()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var requester = MakeAccount(300, 30, "A");
            var addresseeNoPlayer = new UserAccount { Id = 310, Nickname = "B", Player = new List<Player>() };
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepository.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addresseeNoPlayer);

            var result = await sut.SendFriendRequestAsync("b@test.com");

            Assert.Equal(FriendRequestResult.UserNotFound, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300))
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

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackB.Object);
            sut.SubscribeToFriendUpdatesAsync(310);
            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            var requesterAcc = MakeAccount(310, 31, "Requester");
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(310)).ReturnsAsync(requesterAcc);
            accountRepository.Setup(a => a.GetUserByPlayerIdAsync(31)).ReturnsAsync(requesterAcc);
            friendshipRepository.Setup(r => r.RespondToFriendRequestAsync(31, 30, accept)).ReturnsAsync(true);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);

            await sut.RespondToFriendRequestAsync(31, accept);

            if (accept)
            {
                callbackA.Verify(c => c.OnFriendAdded(It.Is<FriendDTO>(
                    d => d.PlayerId == 31 && d.Nickname == "Requester")), Times.Once);
                callbackB.Verify(c => c.OnFriendAdded(It.Is<FriendDTO>(
                    d => d.PlayerId == 30 && d.Nickname == "Addressee")), Times.Once);
            }
            else
            {
                callbackA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
                callbackB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
            }
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_WhenNoCallback_ShouldNotNotify()
        {

            await sut.RespondToFriendRequestAsync(31, true);

            callbackA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
            callbackB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_WhenAddresseeNotFound_ShouldNotNotify()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync((UserAccount?)null);

            await sut.RespondToFriendRequestAsync(31, true);

            callbackA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_WhenAddresseeHasNoPlayer_ShouldNotNotify()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var addresseeNoPlayer = new UserAccount { Id = 300, Nickname = "A", Player = new List<Player>() };
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeNoPlayer);

            await sut.RespondToFriendRequestAsync(31, true);

            callbackA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_WhenRepositoryReturnsFalse_ShouldNotNotify()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            friendshipRepository.Setup(r => r.RespondToFriendRequestAsync(31, 30, true)).ReturnsAsync(false);

            await sut.RespondToFriendRequestAsync(31, true);

            callbackA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_ShouldThrowFault_WhenRepositoryThrowsException()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300))
                .ThrowsAsync(new Exception("Error"));

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => sut.RespondToFriendRequestAsync(31, true)
            );

            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
        }

        [Fact]
        public async Task ValidateFriendRequestAsync_Paths_ShouldReturnExpectedCodes()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var requester = MakeAccount(300, 30, "A");
            var addressee = MakeAccount(310, 31, "B");
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepository.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);
            friendshipRepository.Setup(r => r.GetFriendsByUserAccountIdAsync(300))
                .ReturnsAsync(new List<UserAccount> { addressee });
            
            var result = await sut.SendFriendRequestAsync("b@test.com");

            Assert.Equal(FriendRequestResult.AlreadyFriends, result);

            friendshipRepository.Setup(r => r.GetFriendsByUserAccountIdAsync(300))
                .ReturnsAsync(new List<UserAccount>());
            friendshipRepository.Setup(r => r.GetPendingRequestsAsync(310))
                .ReturnsAsync(new List<Friendship> { new Friendship { RequesterId = 30 } });

            result = await sut.SendFriendRequestAsync("b@test.com");

            Assert.Equal(FriendRequestResult.RequestAlreadySent, result);

            friendshipRepository.Setup(r => r.GetPendingRequestsAsync(310))
                .ReturnsAsync(new List<Friendship>());
            friendshipRepository.Setup(r => r.GetPendingRequestsAsync(300))
                .ReturnsAsync(new List<Friendship> { new Friendship { RequesterId = 31 } });

            result = await sut.SendFriendRequestAsync("b@test.com");

            Assert.Equal(FriendRequestResult.RequestAlreadyReceived, result);
        }

        [Fact]
        public async Task CreateAndNotifyFriendRequestAsync_Failure_ShouldReturnFailed()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var requester = MakeAccount(300, 30, "A");
            var addressee = MakeAccount(310, 31, "B");
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepository.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);
            friendshipRepository.Setup(r => r.GetFriendsByUserAccountIdAsync(300)).ReturnsAsync(new List<UserAccount>());
            friendshipRepository.Setup(r => r.GetPendingRequestsAsync(310)).ReturnsAsync(new List<Friendship>());
            friendshipRepository.Setup(r => r.GetPendingRequestsAsync(300)).ReturnsAsync(new List<Friendship>());
            friendshipRepository.Setup(r => r.CreateFriendRequestAsync(30, 31)).ReturnsAsync(false);

            var result = await sut.SendFriendRequestAsync("b@test.com");

            Assert.Equal(FriendRequestResult.Failed, result);
            callbackB.Verify(c => c.OnFriendRequestReceived(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task SubscribeToFriendUpdatesAsync_ShouldAddClientToConnected()
        {

            var callback = new Mock<IFriendsCallback>();
            callback.As<ICommunicationObject>();
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callback.Object);
            sut.SubscribeToFriendUpdatesAsync(500);
            friendshipRepository.Setup(r => r.GetPendingRequestsAsync(500)).ReturnsAsync(new List<Friendship>());

            var list = await sut.GetPendingRequestsAsync();

            Assert.NotNull(list);
        }

        [Fact]
        public async Task UnsubscribeFromFriendUpdatesAsync_ShouldRemoveClient()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(500);

            await sut.UnsubscribeFromFriendUpdatesAsync(500);
            var list = await sut.GetPendingRequestsAsync();

            Assert.Empty(list);
        }

        [Fact]
        public async Task NotifyOnRequestAcceptedAsync_WhenRequesterNotFound_ShouldNotNotify()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackB.Object);
            sut.SubscribeToFriendUpdatesAsync(310);
            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            accountRepository.Setup(a => a.GetUserByPlayerIdAsync(31)).ReturnsAsync((UserAccount?)null);
            friendshipRepository.Setup(r => r.RespondToFriendRequestAsync(31, 30, true)).ReturnsAsync(true);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);

            await sut.RespondToFriendRequestAsync(31, true);
            
            callbackA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
            callbackB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task NotifyOnRequestAcceptedAsync_WhenRequesterHasNoPlayer_ShouldNotNotify()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackB.Object);
            sut.SubscribeToFriendUpdatesAsync(310);
            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            var requesterNoPlayer = new UserAccount { Id = 310, Nickname = "B", Player = new List<Player>() };
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            accountRepository.Setup(a => a.GetUserByPlayerIdAsync(31)).ReturnsAsync(requesterNoPlayer);
            friendshipRepository.Setup(r => r.RespondToFriendRequestAsync(31, 30, true)).ReturnsAsync(true);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);

            await sut.RespondToFriendRequestAsync(31, true);

            callbackA.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
            callbackB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task NotifyFriendRemovedAsync_WhenCurrentUserNotFound_ShouldNotifyOnlyFriend()
        {

            friendshipRepository.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(true);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackB.Object);
            sut.SubscribeToFriendUpdatesAsync(110);
            accountRepository.Setup(a => a.GetUserByPlayerIdAsync(10)).ReturnsAsync((UserAccount?)null);
            accountRepository.Setup(a => a.GetUserByPlayerIdAsync(11)).ReturnsAsync(MakeAccount(110, 11, "B"));

            var res = await sut.DeleteFriendAsync(10, 11);

            Assert.True(res);
            callbackB.Verify(c => c.OnFriendRemoved(10), Times.Once);
        }

        [Fact]
        public async Task NotifyFriendRemovedAsync_WhenFriendNotFound_ShouldNotifyOnlyCurrentUser()
        {

            friendshipRepository.Setup(r => r.DeleteFriendshipAsync(10, 11)).ReturnsAsync(true);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(100);
            accountRepository.Setup(a => a.GetUserByPlayerIdAsync(10)).ReturnsAsync(MakeAccount(100, 10, "A"));
            accountRepository.Setup(a => a.GetUserByPlayerIdAsync(11)).ReturnsAsync((UserAccount?)null);

            var res = await sut.DeleteFriendAsync(10, 11);

            Assert.True(res);
            callbackA.Verify(c => c.OnFriendRemoved(11), Times.Once);
        }

        [Fact]
        public async Task SendFriendRequestAsync_ShouldNotNotifyAddressee_WhenNotSubscribed()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var requester = MakeAccount(300, 30, "A");
            var addressee = MakeAccount(310, 31, "B");
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(requester);
            accountRepository.Setup(a => a.GetUserByEmailAsync("b@test.com")).ReturnsAsync(addressee);
            friendshipRepository.Setup(r => r.GetFriendsByUserAccountIdAsync(300))
                .ReturnsAsync(new List<UserAccount>());
            friendshipRepository.Setup(r => r.GetPendingRequestsAsync(310))
                .ReturnsAsync(new List<Friendship>());
            friendshipRepository.Setup(r => r.GetPendingRequestsAsync(300))
                .ReturnsAsync(new List<Friendship>());
            friendshipRepository.Setup(r => r.CreateFriendRequestAsync(30, 31)).ReturnsAsync(true);
            
            var result = await sut.SendFriendRequestAsync("b@test.com");

            Assert.Equal(FriendRequestResult.Success, result);
            callbackB.Verify(c => c.OnFriendRequestReceived(It.IsAny<FriendDTO>()), Times.Never);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_WhenAccepted_ShouldNotifyOnlyConnectedClients()
        {

            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);
            sut.SubscribeToFriendUpdatesAsync(300);
            var addresseeAcc = MakeAccount(300, 30, "Addressee");
            var requesterAcc = MakeAccount(310, 31, "Requester");
            accountRepository.Setup(a => a.GetUserByUserAccountIdAsync(300)).ReturnsAsync(addresseeAcc);
            accountRepository.Setup(a => a.GetUserByPlayerIdAsync(31)).ReturnsAsync(requesterAcc);
            friendshipRepository.Setup(r => r.RespondToFriendRequestAsync(31, 30, true)).ReturnsAsync(true);
            contextWrapper.Setup(c => c.GetCallbackChannel<IFriendsCallback>()).Returns(callbackA.Object);

            await sut.RespondToFriendRequestAsync(31, true);

            callbackA.Verify(c => c.OnFriendAdded(It.Is<FriendDTO>(d => d.PlayerId == 31 && d.Nickname == "Requester")),
                Times.Once);
            callbackB.Verify(c => c.OnFriendAdded(It.IsAny<FriendDTO>()), Times.Never);
        }
    }
}
