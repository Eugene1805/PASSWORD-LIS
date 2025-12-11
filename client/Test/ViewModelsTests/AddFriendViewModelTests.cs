using Moq;
using PASSWORD_LIS_Client.FriendsManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System.Threading.Tasks;
using Xunit;

namespace Test.ViewModelsTests
{
    public class AddFriendViewModelTests
    {
        private readonly Mock<IFriendsManagerService> mockFriendsService;
        private readonly Mock<IWindowService> mockWindowService;
        private readonly AddFriendViewModel viewModel;

        public AddFriendViewModelTests()
        {
            mockFriendsService = new Mock<IFriendsManagerService>();
            mockWindowService = new Mock<IWindowService>();
            viewModel = new AddFriendViewModel(mockFriendsService.Object, mockWindowService.Object);
        }

        [Fact]
        public async Task SendRequestCommand_WhenEmailValidAndServiceReturnsSuccess_ShouldCloseWindowAndShowSuccess()
        {
            viewModel.Email = "friend@example.com";
            mockFriendsService.Setup(s => s.SendFriendRequestAsync(viewModel.Email))
                .ReturnsAsync(FriendRequestResult.Success);

            await Task.Run(() => viewModel.SendRequestCommand.Execute(null));

            mockFriendsService.Verify(s => s.SendFriendRequestAsync(viewModel.Email), Times.Once);

            mockWindowService.Verify(w => w.CloseWindow(viewModel), Times.Once);

            mockWindowService.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.requestSentTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.requestSentText,
                PopUpIcon.Success), Times.Once);
        }

        [Fact]
        public async Task SendRequestCommand_WhenUserNotFound_ShouldShowWarning()
        {
            viewModel.Email = "unknown@example.com";
            mockFriendsService.Setup(s => s.SendFriendRequestAsync(viewModel.Email))
                .ReturnsAsync(FriendRequestResult.UserNotFound);

            await Task.Run(() => viewModel.SendRequestCommand.Execute(null));

            mockWindowService.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.errorTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.playerNotFoundText,
                PopUpIcon.Warning), Times.Once);

            mockWindowService.Verify(w => w.CloseWindow(viewModel), Times.Never);
        }

        [Fact]
        public async Task SendRequestCommand_WhenAlreadyFriends_ShouldShowInformation()
        {
            viewModel.Email = "friend@example.com";
            mockFriendsService.Setup(s => s.SendFriendRequestAsync(viewModel.Email))
                .ReturnsAsync(FriendRequestResult.AlreadyFriends);

            await Task.Run(() => viewModel.SendRequestCommand.Execute(null));

            mockWindowService.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.informationText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.existingFriendshipText,
                PopUpIcon.Information), Times.Once);
        }

        [Fact]
        public async Task SendRequestCommand_WhenRequestAlreadySent_ShouldShowInformation()
        {
            viewModel.Email = "pending@example.com";
            mockFriendsService.Setup(s => s.SendFriendRequestAsync(viewModel.Email))
                .ReturnsAsync(FriendRequestResult.RequestAlreadySent);

            await Task.Run(() => viewModel.SendRequestCommand.Execute(null));

            mockWindowService.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.informationText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.existingFriendRequestText,
                PopUpIcon.Information), Times.Once);
        }

        [Fact]
        public async Task SendRequestCommand_WhenCannotAddSelf_ShouldShowInformation()
        {
            viewModel.Email = "me@example.com";
            mockFriendsService.Setup(s => s.SendFriendRequestAsync(viewModel.Email))
                .ReturnsAsync(FriendRequestResult.CannotAddSelf);

            await Task.Run(() => viewModel.SendRequestCommand.Execute(null));

            mockWindowService.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.informationText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.friendRequestToYourselfText,
                PopUpIcon.Information), Times.Once);
        }

        [Fact]
        public async Task SendRequestCommand_WhenRequestAlreadyReceived_ShouldShowInformation()
        {
            viewModel.Email = "incoming@example.com";
            mockFriendsService.Setup(s => s.SendFriendRequestAsync(viewModel.Email))
                .ReturnsAsync(FriendRequestResult.RequestAlreadyReceived);

            await Task.Run(() => viewModel.SendRequestCommand.Execute(null));

            mockWindowService.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.informationText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.friendRequestInboxText,
                PopUpIcon.Information), Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Email_WhenEmpty_ShouldSetError(string emptyValue)
        {
            viewModel.Email = emptyValue;

            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.emptyEmailText, viewModel.EmailError);
        }

        [Fact]
        public void Email_WhenTooLong_ShouldSetError()
        {
            viewModel.Email = new string('a', 101);

            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.emailTooLongText, viewModel.EmailError);
        }

        [Fact]
        public void Email_WhenInvalidFormat_ShouldSetError()
        {
            viewModel.Email = "invalid-email";

            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.invalidEmailFormatText, viewModel.EmailError);
        }

        [Fact]
        public void Email_WhenValid_ShouldClearError()
        {
            viewModel.Email = "valid@test.com";

            Assert.Null(viewModel.EmailError);
        }

        [Fact]
        public async Task SendRequestCommand_WhenValidationFails_ShouldNotCallService()
        {
            viewModel.Email = "invalid";

            await Task.Run(() => viewModel.SendRequestCommand.Execute(null));

            mockFriendsService.Verify(s => s.SendFriendRequestAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void IsSending_ShouldDisableCommand()
        {
            viewModel.IsSending = true;
            Assert.False(viewModel.SendRequestCommand.CanExecute(null));

            viewModel.IsSending = false;
            Assert.True(viewModel.SendRequestCommand.CanExecute(null));
        }
    }
}