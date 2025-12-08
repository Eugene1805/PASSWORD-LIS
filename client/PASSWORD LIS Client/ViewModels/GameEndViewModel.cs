using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Utils;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class GameEndViewModel : BaseViewModel
    {
        private string redTeamScore;
        public string RedTeamScore
        {
            get => redTeamScore;
            set => SetProperty(ref redTeamScore, value);
        }
        private string blueTeamScore;
        public string BlueTeamScore
        {
            get => blueTeamScore;
            set => SetProperty(ref blueTeamScore, value);
        }

        public ICommand BackToLobbyCommand { get; }

        public GameEndViewModel(int redScore, int blueScore, IWindowService windowService)
            : base(windowService)
        {
            RedTeamScore = string.Format(Properties.Langs.Lang.redTeamText + ": {0}", redScore);
            BlueTeamScore = string.Format(Properties.Langs.Lang.blueTeamText + ": {0}", blueScore);

            BackToLobbyCommand = new RelayCommand(BackToLobby);
        }

        private void BackToLobby(object obj)
        {
            windowService.GoToLobby();
        }
    }
}
