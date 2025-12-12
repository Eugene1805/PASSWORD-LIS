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

        private (WaitingRoomManager sut, ConcurrentQueue<Mock<IWaitingRoomCallback>> callbacks,
            Mock<IGameManager> gameManager) CreateSut(Mock<IPlayerRepository> repo, Mock<IOperationContextWrapper> ctx)
        {
            var callbackQueue = new ConcurrentQueue<Mock<IWaitingRoomCallback>>();

            ctx.Setup(o => o.GetCallbackChannel<IWaitingRoomCallback>())
                .Returns(() =>
                {
                    if (callbackQueue.TryDequeue(out var cb))
                    {
                        return cb.Object;
                    }
                    var fallback = new Mock<IWaitingRoomCallback>();
                    return fallback.Object;
                });

            var gm = new Mock<IGameManager>();
            var sut = new WaitingRoomManager(repo.Object, ctx.Object, gm.Object, mockAccountRepository.Object,
                mockNotificationService.Object);
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
            var email = "host@test.com";
            var hostPlayer = MakePlayer(1, email, "HostUser");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(email)).ReturnsAsync(hostPlayer);
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var hostCallback = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(hostCallback);
            
            var gameCode = await sut.CreateRoomAsync(email);
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
                HostJoinedNotified: hostCallback.Invocations.Any(i 
                => i.Method.Name == nameof(IWaitingRoomCallback.OnPlayerJoined))
            );
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task JoinGameAsRegisteredPlayer_ShouldSucceed_AndPreventDuplicates()
        {
            
            var email = "user@test.com";
            var player = MakePlayer(2, email, "User2");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(email)).ReturnsAsync(player);

            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);
            var hostCb = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(hostCb);
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            var userCb = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(userCb);
            var id = await sut.JoinRoomAsRegisteredPlayerAsync(gameCode, email);

            bool secondJoinFault = false;
            try 
            { 
                callbacks.Enqueue(new Mock<IWaitingRoomCallback>()); 
                await sut.JoinRoomAsRegisteredPlayerAsync(gameCode, email); 
            } 
            catch (FaultException<ServiceErrorDetailDTO>) 
            { 
                secondJoinFault = true; 
            }

            
            var players = await sut.GetPlayersInRoomAsync(gameCode);
            var expected = new 
            { 
                FirstJoinId = 2, 
                SecondJoinFault = true, 
                PlayerCount = 2, 
                HasHost = true, 
                HasUser = true 
            };
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
            
            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync("ghost@test.com")).ReturnsAsync((Player?)null);

            
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsRegisteredPlayerAsync(gameCode, "ghost@test.com"));

            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Single(players);
        }

        [Fact]
        public async Task JoinGameAsRegisteredPlayer_ShouldFail_WhenRoomNotFound()
        {

            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsRegisteredPlayerAsync("XXXXX", "user@test.com"));
        }

        [Fact]
        public async Task JoinGameAsRegisteredPlayer_ShouldFail_WhenRoomIsFull()
        {

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


            var reg = MakePlayer(10, "full@test.com", "FullUser");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync("full@test.com")).ReturnsAsync(reg);
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());

            
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsRegisteredPlayerAsync(gameCode, "full@test.com"));

            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Equal(4, players.Count);
        }

        [Fact]
        public async Task JoinGameAsGuest_ShouldAllowUntilFull_ThenFail()
        {

            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var g1 = await sut.JoinRoomAsGuestAsync(gameCode, "Guest1");
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var g2 = await sut.JoinRoomAsGuestAsync(gameCode, "Guest2");
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var g3 = await sut.JoinRoomAsGuestAsync(gameCode, "Guest3");

            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest4"));


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

            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);

            
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () =>
            await sut.JoinRoomAsGuestAsync("XXXXX", "Guest"));
        }

        [Fact]
        public async Task SendMessage_ShouldBroadcastToAllPlayers()
        {

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

            var msg = new ChatMessageDTO 
            { 
                SenderNickname = "Host",
                Message = "hello" 
            };

            await sut.SendMessageAsync(gameCode, msg);

            hostCb.Verify(c => c.OnMessageReceived(It.Is<ChatMessageDTO>(m => m.Message == "hello")), Times.Once);
            g1Cb.Verify(c => c.OnMessageReceived(It.IsAny<ChatMessageDTO>()), Times.Once);
            g2Cb.Verify(c => c.OnMessageReceived(It.IsAny<ChatMessageDTO>()), Times.Once);
            g3Cb.Verify(c => c.OnMessageReceived(It.IsAny<ChatMessageDTO>()), Times.Once);
        }

        [Fact]
        public async Task SendMessage_InvalidGameCode_ShouldNotNotify()
        {

            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var cb = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(cb);


            await sut.SendMessageAsync("XXXXX", new ChatMessageDTO 
            { 
                Message = "x" 
            });


            cb.Verify(c => c.OnMessageReceived(It.IsAny<ChatMessageDTO>()), Times.Never);
        }

        [Fact]
        public async Task StartGame_ShouldNotifyAndRemoveGame()
        {

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


            await sut.StartGameAsync(gameCode);


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

            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);

            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () 
                => await sut.StartGameAsync("XXXXX"));
        }

        [Fact]
        public async Task StartGame_ShouldFail_WhenNotEnoughPlayers()
        {

            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);


            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () 
                => await sut.StartGameAsync(gameCode));
        }

        [Fact]
        public async Task LeaveGame_HostLeaving_ShouldNotifyAndRemoveGame()
        {

            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);

            var hostCb = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(hostCb);
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);


            await sut.LeaveRoomAsync(gameCode, host.Id);


            hostCb.Verify(c => c.OnHostLeft(), Times.Once);
            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Empty(players);
        }

        [Fact]
        public async Task LeaveGame_PlayerLeaving_ShouldRemovePlayerAndNotifyOthers()
        {

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


            await sut.LeaveRoomAsync(gameCode, guestId);

            hostCb.Verify(c => c.OnPlayerLeft(It.Is<int>(x => x == guestId)), Times.Once);
            var playersAfter = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Single(playersAfter);
            Assert.Equal(1, playersAfter[0].Id);
        }

        [Fact]
        public async Task LeaveGame_InvalidGameCodeOrPlayer_ShouldNoop()
        {

            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);
            callbacks.Enqueue(new Mock<IWaitingRoomCallback>());
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);

            var playersBefore = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Single(playersBefore);


            await sut.LeaveRoomAsync("WRONG",1);

            await sut.LeaveRoomAsync(gameCode,999);


            var playersAfter = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Single(playersAfter);
        }

        [Fact]
        public async Task HostLeftAsync_ShouldNotifyAllAndRemoveGame()
        {

            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);

            var hostCb = new Mock<IWaitingRoomCallback>();
            var g1Cb = new Mock<IWaitingRoomCallback>();
            callbacks.Enqueue(hostCb);
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);
            callbacks.Enqueue(g1Cb);
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest1");

            await sut.HostLeftAsync(gameCode);

            hostCb.Verify(c => c.OnHostLeft(), Times.Once);
            g1Cb.Verify(c => c.OnHostLeft(), Times.Once);
            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Empty(players);
        }

        [Fact]
        public async Task Broadcast_ShouldRemoveClientThatThrowsDuringMessage()
        {

            var (sut, callbacks, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            var host = MakePlayer(1, "host@test.com", "Host");
            mockPlayerRepo.Setup(r => r.GetPlayerByEmailAsync(host.UserAccount.Email)).ReturnsAsync(host);

            var hostCb = new Mock<IWaitingRoomCallback>();
            var g1Cb = new Mock<IWaitingRoomCallback>();

            g1Cb.Setup(c => c.OnMessageReceived(It.IsAny<ChatMessageDTO>())).
                Throws(new System.Exception("client error"));

            callbacks.Enqueue(hostCb);
            var gameCode = await sut.CreateRoomAsync(host.UserAccount.Email);
            callbacks.Enqueue(g1Cb);
            await sut.JoinRoomAsGuestAsync(gameCode, "Guest1");


            await sut.SendMessageAsync(gameCode, new ChatMessageDTO 
            { 
                Message = "hi" 
            });


            var players = await sut.GetPlayersInRoomAsync(gameCode);
            Assert.Single(players);
            Assert.Equal(1, players[0].Id);

           
            hostCb.Verify(c => c.OnMessageReceived(It.IsAny<ChatMessageDTO>()), Times.Once);
        }

        [Fact]
        public async Task TeamAndRole_Assignment_ShouldFollowExpectedOrder()
        {

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


            await sut.SendGameInvitationByEmailAsync("friend@test.com", "ABCDE", "Host");

            mockNotificationService.Verify(n => n.SendGameInvitationEmailAsync("friend@test.com", "ABCDE", "Host"),
                Times.Once);
        }

        [Fact]
        public async Task SendGameInvitationByEmailAsync_SmtpError_ShouldThrowFault()
        {

            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            mockNotificationService
            .Setup(n => n.SendGameInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new SmtpException("smtp error"));


            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() =>
            sut.SendGameInvitationByEmailAsync("friend@test.com", "ABCDE", "Host"));
        }

        [Fact]
        public async Task SendGameInvitationByEmailAsync_UnexpectedError_ShouldThrowFault()
        {

            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            mockNotificationService
            .Setup(n => n.SendGameInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("boom"));

            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() =>
            sut.SendGameInvitationByEmailAsync("friend@test.com", "ABCDE", "Host"));
        }

        [Fact]
        public async Task SendGameInvitationToFriendAsync_ShouldSend()
        {
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            mockAccountRepository.Setup(a => a.GetUserByPlayerIdAsync(42)).ReturnsAsync(
                new UserAccount 
                { 
                    Email = "friend@test.com" 
                });

            await sut.SendGameInvitationToFriendAsync(42, "ABCDE", "Host");

            mockNotificationService.Verify(n => n.SendGameInvitationEmailAsync("friend@test.com", "ABCDE", "Host"),
                Times.Once);
        }

        [Fact]
        public async Task SendGameInvitationToFriendAsync_FriendNotFound_ShouldThrowFault()
        {

            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            mockAccountRepository.Setup(a => a.GetUserByPlayerIdAsync(42)).ReturnsAsync((UserAccount?)null);

            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() =>
            sut.SendGameInvitationToFriendAsync(42, "ABCDE", "Host"));
        }

        [Fact]
        public async Task SendGameInvitationToFriendAsync_SmtpError_ShouldThrowFault()
        {
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            mockAccountRepository.Setup(a => a.GetUserByPlayerIdAsync(42)).ReturnsAsync(
                new UserAccount 
                { 
                    Email = "friend@test.com"
                });
            mockNotificationService
            .Setup(n => n.SendGameInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new SmtpException("smtp error"));


            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() =>
            sut.SendGameInvitationToFriendAsync(42, "ABCDE", "Host"));
        }

        [Fact]
        public async Task SendGameInvitationToFriendAsync_UnexpectedError_ShouldThrowFault()
        {
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            mockAccountRepository.Setup(a => a.GetUserByPlayerIdAsync(42)).ReturnsAsync(
                new UserAccount 
                { 
                    Email = "friend@test.com" 
                });
            mockNotificationService
            .Setup(n => n.SendGameInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("boom"));

            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() =>
            sut.SendGameInvitationToFriendAsync(42, "ABCDE", "Host"));
        }

        [Fact]
        public async Task SendGameInvitationByEmailAsync_WhenInvitingSelf_ShouldThrowFaultException()
        {

            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            string myEmail = "me@test.com";
            string myNickname = "MyNick";
            string gameCode = "ABCDE";

            mockAccountRepository.Setup(a => a.GetUserByEmailAsync(myEmail))
                .ReturnsAsync(new UserAccount
                {
                    Email = myEmail,
                    Nickname = myNickname
                });

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => sut.SendGameInvitationByEmailAsync(myEmail, gameCode, myNickname)
            );

            Assert.Equal("SELF_INVITATION", ex.Detail.ErrorCode);

            mockNotificationService.Verify(
                n => n.SendGameInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task SendGameInvitationToFriendAsync_WhenInvitingSelf_ShouldThrowFaultException()
        {
            var (sut, _, _) = CreateSut(mockPlayerRepo, mockOperationContext);
            int friendId = 99;
            string myNickname = "MyNick";
            string gameCode = "ABCDE";

            mockAccountRepository.Setup(a => a.GetUserByPlayerIdAsync(friendId))
                .ReturnsAsync(new UserAccount
                {
                    Id = 1,
                    Email = "me@test.com",
                    Nickname = myNickname
                });

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => sut.SendGameInvitationToFriendAsync(friendId, gameCode, myNickname)
            );

            Assert.Equal("SELF_INVITATION", ex.Detail.ErrorCode);

            mockNotificationService.Verify(
                n => n.SendGameInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }
    }
}
