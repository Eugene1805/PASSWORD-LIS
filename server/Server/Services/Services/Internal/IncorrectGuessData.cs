using Services.Contracts.Enums;

namespace Services.Services.Internal
{
    public class IncorrectGuessData
    {
        public ActivePlayer Sender 
        {
            get;
        }
        public MatchTeam Team 
        { 
            get;
        }
        public int CurrentScore 
        { 
            get;
        }

        public IncorrectGuessData(ActivePlayer Sender, MatchTeam Team, int CurrentScore)
        {
            this.Sender = Sender;
            this.Team = Team;
            this.CurrentScore = CurrentScore;
        }
    }
}
