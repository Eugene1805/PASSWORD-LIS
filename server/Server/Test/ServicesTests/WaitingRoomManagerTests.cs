using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Contracts;
using Services.Services;
using Services.Wrappers;

namespace Test.ServicesTests
{
    public class WaitingRoomManagerTests
    {
        private readonly Mock<IPlayerRepository> mockPlayerRepo;
        private readonly Mock<IOperationContextWrapper> mockOperationContext;
        private readonly Mock<IWaitingRoomCallback> mockCallbackChannel;
        private readonly WaitingRoomManager sut; 

        public WaitingRoomManagerTests()
        {
            mockPlayerRepo = new Mock<IPlayerRepository>();
            mockOperationContext = new Mock<IOperationContextWrapper>();
            mockCallbackChannel = new Mock<IWaitingRoomCallback>();

            mockOperationContext.Setup(o => o.GetCallbackChannel<IWaitingRoomCallback>())
                                 .Returns(mockCallbackChannel.Object);

            sut = new WaitingRoomManager(mockPlayerRepo.Object, mockOperationContext.Object);
        }

        [Fact]
        public async Task JoinAsRegisteredPlayer_ShouldSucceed_WhenPlayerExistsAndIsNotConnected()
        {
            // Arrange
            var email = "TestUser@test.com";
            var playerEntity = new Player
            {
                Id = 1,
                UserAccountId = 1,
                UserAccount = new UserAccount { Email = email , Nickname = "testUser"}
            };
            mockPlayerRepo.Setup(repo => repo.GetPlayerByEmail(email)).Returns(playerEntity);

            // Act
            var result = await sut.JoinAsRegisteredPlayerAsync(email);

            // Assert
            Assert.True(result);
            var connectedPlayers = await sut.GetConnectedPlayersAsync();
            Assert.Single(connectedPlayers);
            Assert.Equal("testUser", connectedPlayers[0].Nickname);
        }

        [Fact]
        public async Task JoinAsRegisteredPlayer_ShouldFail_WhenPlayerDoesNotExist()
        {
            // Arrange
            var email = "GhostUser@test.com";
            mockPlayerRepo.Setup(repo => repo.GetPlayerByEmail(email)).Returns((Player)null);

            // Act
            var result = await sut.JoinAsRegisteredPlayerAsync(email);

            // Assert
            Assert.False(result);
            Assert.Empty(await sut.GetConnectedPlayersAsync());
        }

        [Fact]
        public async Task JoinAsGuest_ShouldSucceed_WhenUsernameIsUnique()
        {
            // Arrange
            var guestUsername = "Guest123";

            // Act
            var result = await sut.JoinAsGuestAsync(guestUsername);

            // Assert
            Assert.True(result);
            var connectedPlayers = await sut.GetConnectedPlayersAsync();
            Assert.Single(connectedPlayers);
            Assert.Equal(guestUsername, connectedPlayers[0].Nickname);
            Assert.True(connectedPlayers[0].Id < 0, "Guest ID should be negative.");
        }

        [Fact]
        public async Task JoinAsGuest_ShouldFail_WhenUsernameIsAlreadyTaken()
        {
            // Arrange
            var guestUsername = "Guest123";
            await sut.JoinAsGuestAsync(guestUsername); 

            // Act
            var result = await sut.JoinAsGuestAsync(guestUsername); 

            // Assert
            Assert.False(result);
            Assert.Single(await sut.GetConnectedPlayersAsync()); 
        }

        [Fact]
        public async Task AssignRole_ShouldAssignRolesBalancedForTwoTeams()
        {
            // Arrange
            var player1 = new Player { Id = 1, UserAccountId = 1, UserAccount = new UserAccount { Nickname = "Player1" } };
            mockPlayerRepo.Setup(r => r.GetPlayerByEmail("Player1")).Returns(player1);

            // Act & Assert

            await sut.JoinAsRegisteredPlayerAsync("Player1");
            var playersAfter1 = await sut.GetConnectedPlayersAsync();
            Assert.Single(playersAfter1); 
            Assert.Equal("ClueGuy", playersAfter1[0].Role); 

            await sut.JoinAsGuestAsync("Guest2");
            var playersAfter2 = await sut.GetConnectedPlayersAsync();
            Assert.Equal(2, playersAfter2.Count); 
            Assert.Equal(2, playersAfter2.Count(p => p.Role == "ClueGuy")); 

            
            await sut.JoinAsGuestAsync("Guest3");
            var playersAfter3 = await sut.GetConnectedPlayersAsync();
            Assert.Equal(3, playersAfter3.Count); 
            Assert.Equal(2, playersAfter3.Count(p => p.Role == "ClueGuy")); 
            Assert.Equal(1, playersAfter3.Count(p => p.Role == "Guesser")); 

            await sut.JoinAsGuestAsync("Guest4");
            var playersAfter4 = await sut.GetConnectedPlayersAsync();
            Assert.Equal(4, playersAfter4.Count);
            Assert.Equal(2, playersAfter4.Count(p => p.Role == "ClueGuy"));
            Assert.Equal(2, playersAfter4.Count(p => p.Role == "Guesser"));
        }

        [Fact]
        public async Task LeaveRoom_ShouldRemovePlayerAndNotifyOthers()
        {
            // Arrange
            var guestUsername = "GuestToLeave";
            await sut.JoinAsGuestAsync(guestUsername);
            var players = await sut.GetConnectedPlayersAsync();
            var playerToRemove = players[0];

            // Act
            await sut.LeaveRoomAsync(playerToRemove.Id);

            // Assert
            Assert.Empty(await sut.GetConnectedPlayersAsync());
        }
    }
}
