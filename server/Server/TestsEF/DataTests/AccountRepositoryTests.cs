using Data.DAL.Implementations;
using Data.DAL.Interfaces;
using Data.Exceptions;
using Data.Model;
using Effort; // Asegúrate de tener 'using Effort;'
using Moq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity; // Para .Include()
using System.Data.Entity.Validation; // Para el catch (opcional)
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace TestEF.DataTests
{
    public class AccountRepositoryTests : IDisposable
    {
        private readonly Mock<IDbContextFactory> _mockFactory;
        private readonly PasswordLISEntities _context;
        private readonly AccountRepository _repository;
        private readonly DbConnection _connection;

        // --- MÉTODO AYUDANTE NUEVO ---
        /// <summary>
        /// Crea una entidad UserAccount válida con todos los campos obligatorios rellenos.
        /// </summary>
        private UserAccount CreateValidUser(string email = "test@test.com", string nickname = "TestUser")
        {
            return new UserAccount
            {
                // Campos que cambian por test
                Email = email,
                Nickname = nickname,

                // Campos obligatorios (causa de los errores)
                PasswordHash = "dummy-hash-123456",
                CreatedAt = DateTime.UtcNow, // Usamos UtcNow por estándar
                IsActive = true,
                EmailVerified = false,

                // Campos opcionales (podemos dejarlos en null o con valor)
                FirstName = "Test",
                LastName = "User",
                PhotoId = null,
                LastLoginAt = null
            };
        }

        // --- CONFIGURACIÓN ---
        public AccountRepositoryTests()
        {
            _connection = EntityConnectionFactory.CreateTransient("name=PasswordLISEntities");
            _context = new PasswordLISEntities(_connection, true);
            _mockFactory = new Mock<IDbContextFactory>();
            _mockFactory.Setup(f => f.CreateDbContext()).Returns(_context);
            _repository = new AccountRepository(_mockFactory.Object);
        }

        // --- LIMPIEZA ---
        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose(); // --- CORREGIDO --- (Era _entityConnection)
        }

        // --- TESTS CORREGIDOS ---

        [Fact]
        public async Task CreateAccountAsync_WhenAccountIsNew_ShouldSaveAccountAndPlayer()
        {
            // Arrange (Preparar)
            // --- CORREGIDO ---
            // Usamos el helper para crear un usuario completo y válido
            var newAccount = CreateValidUser("new@test.com", "Newbie");

            // Act (Actuar)
            await _repository.CreateAccountAsync(newAccount);

            // Assert (Afirmar)
            Assert.Equal(1, _context.UserAccount.Count());
            Assert.Equal(1, _context.Player.Count());

            var playerInDb = _context.Player.Include(p => p.UserAccount).First(); // Es mejor usar lambda
            Assert.Equal("new@test.com", playerInDb.UserAccount.Email);
        }

        [Fact]
        public async Task CreateAccountAsync_WhenEmailExists_ShouldThrowDuplicateAccountException()
        {
            // Arrange (Preparar)
            // --- CORREGIDO ---
            // Sembramos un usuario válido
            var existingAccount = CreateValidUser("existing@test.com", "OldUser");
            _context.UserAccount.Add(existingAccount);
            await _context.SaveChangesAsync(); // <-- Esto ahora funcionará

            // Preparamos el nuevo usuario duplicado
            var newAccount = CreateValidUser("existing@test.com", "Newbie");

            // Act & Assert (Actuar y Afirmar)
            var exception = await Assert.ThrowsAsync<DuplicateAccountException>(() =>
                _repository.CreateAccountAsync(newAccount)
            );

            Assert.Equal("An account with the email 'existing@test.com' already exists.", exception.Message);
        }

        [Fact]
        public void AccountAlreadyExist_WhenEmailExists_ReturnsTrue()
        {
            // Arrange
            // --- CORREGIDO ---
            var existingAccount = CreateValidUser("existing@test.com", "TestUser");
            _context.UserAccount.Add(existingAccount);
            _context.SaveChanges(); // <-- Esto ahora funcionará

            // Act
            var result = _repository.AccountAlreadyExist("existing@test.com");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void AccountAlreadyExist_WhenEmailDoesNotExist_ReturnsFalse()
        {
            // Arrange (BD vacía) - Este test ya funcionaba

            // Act
            var result = _repository.AccountAlreadyExist("non-existing@test.com");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetUserByEmail_WhenUserExists_ReturnsUserWithIncludes()
        {
            // Arrange: Sembramos datos más complejos
            // --- CORREGIDO ---
            var user = CreateValidUser("user@test.com", "TestUser");
            _context.UserAccount.Add(user);
            _context.SaveChanges(); // <-- Esto ahora funcionará. 'user.Id' se poblará.

            // El resto de tu 'Arrange' estaba bien
            var player = new Player { Id = 10, UserAccountId = user.Id };
            var social = new SocialAccount { Provider = "GitHub", Username = "TestUser", UserAccountId = user.Id };
            _context.Player.Add(player);
            _context.SocialAccount.Add(social);
            _context.SaveChanges();

            // Act
            var result = _repository.GetUserByEmail("user@test.com");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("user@test.com", result.Email);
            Assert.NotNull(result.Player);
            Assert.True(result.Player.Any(p => p.Id == player.Id));
            Assert.NotNull(result.SocialAccount);
            Assert.Equal(1, result.SocialAccount.Count);
            Assert.Equal("GitHub", result.SocialAccount.First().Provider);
        }

        [Fact]
        public void GetUserByEmail_WhenUserDoesNotExist_ReturnsNull()
        {
            // Arrange (BD vacía) - Este test ya funcionaba

            // Act
            var result = _repository.GetUserByEmail("nouser@test.com");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void VerifyEmail_WhenUserExists_UpdatesEmailVerifiedAndReturnsTrue()
        {
            // Arrange
            // --- CORREGIDO ---
            var user = CreateValidUser("verify@test.com", "VerifyUser");
            user.EmailVerified = false; // Nos aseguramos de que empiece en false
            _context.UserAccount.Add(user);
            _context.SaveChanges(); // <-- Esto ahora funcionará

            // Act
            var result = _repository.VerifyEmail("verify@test.com");

            // Assert
            Assert.True(result);
            var userInDb = _context.UserAccount.First(u => u.Email == "verify@test.com");
            Assert.True(userInDb.EmailVerified);
        }

        [Fact]
        public void VerifyEmail_WhenUserDoesNotExist_ReturnsFalse()
        {
            // Arrange (BD vacía) - Este test ya funcionaba

            // Act
            var result = _repository.VerifyEmail("nouser@test.com");

            // Assert
            Assert.False(result);
            Assert.Equal(0, _context.UserAccount.Count());
        }

        [Fact]
        public void UpdateUserProfile_WhenUserExists_UpdatesDataAndManagesSocials()
        {
            // Arrange (Seed): Preparamos un usuario completo
            // --- CORREGIDO ---
            var user = CreateValidUser("user@test.com", "OldUser");
            user.FirstName = "OldFirst"; // Sobrescribimos los defaults
            user.LastName = "OldLast";
            user.SocialAccount = new List<SocialAccount>
            {
                new SocialAccount { Provider = "GitHub", Username = "OldGitHub" },
                new SocialAccount { Provider = "Twitter", Username = "OldTwitter" }
            };

            var player = new Player { Id = 10, UserAccount = user };
            _context.Player.Add(player);
            // No es necesario añadir 'user' por separado, EF lo hará al añadir 'player'
            _context.SaveChanges(); // <-- Esto ahora funcionará

            // Capturamos el Id real generado por la BD en memoria (puede ignorar el 10 explícito)
            var savedPlayerId = player.Id;

            // Arrange (Input): Preparamos los datos nuevos
            var updatedUserData = new UserAccount
            {
                FirstName = "NewFirst",
                LastName = "NewLast",
                PhotoId = (byte)123 // --- CORREGIDO --- (123 es int, PhotoId es byte?)
            };
            var updatedSocials = new List<SocialAccount>
            {
                new SocialAccount { Provider = "GitHub", Username = "NewGitHub" },
                new SocialAccount { Provider = "LinkedIn", Username = "NewLinkedIn" }
            };

            // Act
            var result = _repository.UpdateUserProfile(savedPlayerId, updatedUserData, updatedSocials);

            // Assert
            Assert.True(result);

            // Verificamos los cambios en la BD en memoria
            var userInDb = _context.UserAccount.Include(u => u.SocialAccount).First(u => u.Id == user.Id); // Usamos user.Id

            Assert.Equal("NewFirst", userInDb.FirstName);
            Assert.Equal("NewLast", userInDb.LastName);
            Assert.Equal((byte?)123, userInDb.PhotoId); // El Assert estaba bien
            Assert.Equal(2, userInDb.SocialAccount.Count);
            Assert.True(userInDb.SocialAccount.Any(s => s.Provider == "LinkedIn" && s.Username == "NewLinkedIn"));
            Assert.True(userInDb.SocialAccount.Any(s => s.Provider == "GitHub" && s.Username == "NewGitHub"));
            Assert.False(userInDb.SocialAccount.Any(s => s.Provider == "Twitter"));
        }
    }
}