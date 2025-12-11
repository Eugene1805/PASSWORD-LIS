using Moq;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ProfileUserDTO = PASSWORD_LIS_Client.ProfileManagerServiceReference.UserDTO;
using SessionUserDTO = PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO;

namespace Test.ViewModelsTests
{
    public class ProfileViewModelTests
    {
        private readonly Mock<IProfileManagerService> mockProfileService;
        private readonly Mock<IWindowService> mockWindowService;
        private ProfileViewModel viewModel;

        public ProfileViewModelTests()
        {
            mockProfileService = new Mock<IProfileManagerService>();
            mockWindowService = new Mock<IWindowService>();

            var initialUser = new SessionUserDTO
            {
                UserAccountId = 1,
                PlayerId = 10,
                Email = "test@example.com",
                Nickname = "TestNick",
                FirstName = "OriginalFirst",
                LastName = "OriginalLast",
                PhotoId = 1,
                SocialAccounts = new Dictionary<string, string>
                {
                    { "Facebook", "fb_user" }
                }
            };
            SessionManager.Login(initialUser);

            viewModel = new ProfileViewModel(mockProfileService.Object, mockWindowService.Object);
        }

        [Fact]
        public void Constructor_ShouldLoadDataFromSession()
        {
            Assert.Equal("TestNick", viewModel.Nickname);
            Assert.Equal("OriginalFirst", viewModel.FirstName);
            Assert.Equal("OriginalLast", viewModel.LastName);
            Assert.Equal(1, viewModel.PhotoId);
            Assert.Equal("fb_user", viewModel.Facebook);
        }

