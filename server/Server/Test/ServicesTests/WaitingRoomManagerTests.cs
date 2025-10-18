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
        private readonly WaitingRoomManager sut; // System Under Test

        public WaitingRoomManagerTests()
        {
            mockPlayerRepo = new Mock<IPlayerRepository>();
            mockOperationContext = new Mock<IOperationContextWrapper>();
            mockCallbackChannel = new Mock<IWaitingRoomCallback>();

            // Configurar el mock del wrapper para que siempre devuelva nuestro mock del canal de callback
            mockOperationContext.Setup(o => o.GetCallbackChannel<IWaitingRoomCallback>())
                                 .Returns(mockCallbackChannel.Object);

            // Crear la instancia del servicio con los mocks
            sut = new WaitingRoomManager(mockPlayerRepo.Object, mockOperationContext.Object);
        }

        [Fact]
        public async Task JoinAsRegisteredPlayer_ShouldSucceed_WhenPlayerExistsAndIsNotConnected()
        {
            // Arrange: Preparar el escenario
            var username = "TestUser";
            var playerEntity = new Player
            {
                Id = 1,
                UserAccountId = 1,
                UserAccount = new UserAccount { Nickname = username }
            };
            mockPlayerRepo.Setup(repo => repo.GetPlayerByUsername(username)).Returns(playerEntity);

            // Act: Ejecutar la acción
            var result = await sut.JoinAsRegisteredPlayerAsync(username);

            // Assert: Verificar el resultado
            Assert.True(result);
            var connectedPlayers = await sut.GetConnectedPlayersAsync();
            Assert.Single(connectedPlayers); // Debe haber un solo jugador conectado
            Assert.Equal(username, connectedPlayers[0].Username);
        }

        [Fact]
        public async Task JoinAsRegisteredPlayer_ShouldFail_WhenPlayerDoesNotExist()
        {
            // Arrange
            var username = "GhostUser";
            mockPlayerRepo.Setup(repo => repo.GetPlayerByUsername(username)).Returns((Player)null);

            // Act
            var result = await sut.JoinAsRegisteredPlayerAsync(username);

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
            Assert.Equal(guestUsername, connectedPlayers[0].Username);
            Assert.True(connectedPlayers[0].Id < 0, "Guest ID should be negative."); // Verificar que el ID de invitado sea negativo
        }

        [Fact]
        public async Task JoinAsGuest_ShouldFail_WhenUsernameIsAlreadyTaken()
        {
            // Arrange
            var guestUsername = "Guest123";
            await sut.JoinAsGuestAsync(guestUsername); // Un primer invitado se une

            // Act
            var result = await sut.JoinAsGuestAsync(guestUsername); // El segundo intenta unirse con el mismo nombre

            // Assert
            Assert.False(result);
            Assert.Single(await sut.GetConnectedPlayersAsync()); // Solo debe haber un jugador
        }

        [Fact]
        public async Task AssignRole_ShouldAssignRolesBalancedForTwoTeams()
        {
            // Arrange
            var player1 = new Player { Id = 1, UserAccountId = 1, UserAccount = new UserAccount { Nickname = "Player1" } };
            mockPlayerRepo.Setup(r => r.GetPlayerByUsername("Player1")).Returns(player1);

            // Act & Assert

            // 1er jugador se une
            await sut.JoinAsRegisteredPlayerAsync("Player1");
            var playersAfter1 = await sut.GetConnectedPlayersAsync();
            Assert.Single(playersAfter1); // Debe haber 1 jugador
            Assert.Equal("ClueGuy", playersAfter1[0].Role); // El único jugador debe ser ClueGuy

            // 2do jugador se une
            await sut.JoinAsGuestAsync("Guest2");
            var playersAfter2 = await sut.GetConnectedPlayersAsync();
            Assert.Equal(2, playersAfter2.Count); // Deben haber 2 jugadores
            Assert.Equal(2, playersAfter2.Count(p => p.Role == "ClueGuy")); // Ambos deben ser ClueGuy

            // 3er jugador se une
            await sut.JoinAsGuestAsync("Guest3");
            var playersAfter3 = await sut.GetConnectedPlayersAsync();
            Assert.Equal(3, playersAfter3.Count); // Deben haber 3 jugadores
            Assert.Equal(2, playersAfter3.Count(p => p.Role == "ClueGuy")); // Contamos cuántos ClueGuys hay
            Assert.Equal(1, playersAfter3.Count(p => p.Role == "Guesser")); // Contamos cuántos Guessers hay

            // 4to jugador se une
            await sut.JoinAsGuestAsync("Guest4");
            var playersAfter4 = await sut.GetConnectedPlayersAsync();
            Assert.Equal(4, playersAfter4.Count); // Deben haber 4 jugadores
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
