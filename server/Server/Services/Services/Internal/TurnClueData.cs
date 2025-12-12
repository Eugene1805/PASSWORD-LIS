using Data.Model;
using Services.Contracts.Enums;

namespace Services.Services.Internal
{
    public class TurnClueData
    {
        public MatchTeam Team 
        { 
            get;
        }
        public PasswordWord Password 
        { 
            get;
        }
        public string Clue 
        { 
            get;
        }

        public TurnClueData(MatchTeam Team, PasswordWord Password, string Clue)
        {
            this.Team = Team;
            this.Password = Password;
            this.Clue = Clue;
        }
    }
}
