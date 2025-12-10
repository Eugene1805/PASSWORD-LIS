using Services.Contracts.DTOs;
using System.Collections.Generic;

namespace Services.Services.Internal
{
    public class VoteSubmissionData
    {
        public int SenderPlayerId { get; }
        public List<ValidationVoteDTO> Votes { get; }
        public string GameCode { get; }

        public VoteSubmissionData(int senderPlayerId, List<ValidationVoteDTO> votes, string gameCode)
        {
            SenderPlayerId = senderPlayerId;
            Votes = votes;
            GameCode = gameCode;
        }
    }
}
