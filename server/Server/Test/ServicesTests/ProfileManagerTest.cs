using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Contracts.DTOs;
using Services.Services;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Test.ServicesTests
{
    public class ProfileManagerTest
    {
        private readonly Mock<IAccountRepository> mockRepo;
        private readonly ProfileManager profileManager;

        public ProfileManagerTest()
        {
            mockRepo = new Mock<IAccountRepository>();
            profileManager = new ProfileManager(mockRepo.Object);
        }

        [Fact]
        public void UpdateProfile_ShouldReturnUpdatedDTO_WhenRepositorySucceeds()
        {
            // Arrange
            var inputDto = new UserDTO
            {
                PlayerId = 1,
                FirstName = "UpdatedFName",
                LastName = "UpdatedLName",
                PhotoId = 2,
                SocialAccounts = new Dictionary<string, string> { { "X", "updatedX" } }
            };

            mockRepo.Setup(repo => repo.UpdateUserProfile(
                It.Is<int>(id => id == inputDto.PlayerId), 
                It.IsAny<UserAccount>(),                   
                It.IsAny<List<SocialAccount>>()            
            )).Returns(true);

            // Act
            var resultDto = profileManager.UpdateProfile(inputDto);

            // Assert
            Assert.NotNull(resultDto); 
            Assert.Same(inputDto, resultDto); 
            mockRepo.Verify(repo => repo.UpdateUserProfile(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Once); 
        }

        [Fact]
        public void UpdateProfile_ShouldReturnNull_WhenRepositoryReturnsFalse()
        {
            // Arrange
            var inputDto = new UserDTO { PlayerId = 99, FirstName = "FName", LastName = "LName" };

            mockRepo.Setup(repo => repo.UpdateUserProfile(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()))
                    .Returns(false); 

            // Act
            var resultDto = profileManager.UpdateProfile(inputDto);

            // Assert
            Assert.Null(resultDto);
            mockRepo.Verify(repo => repo.UpdateUserProfile(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Once); 
        }

        [Fact]
        public void UpdateProfile_ShouldThrowFaultExceptionWithDatabaseError_WhenRepositoryThrowsDbUpdateException()
        {
            // Arrange
            var inputDto = new UserDTO { PlayerId = 1, FirstName = "FName", LastName = "LName" };
            var dbUpdateEx = new DbUpdateException("Simulated SaveChanges error");

            mockRepo.Setup(repo => repo.UpdateUserProfile(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()))
                    .Throws(dbUpdateEx);

            // Act & Assert
            var faultEx = Assert.Throws<FaultException<ServiceErrorDetailDTO>>(
                () => profileManager.UpdateProfile(inputDto)
            );

            Assert.Equal("DATABASE_ERROR", faultEx.Detail.ErrorCode);
            Assert.Contains("guardar los cambios", faultEx.Detail.Message); 
            mockRepo.Verify(repo => repo.UpdateUserProfile(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Once); 
        }

       
        public class MockDbException : DbException { public MockDbException(string message) : base(message) { } }

        [Fact]
        public void UpdateProfile_ShouldThrowFaultExceptionWithDatabaseError_WhenRepositoryThrowsDbException()
        {
            // Arrange
            var inputDto = new UserDTO { PlayerId = 1, FirstName = "FName", LastName = "LName" };
            var dbEx = new MockDbException("Simulated connection error");

            mockRepo.Setup(repo => repo.UpdateUserProfile(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()))
                    .Throws(dbEx); 

            // Act & Assert
            var faultEx = Assert.Throws<FaultException<ServiceErrorDetailDTO>>(
                () => profileManager.UpdateProfile(inputDto)
            );

            Assert.Equal("DATABASE_ERROR", faultEx.Detail.ErrorCode);
            Assert.Contains("comunicación con la base de datos", faultEx.Detail.Message); 
            mockRepo.Verify(repo => repo.UpdateUserProfile(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Once); 
        }

        [Fact]
        public void UpdateProfile_ShouldThrowFaultExceptionWithUnexpectedError_WhenRepositoryThrowsGeneralException()
        {
            // Arrange
            var inputDto = new UserDTO { PlayerId = 1, FirstName = "FName", LastName = "LName" };
            var generalEx = new Exception("Simulated unexpected error");

            mockRepo.Setup(repo => repo.UpdateUserProfile(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()))
                    .Throws(generalEx); // Simula error inesperado

            // Act & Assert
            var faultEx = Assert.Throws<FaultException<ServiceErrorDetailDTO>>(
                () => profileManager.UpdateProfile(inputDto)
            );

            Assert.Equal("UNEXPECTED_ERROR", faultEx.Detail.ErrorCode);
            Assert.Contains("error inesperado", faultEx.Detail.Message); 
            mockRepo.Verify(repo => repo.UpdateUserProfile(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Once); 
        }

        [Fact]
        public void UpdateProfile_ShouldReturnNull_WhenInputDtoIsNull()
        {
            // Arrange
            UserDTO inputDto = null;

            // Act
            var resultDto = profileManager.UpdateProfile(inputDto);

            // Assert
            Assert.Null(resultDto);
            mockRepo.Verify(repo => repo.UpdateUserProfile(It.IsAny<int>(), It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Never);
        }

        [Theory] 
        [InlineData(0)]
        [InlineData(-1)]
        public void UpdateProfile_ShouldReturnNull_WhenPlayerIdIsInvalid(int invalidPlayerId)
        {
            // Arrange
            var inputDto = new UserDTO { PlayerId = invalidPlayerId, FirstName = "FName", LastName = "LName" };

            // Act
            var resultDto = profileManager.UpdateProfile(inputDto);

            // Assert
            Assert.Null(resultDto);
            mockRepo.Verify(repo => repo.UpdateUserProfile(It.IsAny<int>(), It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Never);
        }

        [Fact]
        public void UpdateProfile_ShouldCallRepositoryWithCorrectlyMappedData_WhenInputIsValid()
        {
            // Arrange
            var inputDto = new UserDTO
            {
                PlayerId = 5,
                FirstName = " Test ",
                LastName = " User ",
                PhotoId = 3,
                SocialAccounts = new Dictionary<string, string> {
                    { "Facebook", "fbUser " },
                    { "Instagram", "" },      
                    { "X", null },             
                    { "TikTok", " tiktokUser" } 
                }
            };

            UserAccount capturedAccount = null;
            List<SocialAccount> capturedSocials = null;

            mockRepo.Setup(repo => repo.UpdateUserProfile(
                inputDto.PlayerId,
                It.IsAny<UserAccount>(),
                It.IsAny<List<SocialAccount>>()
            ))
            .Callback<int, UserAccount, List<SocialAccount>>((id, ua, sa) => {
                capturedAccount = ua; 
                capturedSocials = sa; 
            })
            .Returns(true);

            // Act
            profileManager.UpdateProfile(inputDto);

            // Assert
            Assert.NotNull(capturedAccount);
            Assert.Equal(inputDto.FirstName, capturedAccount.FirstName); 
            Assert.Equal(inputDto.LastName, capturedAccount.LastName);   
            Assert.Equal((byte?)inputDto.PhotoId, capturedAccount.PhotoId); 

            Assert.NotNull(capturedSocials);
            Assert.Equal(4, capturedSocials.Count); 
            Assert.Contains(capturedSocials, s => s.Provider == "Facebook" && s.Username == "fbUser ");
            Assert.Contains(capturedSocials, s => s.Provider == "Instagram" && s.Username == "");
            Assert.Contains(capturedSocials, s => s.Provider == "X" && s.Username == null);
            Assert.Contains(capturedSocials, s => s.Provider == "TikTok" && s.Username == " tiktokUser");
        }
    }
}