        [Fact]
        public void EditProfileCommand_ShouldEnableEditModeAndClearErrors()
        {
            viewModel.FirstNameError = "Some error";
            viewModel.IsEditMode = false;

            viewModel.EditProfileCommand.Execute(null);

            Assert.True(viewModel.IsEditMode);
            Assert.Null(viewModel.FirstNameError);
            mockWindowService.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.editingModeTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.editingModeActiveText,
                PopUpIcon.Information), Times.Once);
        }

        [Theory]
        [InlineData("", "emptyFirstNameText")]
        [InlineData("NameWithNumbers123", "nameInvalidCharsText")]
        [InlineData("ThisNameIsWayTooLongAndShouldTriggerTheMaxLengthErrorCheckInTheViewModelLogic", "firstNameTooLongText")]
        public void FirstName_WhenInvalid_ShouldSetError(string invalidName, string resourceKey)
        {
            viewModel.FirstName = invalidName;

            var expectedError = typeof(PASSWORD_LIS_Client.Properties.Langs.Lang)
                .GetProperty(resourceKey)
                .GetValue(null) as string;

            Assert.Equal(expectedError, viewModel.FirstNameError);
        }

        [Fact]
        public void FirstName_WhenValid_ShouldClearError()
        {
            viewModel.FirstName = "ValidName";
            Assert.Null(viewModel.FirstNameError);
        }

        [Theory]
        [InlineData("", "emptyLastNameText")]
        [InlineData("Last123", "lastNameInvalidCharsText")]
        public void LastName_WhenInvalid_ShouldSetError(string invalidLast, string resourceKey)
        {
            viewModel.LastName = invalidLast;

            var expectedError = typeof(PASSWORD_LIS_Client.Properties.Langs.Lang)
                .GetProperty(resourceKey)
                .GetValue(null) as string;

            Assert.Equal(expectedError, viewModel.LastNameError);
        }

        [Fact]
        public void SocialMedia_WhenTooLong_ShouldSetError()
        {
            var longString = new string('a', 51);
            viewModel.Facebook = longString;

            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.usernameNotExceedFiftyCharacteresText, viewModel.FacebookError);
        }

        [Fact]
        public void CanSaveChanges_WhenNoChanges_ShouldReturnFalse()
        {
            viewModel.IsEditMode = true;

            var canExecute = viewModel.SaveChangesCommand.CanExecute(null);

            Assert.False(canExecute);
        }

        [Fact]
        public void CanSaveChanges_WhenChangesMadeAndValid_ShouldReturnTrue()
        {
            viewModel.IsEditMode = true;
            viewModel.FirstName = "NewName"; 

            var canExecute = viewModel.SaveChangesCommand.CanExecute(null);

            Assert.True(canExecute);
        }

        [Fact]
        public void CanSaveChanges_WhenChangesMadeButInvalid_ShouldReturnFalse()
        {
            viewModel.IsEditMode = true;
            viewModel.FirstName = ""; 

            var canExecute = viewModel.SaveChangesCommand.CanExecute(null);

            Assert.False(canExecute);
        }

        [Fact]
        public async Task SaveChangesAsync_WhenServiceSucceeds_ShouldUpdateSessionAndShowSuccess()
        {
            viewModel.IsEditMode = true;
            viewModel.FirstName = "UpdatedName";

            var returnedDto = new ProfileUserDTO
            {
                PlayerId = 10,
                Nickname = "TestNick",
                FirstName = "UpdatedName",
                LastName = "OriginalLast",
                Email = "test@example.com",
                PhotoId = 1,
                SocialAccounts = new Dictionary<string, string>()
            };

            mockProfileService.Setup(s => s.UpdateProfileAsync(It.IsAny<ProfileUserDTO>()))
                .ReturnsAsync(returnedDto);

            await Task.Run(() => viewModel.SaveChangesCommand.Execute(null));

            mockProfileService.Verify(s => s.UpdateProfileAsync(It.IsAny<ProfileUserDTO>()), Times.Once);

            Assert.Equal("UpdatedName", SessionManager.CurrentUser.FirstName);

            mockWindowService.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.profileUpdatedTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.profileChangesSavedSuccessText,
                PopUpIcon.Success), Times.Once);

            Assert.False(viewModel.IsEditMode);
        }

        [Fact]
        public async Task SaveChangesAsync_WhenServiceReturnsNull_ShouldShowErrorAndStayInEditMode()
        {
            viewModel.IsEditMode = true;
            viewModel.FirstName = "UpdatedName";

            mockProfileService.Setup(s => s.UpdateProfileAsync(It.IsAny<ProfileUserDTO>()))
                .ReturnsAsync((ProfileUserDTO)null);

            await Task.Run(() => viewModel.SaveChangesCommand.Execute(null));

            mockWindowService.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.errorTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.changesSavedErrorText,
                PopUpIcon.Error), Times.Once);

            Assert.True(viewModel.IsEditMode);
        }

        [Fact]
        public void BackToLobby_WhenNotEditing_ShouldNavigateDirectly()
        {
            viewModel.IsEditMode = false;

            viewModel.BackToLobbyCommand.Execute(null);

            mockWindowService.Verify(w => w.GoToLobby(), Times.Once);
            mockWindowService.Verify(w => w.ShowYesNoPopUp(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void BackToLobby_WhenEditingWithChanges_AndUserConfirmsDiscard_ShouldRevertAndNavigate()
        {
            viewModel.IsEditMode = true;
            viewModel.FirstName = "ChangedName"; 

            mockWindowService.Setup(w => w.ShowYesNoPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.unsavedChangesWarningTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.unsavedChangesWarningText))
                .Returns(true);

            viewModel.BackToLobbyCommand.Execute(null);

            Assert.Equal("OriginalFirst", viewModel.FirstName);
            Assert.False(viewModel.IsEditMode);
            mockWindowService.Verify(w => w.GoToLobby(), Times.Once);
        }

        [Fact]
        public void BackToLobby_WhenEditingWithChanges_AndUserCancelsDiscard_ShouldStayOnPage()
        {
            viewModel.IsEditMode = true;
            viewModel.FirstName = "ChangedName";

            mockWindowService.Setup(w => w.ShowYesNoPopUp(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);

            viewModel.BackToLobbyCommand.Execute(null);

            Assert.Equal("ChangedName", viewModel.FirstName); // No revirtió
            Assert.True(viewModel.IsEditMode); // Sigue editando
            mockWindowService.Verify(w => w.GoToLobby(), Times.Never);
        }

        [Fact]
        public void BackToLobby_WhenEditingButNoChanges_ShouldNavigateWithoutPrompt()
        {
            viewModel.IsEditMode = true;
           
            viewModel.BackToLobbyCommand.Execute(null);

            mockWindowService.Verify(w => w.ShowYesNoPopUp(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            mockWindowService.Verify(w => w.GoToLobby(), Times.Once);
            Assert.False(viewModel.IsEditMode);
        }
    }
}