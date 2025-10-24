using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Services;
using Services.Wrappers;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.ServicesTests
{
    public class LoginManagerTests
    {
        private readonly Mock<IAccountRepository> mockRepo;
        private readonly LoginManager loginManager;

        public LoginManagerTests()
        {
            mockRepo = new Mock<IAccountRepository>();
            loginManager = new LoginManager(mockRepo.Object);
        }
        /*
        [Fact]
        public void Login_ShouldReturnUserDTO_WhenCredentialsAreValidAndPlayerExists()
        {
            // Arrange
            var email = "test@example.com";
            var password = "ValidPassword123!";

            // Creamos un hash real que coincida con la contraseña
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

            var mockPlayer = new Player { Id = 10 };

            var mockAccount = new UserAccount
            {
                Id = 1,
                Nickname = "testuser",
                Email = email,
                FirstName = "Test",
                LastName = "User",
                PasswordHash = hashedPassword, // Usamos el hash real
                PhotoId = 1,
                Player = new List<Player> { mockPlayer }, // El usuario tiene un jugador
                SocialAccount = new List<SocialAccount>() // Evita NullReference en ToDictionary
            };

            mockRepo.Setup(repo => repo.GetUserByEmail(email)).Returns(mockAccount);

            // Act
            var result = loginManager.Login(email, password);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(mockAccount.Id, result.UserAccountId);
            Assert.Equal(mockPlayer.Id, result.PlayerId);
            Assert.Equal(mockAccount.Nickname, result.Nickname);
            mockRepo.Verify(repo => repo.GetUserByEmail(email), Times.Once);
        }
        */
        [Fact]
        public void Login_ShouldReturnNull_WhenUserNotFound()
        {
            // Arrange
            var email = "nonexistent@example.com";
            var password = "password";

            // Configuramos el repo para que devuelva null (usuario no encontrado)
            mockRepo.Setup(repo => repo.GetUserByEmail(email)).Returns((UserAccount)null);

            // Act
            var result = loginManager.Login(email, password);

            // Assert
            Assert.Null(result);
            mockRepo.Verify(repo => repo.GetUserByEmail(email), Times.Once);
        }
        /*
        [Fact]
        public void Login_ShouldReturnNull_WhenPasswordIsInvalid()
        {
            // Arrange
            var email = "test@example.com";
            var correctPassword = "ValidPassword123!";
            var wrongPassword = "WrongPassword!";

            // Creamos un hash real para la contraseña correcta
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(correctPassword);

            var mockAccount = new UserAccount
            {
                Id = 1,
                Nickname = "testuser",
                Email = email,
                PasswordHash = hashedPassword,
                Player = new List<Player> { new Player { Id = 10 } },
                SocialAccount = new List<SocialAccount>()
            };

            mockRepo.Setup(repo => repo.GetUserByEmail(email)).Returns(mockAccount);

            // Act
            // Llamamos al Login con la contraseña incorrecta
            var result = loginManager.Login(email, wrongPassword);

            // Assert
            // BCrypt.Verify devolverá false, y el método debe devolver null
            Assert.Null(result);
            mockRepo.Verify(repo => repo.GetUserByEmail(email), Times.Once);
        }
        */
        /*
        [Fact]
        public void Login_ShouldReturnNull_WhenUserHasNoPlayerAssociated()
        {
            // Arrange
            var email = "test@example.com";
            var password = "ValidPassword123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

            var mockAccount = new UserAccount
            {
                Id = 1,
                Nickname = "testuser",
                Email = email,
                PasswordHash = hashedPassword,
                Player = new List<Player>(), // <-- El usuario existe, pero no tiene jugador
                SocialAccount = new List<SocialAccount>()
            };

            mockRepo.Setup(repo => repo.GetUserByEmail(email)).Returns(mockAccount);

            // Act
            var result = loginManager.Login(email, password);

            // Assert
            // Debería fallar en userAccount.Player.Any() y devolver null
            Assert.Null(result);
            mockRepo.Verify(repo => repo.GetUserByEmail(email), Times.Once);
        }
        */
        [Fact]
        public void Login_ShouldThrowException_WhenRepositoryFails()
        {
            // Arrange
            var email = "db_error@example.com";
            var password = "password";

            // Simulamos un error de base de datos, como en tus otros ejemplos
            mockRepo.Setup(repo => repo.GetUserByEmail(email))
                    .Throws(new DbUpdateException("Error de BD simulado"));

            // Act & Assert
            // Probamos que la excepción de BD "burbujea" (ya que tu LoginManager no la maneja)
            // A diferencia de AccountManager, este servicio no lo envuelve en un FaultException.
            Assert.Throws<DbUpdateException>(
                () => loginManager.Login(email, password)
            );

            mockRepo.Verify(repo => repo.GetUserByEmail(email), Times.Once);
        }

    }
}
