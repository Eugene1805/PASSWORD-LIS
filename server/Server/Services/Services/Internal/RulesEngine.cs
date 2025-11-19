using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using System.Collections.Generic;

namespace Services.Services.Internal
{
    public static class RulesEngine
    {
        private const int PenaltySynonym = 2;
        private const int PenaltyMultiword = 1;

        public static (int RedPenalty, int BluePenalty) CalculateValidationPenalties(
            List<(MatchTeam VoterTeam, List<ValidationVoteDTO> Votes)> receivedVotes)
        {
            var redTurnsToPenalizeMultiword = new HashSet<int>();
            var blueTurnsToPenalizeMultiword = new HashSet<int>();
            var redTurnsToPenalizeSynonym = new HashSet<int>();
            var blueTurnsToPenalizeSynonym = new HashSet<int>();

            foreach (var (voterTeam, voteList) in receivedVotes)
            {
                foreach (var vote in voteList)
                {
                    if (voterTeam == MatchTeam.RedTeam)
                    {
                        if (vote.PenalizeMultiword) blueTurnsToPenalizeMultiword.Add(vote.TurnId);
                        if (vote.PenalizeSynonym) blueTurnsToPenalizeSynonym.Add(vote.TurnId);
                    }
                    else
                    {
                        if (vote.PenalizeMultiword) redTurnsToPenalizeMultiword.Add(vote.TurnId);
                        if (vote.PenalizeSynonym) redTurnsToPenalizeSynonym.Add(vote.TurnId);
                    }
                }
            }

            int redPenalty = (redTurnsToPenalizeMultiword.Count * PenaltyMultiword)
                           + (redTurnsToPenalizeSynonym.Count * PenaltySynonym);

            int bluePenalty = (blueTurnsToPenalizeMultiword.Count * PenaltyMultiword)
                            + (blueTurnsToPenalizeSynonym.Count * PenaltySynonym);

            return (redPenalty, bluePenalty);
        }
    }
}