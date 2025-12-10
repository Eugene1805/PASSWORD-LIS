using Services.Contracts.Enums;

namespace Services.Services.Internal
{
    public class IncorrectGuessData
    {
        public ActivePlayer Sender { get; }
        public MatchTeam Team { get; }
        public int CurrentScore { get; }

        public IncorrectGuessData(ActivePlayer sender, MatchTeam team, int currentScore)
        {
            Sender = sender;
            Team = team;
            CurrentScore = currentScore;
        }
    }
}
