using Data.Model;
using Services.Contracts.Enums;

namespace Services.Services.Internal
{
    public class TurnClueData
    {
        public MatchTeam Team { get; }
        public PasswordWord Password { get; }
        public string Clue { get; }

        public TurnClueData(MatchTeam team, PasswordWord password, string clue)
        {
            Team = team;
            Password = password;
            Clue = clue;
        }
    }
}
