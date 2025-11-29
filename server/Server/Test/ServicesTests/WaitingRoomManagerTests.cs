using System.Collections.Concurrent;
using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Services;
using Services.Wrappers;
using System.ServiceModel;
using Services.Util;
using System.Net.Mail;

namespace Test.ServicesTests
{
    public class WaitingRoomManagerTests
    {
        private readonly Mock<IPlayerRepository> mockPlayerRepo;
        private readonly Mock<IOperationContextWrapper> mockOperationContext;
        private readonly Mock<IAccountRepository> mockAccountRepository;
        private readonly Mock<INotificationService> mockNotificationService;

        public WaitingRoomManagerTests()
        {
            mockPlayerRepo = new Mock<IPlayerRepository>();
            mockOperationContext = new Mock<IOperationContextWrapper>();
            mockAccountRepository = new Mock<IAccountRepository>();
            mockNotificationService = new Mock<INotificationService>();
        }

        private (WaitingRoomManager sut, ConcurrentQueue<Mock<IWaitingRoomCallback>> callbacks, Mock<IGameManager> gameManager) CreateSut(Mock<IPlayerRepository> repo, Mock<IOperationContextWrapper> ctx)
        {
            var callbackQueue = new ConcurrentQueue<Mock<IWaitingRoomCallback>>();

            // Return a different callback channel on each Join call (dequeue order)
            ctx.Setup(o => o.GetCallbackChannel<IWaitingRoomCallback>())
                .Returns(() =>
                {
                    if (callbackQueue.TryDequeue(out var cb))
                    {
                        return cb.Object;
                    }
                    // Fallback callback to avoid nulls in corner cases
                    var fallback = new Mock<IWaitingRoomCallback>();
                    return fallback.Object;
                });

            var gm = new Mock<IGameManager>();
            var sut = new WaitingRoomManager(repo.Object, ctx.Object, gm.Object, mockAccountRepository.Object, mockNotificationService.Object);
            return (sut, callbackQueue, gm);
        }

        private static Player MakePlayer(int id, string email, string nickname)
        {
            return new Player
            {
                Id = id,
                UserAccountId = id,
                UserAccount = new UserAccount
                {
                    Email = email,
                    Nickname = nickname,
                }
            };
        }

        [Fact]
        public async Task CreateGame_ShouldCreateGameAndSetHost()
        {
            // Arrange
            var email = "host@test.com";
            var hostPlayer = MakePlayer(1, email, "HostUser");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(email)).ReturnsAsync(hostPlayer);
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var hostCallback = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(hostCallback);

            // Act
            var gameCode = await sut.CreateRoomAsync(email);

            // Assert
            var players = await sut.GetPlayersInRoomAsync(gameCode);
            var p = players.FirstOrDefault();
            var expected = (
                CodeNotNull: true,
                CodeLen: 5,
                PlayerCount: 1,
                HostId: 1,
                HostNick: (string?)"HostUser",
                HostRole: PlayerRole.ClueGuy,
                HostTeam: MatchTeam.RedTeam,
                HostJoinedNotified: true
            );
            var actual = (
                CodeNotNull: gameCode != null,
                CodeLen: gameCode?.Length ?? -1,
                PlayerCount: players.Count,
                HostId: p?.Id ?? -1,
                HostNick: p?.Nickname,
                HostRole: p?.Role ?? PlayerRole.ClueGuy,
                HostTeam: p?.Team ?? MatchTeam.RedTeam,
                HostJoinedNotified: hostCallback.Invocations.Any(i => i.Method.Name == nameof(IWaitingRoomCallback.OnPlayerJoined))
            );
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task JoinGameAsRegisteredPlayer_ShouldSucceed_AndPreventDuplicates()
        {
            // Arrange
            var email = "user@test.com";
            var player = MakePlayer(2, email, "User2");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(email)).ReturnsAsync(player);

            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            // Host setup
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);
            var hostCb = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(hostCb);
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            // Player join
            var userCb = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(userCb);
            var id = await sut.JoinRoomAsRegisteredPlayerAsync(gameCode, email);

            // Second attempt should throw AlreadyInRoom fault
            bool secondJoinFault = false;
            try { callbacks.Enqueue(new Mock<IWaitingRoomCallback>()); await sut.JoinRoomAsRegisteredPlayerAsync(gameCode, email); } catch (FaultException<ServiceErrorDetailDTO>) { secondJoinFault = true; }

