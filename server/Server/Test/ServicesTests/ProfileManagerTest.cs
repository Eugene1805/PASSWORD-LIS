using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;

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
        public async Task UpdateProfile_ShouldReturnUpdatedDTO_WhenRepositorySucceeds()
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

            mockRepo.Setup(repo => repo.UpdateUserProfileAsync(
                It.Is<int>(id => id == inputDto.PlayerId),
                It.IsAny<UserAccount>(),
                It.IsAny<List<SocialAccount>>()
            )).ReturnsAsync(true);

            // Act
            var resultDto = await profileManager.UpdateProfileAsync(inputDto);

            // Assert
            Assert.NotNull(resultDto);
            Assert.Same(inputDto, resultDto);
            mockRepo.Verify(repo => repo.UpdateUserProfileAsync(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Once);
        }

        [Fact]
        public async Task UpdateProfile_ShouldReturnNull_WhenRepositoryReturnsFalse()
        {
            // Arrange
            var inputDto = new UserDTO { PlayerId = 99, FirstName = "FName", LastName = "LName" };

            mockRepo.Setup(repo => repo.UpdateUserProfileAsync(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()))
                    .ReturnsAsync(false);

            // Act
            var resultDto = await profileManager.UpdateProfileAsync(inputDto);

            // Assert
            Assert.Null(resultDto);
            mockRepo.Verify(repo => repo.UpdateUserProfileAsync(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Once);
        }

        [Fact]
        public async Task UpdateProfile_ShouldThrowFaultExceptionWithDatabaseError_WhenRepositoryThrowsDbUpdateException()
        {
            // Arrange
            var inputDto = new UserDTO { PlayerId = 1, FirstName = "FName", LastName = "LName" };
            var dbUpdateEx = new DbUpdateException("Simulated SaveChanges error", new Exception());

            mockRepo.Setup(repo => repo.UpdateUserProfileAsync(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()))
                    .ThrowsAsync(dbUpdateEx);

            // Act & Assert
            var faultEx = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => profileManager.UpdateProfileAsync(inputDto)
            );

            Assert.Equal(ServiceErrorCode.DatabaseError, faultEx.Detail.Code);
            Assert.Equal("DATABASE_ERROR", faultEx.Detail.ErrorCode);
            mockRepo.Verify(repo => repo.UpdateUserProfileAsync(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Once);
        }

        [Fact]
        public async Task UpdateProfile_ShouldThrowFaultExceptionWithDatabaseError_WhenRepositoryThrowsDbException()
        {
            // Arrange
            var inputDto = new UserDTO { PlayerId = 1, FirstName = "FName", LastName = "LName" };
            var dbEx = new DbUpdateException("Simulated connection error", new Exception());

            mockRepo.Setup(repo => repo.UpdateUserProfileAsync(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()))
                    .ThrowsAsync(dbEx);

            // Act & Assert
            var faultEx = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => profileManager.UpdateProfileAsync(inputDto)
            );

            Assert.Equal(ServiceErrorCode.DatabaseError, faultEx.Detail.Code);
            Assert.Equal("DATABASE_ERROR", faultEx.Detail.ErrorCode);
            mockRepo.Verify(repo => repo.UpdateUserProfileAsync(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Once);
        }

        [Fact]
        public async Task UpdateProfile_ShouldThrowFaultExceptionWithUnexpectedError_WhenRepositoryThrowsGeneralException()
        {
            // Arrange
            var inputDto = new UserDTO { PlayerId = 1, FirstName = "FName", LastName = "LName" };
            var generalEx = new Exception("Simulated unexpected error");

            mockRepo.Setup(repo => repo.UpdateUserProfileAsync(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()))
                    .ThrowsAsync(generalEx);

            // Act & Assert
            var faultEx = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => profileManager.UpdateProfileAsync(inputDto)
            );

            Assert.Equal(ServiceErrorCode.UnexpectedError, faultEx.Detail.Code);
            Assert.Equal("UNEXPECTED_ERROR", faultEx.Detail.ErrorCode);
            mockRepo.Verify(repo => repo.UpdateUserProfileAsync(inputDto.PlayerId, It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Once);
        }

        [Fact]
        public async Task UpdateProfile_ShouldReturnNull_WhenInputDtoIsNull()
        {
            UserDTO? inputDto = null;
            var result = await profileManager.UpdateProfileAsync(inputDto);
            Assert.Null(result);

            mockRepo.Verify(repo => repo.UpdateUserProfileAsync(It.IsAny<int>(), It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Never);
        }

        [Theory] 
        [InlineData(0)]
        [InlineData(-1)]
        public async Task UpdateProfile_ShouldReturnNull_WhenPlayerIdIsInvalid(int invalidPlayerId)
        {
            // Arrange
            var inputDto = new UserDTO { PlayerId = invalidPlayerId, FirstName = "FName", LastName = "LName" };

            // Act
            var resultDto = await profileManager.UpdateProfileAsync(inputDto);

            // Assert
            Assert.Null(resultDto);
            mockRepo.Verify(repo => repo.UpdateUserProfileAsync(It.IsAny<int>(), It.IsAny<UserAccount>(), It.IsAny<List<SocialAccount>>()), Times.Never);
        }

        [Fact]
        public async Task UpdateProfile_ShouldCallRepositoryWithCorrectlyMappedData_WhenInputIsValid()
        {
            // Arrange
            var inputDto = new UserDTO
            {
                PlayerId = 5,
                FirstName = " Test ",
                LastName = " User ",
                PhotoId = 3,
                SocialAccounts = new Dictionary<string, string?> {
                    { "Facebook", "fbUser " },
                    { "Instagram", "" },
                    { "X", null },
                    { "TikTok", " tiktokUser" }
                }
            };

            UserAccount? capturedAccount = null;
            List<SocialAccount>? capturedSocials = null;

            mockRepo.Setup(repo => repo.UpdateUserProfileAsync(
                inputDto.PlayerId,
                It.IsAny<UserAccount>(),
                It.IsAny<List<SocialAccount>>()
            ))
            .Callback<int, UserAccount, List<SocialAccount>>((id, ua, sa) => {
                capturedAccount = ua;
                capturedSocials = sa;
            })
            .ReturnsAsync(true);

            // Act
            await profileManager.UpdateProfileAsync(inputDto);

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
