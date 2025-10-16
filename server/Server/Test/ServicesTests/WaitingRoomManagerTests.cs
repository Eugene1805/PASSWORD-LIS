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
        private readonly Mock<IPlayerRepository> _mockPlayerRepo;
        private readonly Mock<IOperationContextWrapper> _mockOperationContext;
        private readonly Mock<IWaitingRoomCallback> _mockCallbackChannel;
        private readonly WaitingRoomManager _sut; // System Under Test

        public WaitingRoomManagerTests()
        {
            _mockPlayerRepo = new Mock<IPlayerRepository>();
            _mockOperationContext = new Mock<IOperationContextWrapper>();
            _mockCallbackChannel = new Mock<IWaitingRoomCallback>();

            // Configurar el mock del wrapper para que siempre devuelva nuestro mock del canal de callback
            _mockOperationContext.Setup(o => o.GetCallbackChannel<IWaitingRoomCallback>())
                                 .Returns(_mockCallbackChannel.Object);

            // Crear la instancia del servicio con los mocks
            _sut = new WaitingRoomManager(_mockPlayerRepo.Object, _mockOperationContext.Object);
        }

        [Fact]
        public void JoinAsRegisteredPlayer_ShouldSucceed_WhenPlayerExistsAndIsNotConnected()
        {
            // Arrange: Preparar el escenario
            var username = "TestUser";
            var playerEntity = new Player
            {
                Id = 1,
                UserAccountId = 1,
                UserAccount = new UserAccount { Nickname = username }
            };
            _mockPlayerRepo.Setup(repo => repo.GetPlayerByUsername(username)).Returns(playerEntity);

            // Act: Ejecutar la acción
            var result = _sut.JoinAsRegisteredPlayer(username);

            // Assert: Verificar el resultado
            Assert.True(result);
            var connectedPlayers = _sut.GetConnectedPlayers();
            Assert.Single(connectedPlayers); // Debe haber un solo jugador conectado
            Assert.Equal(username, connectedPlayers[0].Username);
        }

        [Fact]
        public void JoinAsRegisteredPlayer_ShouldFail_WhenPlayerDoesNotExist()
        {
            // Arrange
            var username = "GhostUser";
            _mockPlayerRepo.Setup(repo => repo.GetPlayerByUsername(username)).Returns((Player)null);

            // Act
            var result = _sut.JoinAsRegisteredPlayer(username);

            // Assert
            Assert.False(result);
            Assert.Empty(_sut.GetConnectedPlayers());
        }

        [Fact]
        public void JoinAsGuest_ShouldSucceed_WhenUsernameIsUnique()
        {
            // Arrange
            var guestUsername = "Guest123";

            // Act
            var result = _sut.JoinAsGuest(guestUsername);

            // Assert
            Assert.True(result);
            var connectedPlayers = _sut.GetConnectedPlayers();
            Assert.Single(connectedPlayers);
            Assert.Equal(guestUsername, connectedPlayers[0].Username);
            Assert.True(connectedPlayers[0].Id < 0, "Guest ID should be negative."); // Verificar que el ID de invitado sea negativo
        }

        [Fact]
        public void JoinAsGuest_ShouldFail_WhenUsernameIsAlreadyTaken()
        {
            // Arrange
            var guestUsername = "Guest123";
            _sut.JoinAsGuest(guestUsername); // Un primer invitado se une

            // Act
            var result = _sut.JoinAsGuest(guestUsername); // El segundo intenta unirse con el mismo nombre

            // Assert
            Assert.False(result);
            Assert.Single(_sut.GetConnectedPlayers()); // Solo debe haber un jugador
        }

        [Fact]
        public void AssignRole_ShouldAssignRolesBalancedForTwoTeams()
        {
            // Arrange
            var player1 = new Player { Id = 1, UserAccountId = 1, UserAccount = new UserAccount { Nickname = "Player1" } };
            _mockPlayerRepo.Setup(r => r.GetPlayerByUsername("Player1")).Returns(player1);

            // Act & Assert

            // 1er jugador se une
            _sut.JoinAsRegisteredPlayer("Player1");
            var playersAfter1 = _sut.GetConnectedPlayers();
            Assert.Single(playersAfter1); // Debe haber 1 jugador
            Assert.Equal("ClueGuy", playersAfter1.First().Role); // El único jugador debe ser ClueGuy

            // 2do jugador se une
            _sut.JoinAsGuest("Guest2");
            var playersAfter2 = _sut.GetConnectedPlayers();
            Assert.Equal(2, playersAfter2.Count); // Deben haber 2 jugadores
            Assert.Equal(2, playersAfter2.Count(p => p.Role == "ClueGuy")); // Ambos deben ser ClueGuy

            // 3er jugador se une
            _sut.JoinAsGuest("Guest3");
            var playersAfter3 = _sut.GetConnectedPlayers();
            Assert.Equal(3, playersAfter3.Count); // Deben haber 3 jugadores
            Assert.Equal(2, playersAfter3.Count(p => p.Role == "ClueGuy")); // Contamos cuántos ClueGuys hay
            Assert.Equal(1, playersAfter3.Count(p => p.Role == "Guesser")); // Contamos cuántos Guessers hay

            // 4to jugador se une
            _sut.JoinAsGuest("Guest4");
            var playersAfter4 = _sut.GetConnectedPlayers();
            Assert.Equal(4, playersAfter4.Count); // Deben haber 4 jugadores
            Assert.Equal(2, playersAfter4.Count(p => p.Role == "ClueGuy"));
            Assert.Equal(2, playersAfter4.Count(p => p.Role == "Guesser"));
        }

        [Fact]
        public void LeaveRoom_ShouldRemovePlayerAndNotifyOthers()
        {
            // Arrange
            var guestUsername = "GuestToLeave";
            _sut.JoinAsGuest(guestUsername);
            var playerToRemove = _sut.GetConnectedPlayers()[0];

            // Act
            _sut.LeaveRoom(playerToRemove.Id);

            // Assert
            Assert.Empty(_sut.GetConnectedPlayers());

            // Opcional: Verificar que el callback fue invocado para notificar a otros
            // Nota: Esto solo funciona si hay otros jugadores.
            // _mockCallbackChannel.Verify(c => c.OnPlayerLeft(playerToRemove.Id), Times.Once);
        }
    }
}