            // Assert
            var players = await sut.GetPlayersInRoomAsync(gameCode);
            var expected = new { FirstJoinId = 2, SecondJoinFault = true, PlayerCount = 2, HasHost = true, HasUser = true };
            var actual = new
            {
                FirstJoinId = id,
                SecondJoinFault = secondJoinFault,
                PlayerCount = players.Count,
                HasHost = players.Any(x => x.Id == 1),
                HasUser = players.Any(x => x.Id == 2)
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task JoinGameAsRegisteredPlayer_ShouldFail_WhenPlayerDoesNotExist()
        {
            // Arrange
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync("ghost@test.com")).ReturnsAsync((Player?)null);

            // Act + Assert
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsRegisteredPlayerAsync(gameCode, "ghost@test.com"));

            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Single(players);
        }

        [Fact]
        public async Task JoinGameAsRegisteredPlayer_ShouldFail_WhenRoomNotFound()
        {
            // Arrange
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);

            // Act + Assert
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsRegisteredPlayerAsync("XXXXX", "user@test.com"));
        }

        [Fact]
        public async Task JoinGameAsRegisteredPlayer_ShouldFail_WhenRoomIsFull()
        {
            // Arrange: host +3 guests -> room full
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            await sut.JoinRoomAsGuestAsync(gameCode, "G1");
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            await sut.JoinRoomAsGuestAsync(gameCode, "G2");
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            await sut.JoinRoomAsGuestAsync(gameCode, "G3");

            // Attempt registered join when full
            var reg = MakePlayer(10, "full@test.com", "FullUser");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync("full@test.com")).ReturnsAsync(reg);
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());

            // Act + Assert
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsRegisteredPlayerAsync(gameCode, "full@test.com"));

            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Equal(4, players.Count);
        }

        [Fact]
        public async Task JoinGameAsGuest_ShouldAllowUntilFull_ThenFail()
        {
            // Arrange
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            // Guest1,2,3 (reaching4 total including host)
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var g1 = await sut.JoinRoomAsGuestAsync(gameCode, "Guest1");
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var g2 = await sut.JoinRoomAsGuestAsync(gameCode, "Guest2");
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var g3 = await sut.JoinRoomAsGuestAsync(gameCode, "Guest3");

            //5th player should throw (room full)
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest4"));

            // Assert
            Assert.True(g1);
            Assert.True(g2);
            Assert.True(g3);

            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Equal(4, players.Count);

            var hostDto = players.First(p => p.Id ==1);
            Assert.Equal(MatchTeam.RedTeam, hostDto.Team);
            Assert.Equal(PlayerRole.ClueGuy, hostDto.Role);
        }

        [Fact]
        public async Task JoinGameAsGuest_ShouldFail_WhenRoomNotFound()
        {
            // Arrange
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);

            // Act + Assert
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsGuestAsync("XXXXX", "Guest"));
        }

        [Fact]
        public async Task SendMessage_ShouldBroadcastToAllPlayers()
        {
            // Arrange
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);

            var hostCb = new Mock<IWaitingRoomCallback>();
            var g1Cb = new Mock<IWaitingRoomCallback>();
            var g2Cb = new Mock<IWaitingRoomCallback>();
            var g3Cb = new Mock<IWaitingRoomCallback>();

            callbacks.Enqueue(hostCb);
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            callbacks.Enqueue(g1Cb);
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest1");
            callbacks.Enqueue(g2Cb);
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest2");
            callbacks.Enqueue(g3Cb);
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest3");

            var msg = new ChatMessageDTO { SenderNickname = "Host", Message = "hello" };

            // Act
            await sut.SendMessageAsync(gameCode, msg);

            // Assert
            hostCb.Verify(c => c.OnMessageReceived(It.Is<ChatMessageDTO>(m => m.Message == "hello")), Times.Once);
            g1Cb.Verify(c => c.OnMessageReceived(It.IsAny<ChatMessageDTO>()), Times.Once);
            g2Cb.Verify(c => c.OnMessageReceived(It.IsAny<ChatMessageDTO>()), Times.Once);
            g3Cb.Verify(c => c.OnMessageReceived(It.IsAny<ChatMessageDTO>()), Times.Once);
        }

        [Fact]
        public async Task SendMessage_InvalidGameCode_ShouldNotNotify()
        {
            // Arrange
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var cb = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(cb);

            // Act
            await sut.SendMessageAsync("XXXXX", new ChatMessageDTO { Message = "x" });

            // Assert
            cb.Verify(c => c.OnMessageReceived(It.IsAny<ChatMessageDTO>()), Times.Never);
        }

        [Fact]
        public async Task StartGame_ShouldNotifyAndRemoveGame()
        {
            // Arrange
            var (sut, callbacks, gm) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);

            var hostCb = new Mock<IWaitingRoomCallback>();
            var g1Cb = new Mock<IWaitingRoomCallback>();
            var g2Cb = new Mock<IWaitingRoomCallback>();
            var g3Cb = new Mock<IWaitingRoomCallback>();

            callbacks.Enqueue(hostCb);
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            callbacks.Enqueue(g1Cb);
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest1");
            callbacks.Enqueue(g2Cb);
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest2");
            callbacks.Enqueue(g3Cb);
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest3");

            gm.Setup(m => m.CreateMatch(gameCode, It.IsAny<List<PlayerDTO>>())).Returns(true);

            // Act
            await sut.StartGameAsync(gameCode);

            // Assert
            hostCb.Verify(c => c.OnGameStarted(), Times.Once);
            g1Cb.Verify(c => c.OnGameStarted(), Times.Once);
            g2Cb.Verify(c => c.OnGameStarted(), Times.Once);
            g3Cb.Verify(c => c.OnGameStarted(), Times.Once);

            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Empty(players);

            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsGuestAsync(gameCode, "Later"));
        }

        [Fact]
        public async Task StartGame_ShouldFail_WhenRoomNotFound()
        {
            // Arrange
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);

            // Act + Assert
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () => await sut.StartGameAsync("XXXXX"));
        }

        [Fact]
        public async Task StartGame_ShouldFail_WhenNotEnoughPlayers()
        {
            // Arrange
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            // Act + Assert
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () => await sut.StartGameAsync(gameCode));
        }

        [Fact]
        public async Task LeaveGame_HostLeaving_ShouldNotifyAndRemoveGame()
        {
            // Arrange
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);

            var hostCb = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(hostCb);
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            // Act
            await sut.LeaveRoomAsync(gameCode, host.Id);

            // Assert
            hostCb.Verify(c => c.OnHostLeft(), Times.Once);
            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Empty(players);
        }

        [Fact]
        public async Task LeaveGame_PlayerLeaving_ShouldRemovePlayerAndNotifyOthers()
        {
            // Arrange
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);
            var hostCb = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(hostCb);
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            var g1Cb = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(g1Cb);
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest1");

            var playersBefore = await sut.GetPlayersInRoomAsync(gameCode);
            var guestId = playersBefore.First(p => p.Nickname == "Guest1").Id; 

            // Act
            await sut.LeaveRoomAsync(gameCode, guestId);

            // Assert
            hostCb.Verify(c => c.OnPlayerLeft(It.Is<int>(x => x == guestId)), Times.Once);
            var playersAfter = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Single(playersAfter);
            Assert.Equal(1, playersAfter[0].Id);
        }

        [Fact]
        public async Task LeaveGame_InvalidGameCodeOrPlayer_ShouldNoop()
        {
            // Arrange
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            var playersBefore = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Single(playersBefore);

            // Act
            await sut.LeaveRoomAsync("WRONG",1);
            // Act
            await sut.LeaveRoomAsync(gameCode,999);

            // Assert
            var playersAfter = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Single(playersAfter);
        }

        [Fact]
        public async Task HostLeftAsync_ShouldNotifyAllAndRemoveGame()
        {
            // Arrange
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);

            var hostCb = new Mock<IWaitingRoomCallback>();
            var g1Cb = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(hostCb);
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);
            callbacks.Enqueue(g1Cb);
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest1");

            // Act
            await sut.HostLeftAsync(gameCode);

            // Assert
            hostCb.Verify(c => c.OnHostLeft(), Times.Once);
            g1Cb.Verify(c => c.OnHostLeft(), Times.Once);
            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Empty(players);
        }

        [Fact]
        public async Task Broadcast_ShouldRemoveClientThatThrowsDuringMessage()
        {
            // Arrange
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);

            var hostCb = new Mock<IWaitingRoomCallback>();
            var g1Cb = new Mock<IWaitingRoomCallback>();

            // Make guest callback throw on message
            g1Cb.Setup(c => c.OnMessageReceived(It.IsAny<ChatMessageDTO>())).Throws(new System.Exception("client error"));

            callbacks.Enqueue(hostCb);
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);
            callbacks.Enqueue(g1Cb);
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest1");

            // Act: send message triggers broadcast; guest will be removed
            await sut.SendMessageAsync(gameCode, new ChatMessageDTO { Message = "hi" });

            // Assert
            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Single(players);
            Assert.Equal(1, players[0].Id);

           
            hostCb.Verify(c => c.OnMessageReceived(It.IsAny<ChatMessageDTO>()), Times.Once);
        }

        [Fact]
        public async Task TeamAndRole_Assignment_ShouldFollowExpectedOrder()
        {
            // Arrange
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest1"); 
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest2"); 
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest3"); 

            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Equal(4, players.Count);

            var redClue = players.First(p => p.Team == MatchTeam.RedTeam && p.Role == PlayerRole.ClueGuy);
            var blueClue = players.First(p => p.Team == MatchTeam.BlueTeam && p.Role == PlayerRole.ClueGuy);
            var redGuess = players.First(p => p.Team == MatchTeam.RedTeam && p.Role == PlayerRole.Guesser);
            var blueGuess = players.First(p => p.Team == MatchTeam.BlueTeam && p.Role == PlayerRole.Guesser);

            Assert.Equal(1, redClue.Id);
            Assert.NotEqual(0, blueClue.Id);
            Assert.NotEqual(0, redGuess.Id);
            Assert.NotEqual(0, blueGuess.Id);
        }

        [Fact]
        public async Task SendGameInvitationByEmailAsync_ShouldSend()
        {
            // Arrange
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);

            mockAccountRepository
                .Setup(a => a.GetUserByEmailAsync("friend@test.com"))
                .ReturnsAsync(new UserAccount
                {
                    Email = "friend@test.com",
                    Nickname = "Friend"  
                });

            mockNotificationService
                .Setup(n => n.SendGameInvitationEmailAsync("friend@test.com", It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await sut.SendGameInvitationByEmailAsync("friend@test.com", "ABCDE", "Host");

            // Assert
            mockNotificationService.Verify(n => n.SendGameInvitationEmailAsync("friend@test.com", "ABCDE", "Host"), Times.Once);
        }

        [Fact]
        public async Task SendGameInvitationByEmailAsync_SmtpError_ShouldThrowFault()
        {
            // Arrange
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            mockNotificationService
            .Setup(n => n.SendGameInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new SmtpException("smtp error"));

            // Act + Assert
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() =>
            sut.SendGameInvitationByEmailAsync("friend@test.com", "ABCDE", "Host"));
        }

        [Fact]
        public async Task SendGameInvitationByEmailAsync_UnexpectedError_ShouldThrowFault()
        {
            // Arrange
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            mockNotificationService
            .Setup(n => n.SendGameInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("boom"));

            // Act + Assert
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() =>
            sut.SendGameInvitationByEmailAsync("friend@test.com", "ABCDE", "Host"));
        }

        [Fact]
        public async Task SendGameInvitationToFriendAsync_ShouldSend()
        {
            // Arrange
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            mockAccountRepository.Setup(a => a.GetUserByPlayerIdAsync(42)).ReturnsAsync(new UserAccount { Email = "friend@test.com" });

            // Act
            await sut.SendGameInvitationToFriendAsync(42, "ABCDE", "Host");

            // Assert
            mockNotificationService.Verify(n => n.SendGameInvitationEmailAsync("friend@test.com", "ABCDE", "Host"), Times.Once);
        }

        [Fact]
        public async Task SendGameInvitationToFriendAsync_FriendNotFound_ShouldThrowFault()
        {
            // Arrange
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            mockAccountRepository.Setup(a => a.GetUserByPlayerIdAsync(42)).ReturnsAsync((UserAccount?)null);

            // Act + Assert
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() =>
            sut.SendGameInvitationToFriendAsync(42, "ABCDE", "Host"));
        }

        [Fact]
        public async Task SendGameInvitationToFriendAsync_SmtpError_ShouldThrowFault()
        {
            // Arrange
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            mockAccountRepository.Setup(a => a.GetUserByPlayerIdAsync(42)).ReturnsAsync(new UserAccount { Email = "friend@test.com" });
            mockNotificationService
            .Setup(n => n.SendGameInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new SmtpException("smtp error"));

            // Act + Assert
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() =>
            sut.SendGameInvitationToFriendAsync(42, "ABCDE", "Host"));
        }

        [Fact]
        public async Task SendGameInvitationToFriendAsync_UnexpectedError_ShouldThrowFault()
        {
            // Arrange
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            mockAccountRepository.Setup(a => a.GetUserByPlayerIdAsync(42)).ReturnsAsync(new UserAccount { Email = "friend@test.com" });
            mockNotificationService
            .Setup(n => n.SendGameInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("boom"));

            // Act + Assert
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() =>
            sut.SendGameInvitationToFriendAsync(42, "ABCDE", "Host"));
        }
    }
}
