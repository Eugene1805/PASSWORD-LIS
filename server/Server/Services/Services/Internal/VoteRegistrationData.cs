using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using System.Collections.Generic;

namespace Services.Services.Internal
{
    public class VoteRegistrationData
    {
        public int SenderPlayerId { get; }
        public MatchTeam Team { get; }
        public List<ValidationVoteDTO> Votes { get; }

        public VoteRegistrationData(int senderPlayerId, MatchTeam team, List<ValidationVoteDTO> votes)
        {
            SenderPlayerId = senderPlayerId;
            Team = team;
            Votes = votes;
        }
    }
}
