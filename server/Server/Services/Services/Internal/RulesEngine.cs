using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using System.Collections.Generic;
using System.Linq;

namespace Services.Services.Internal
{
    public static class RulesEngine
    {
        private const int PenaltySynonym = 2;
        private const int PenaltyMultiword = 1;

        public static (int RedPenalty, int BluePenalty) CalculateValidationPenalties(
            List<(MatchTeam VoterTeam, List<ValidationVoteDTO> Votes)> receivedVotes)
        {
            var redPenalties = new HashSet<(int TurnId, bool Multiword, bool Synonym)>();
            var bluePenalties = new HashSet<(int TurnId, bool Multiword, bool Synonym)>();

            foreach (var (voterTeam, voteList) in receivedVotes)
            {
                foreach (var vote in voteList)
                {
                    ProcessVote(voterTeam, vote, redPenalties, bluePenalties);
                }
            }

            int redPenalty = redPenalties.Count(p => p.Multiword) * PenaltyMultiword
                            + redPenalties.Count(p => p.Synonym) * PenaltySynonym;

            int bluePenalty = bluePenalties.Count(p => p.Multiword) * PenaltyMultiword
                             + bluePenalties.Count(p => p.Synonym) * PenaltySynonym;

            return (redPenalty, bluePenalty);
        }

        private static void ProcessVote(
            MatchTeam voterTeam,
            ValidationVoteDTO vote,
            HashSet<(int TurnId, bool Multiword, bool Synonym)> redPenalties,
            HashSet<(int TurnId, bool Multiword, bool Synonym)> bluePenalties)
        {
            var targetSet = voterTeam == MatchTeam.RedTeam ? bluePenalties : redPenalties;

            bool multiword = vote.PenalizeMultiword;
            bool synonym = vote.PenalizeSynonym;

            var existing = targetSet.FirstOrDefault(t => t.TurnId == vote.TurnId);
            if (existing.TurnId == vote.TurnId)
            {
                multiword = multiword || existing.Multiword;
                synonym = synonym || existing.Synonym;
                targetSet.Remove(existing);
            }

            targetSet.Add((vote.TurnId, multiword, synonym));
        }
    }
}