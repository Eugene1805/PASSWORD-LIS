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

namespace Test.ServicesTests
{
    public class WaitingRoomManagerTests
    {/*
        private readonly Mock<IPlayerRepository> mockPlayerRepo;
        private readonly Mock<IOperationContextWrapper> mockOperationContext;

        public WaitingRoomManagerTests()
        {
            mockPlayerRepo = new Mock<IPlayerRepository>();
            mockOperationContext = new Mock<IOperationContextWrapper>();
        }

        private static (WaitingRoomManager sut, ConcurrentQueue<Mock<IWaitingRoomCallback>> callbacks, Mock<IGameManager> gameManager) CreateSut(Mock<IPlayerRepository> repo, Mock<IOperationContextWrapper> ctx)
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
            var sut = new WaitingRoomManager(repo.Object, ctx.Object, gm.Object);
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
            Assert.NotNull(gameCode);
            Assert.Equal(5, gameCode.Length);

            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Single(players);
            var p = players[0];
            Assert.Equal(1, p.Id);
            Assert.Equal("HostUser", p.Nickname);
            Assert.Equal(PlayerRole.ClueGuy, p.Role);
            Assert.Equal(MatchTeam.RedTeam, p.Team);

            hostCallback.Verify(c => c.OnPlayerJoined(It.IsAny<PlayerDTO>()), Times.Once);
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
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsRegisteredPlayerAsync(gameCode, email));

            // Assert
            Assert.Equal(2, id);

            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Equal(2, players.Count);
            Assert.Contains(players, x => x.Id ==1);
            Assert.Contains(players, x => x.Id ==2);
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

            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync("ghost@test.com")).ReturnsAsync((Player)null);

            // Act + Assert
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsRegisteredPlayerAsync(gameCode, "ghost@test.com"));

            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Single(players);
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

            // Validate team/role assignment order
            // Order of joins: Host(0), G1(1), G2(2), G3(3)
            var hostDto = players.First(p => p.Id ==1);
            Assert.Equal(MatchTeam.RedTeam, hostDto.Team);
            Assert.Equal(PlayerRole.ClueGuy, hostDto.Role);
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
            Assert.Empty(players); // game removed

            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsGuestAsync(gameCode, "Later"));
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
            var guestId = playersBefore.First(p => p.Nickname == "Guest1").Id; // negative id

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

            // Act: wrong code
            await sut.LeaveRoomAsync("WRONG",1);
            // Act: wrong player in valid game
            await sut.LeaveRoomAsync(gameCode,999);

            // Assert: still single player
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

            // Host receives the message
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
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest1"); // player count1
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest2"); // player count2
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest3"); // player count3

            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Equal(4, players.Count);

            // Determine join order by team/role assumptions:
            var redClue = players.First(p => p.Team == MatchTeam.RedTeam && p.Role == PlayerRole.ClueGuy);
            var blueClue = players.First(p => p.Team == MatchTeam.BlueTeam && p.Role == PlayerRole.ClueGuy);
            var redGuess = players.First(p => p.Team == MatchTeam.RedTeam && p.Role == PlayerRole.Guesser);
            var blueGuess = players.First(p => p.Team == MatchTeam.BlueTeam && p.Role == PlayerRole.Guesser);

            Assert.Equal(1, redClue.Id); // host
            Assert.NotEqual(0, blueClue.Id);
            Assert.NotEqual(0, redGuess.Id);
            Assert.NotEqual(0, blueGuess.Id);
       }*/
    }
}
