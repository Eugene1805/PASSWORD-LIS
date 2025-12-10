using Data.Model;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;

namespace Services.Services.Internal
{
    public class TurnHistoryManager
    {
        public void RecordClue(MatchSession session, MatchTeam team, PasswordWord password, string clue)
        {
            var historyItem = new TurnHistoryDTO
            {
                TurnId = GetCurrentWordIndex(session, team),
                Password = DTOMapper.ToWordDTO(password),
                ClueUsed = clue
            };

            AddToTeamHistory(session, team, historyItem);
        }

        public void RecordPass(MatchSession session, MatchTeam team, PasswordWord password)
        {
            if (password == null || password.Id == -1)
            {
                return;
            }

            RecordClue(session, team, password, "[]");
        }

        public bool HasTeamPassed(MatchSession session, MatchTeam team)
        {
            return (team == MatchTeam.RedTeam && session.RedTeamPassedThisRound) ||
                   (team == MatchTeam.BlueTeam && session.BlueTeamPassedThisRound);
        }

        public void MarkTeamAsPassed(MatchSession session, MatchTeam team)
        {
            if (team == MatchTeam.RedTeam)
            {
                session.RedTeamPassedThisRound = true;
            }
            else
            {
                session.BlueTeamPassedThisRound = true;
            }
        }

        public void AdvanceWordIndex(MatchSession session, MatchTeam team)
        {
            if (team == MatchTeam.RedTeam)
            {
                session.RedTeamWordIndex++;
            }
            else
            {
                session.BlueTeamWordIndex++;
            }
        }

        public void ApplyPassAndAdvance(MatchSession session, MatchTeam team)
        {
            MarkTeamAsPassed(session, team);
            AdvanceWordIndex(session, team);
        }

        private static int GetCurrentWordIndex(MatchSession session, MatchTeam team)
        {
            return (team == MatchTeam.RedTeam) ? session.RedTeamWordIndex : session.BlueTeamWordIndex;
        }

        private static void AddToTeamHistory(MatchSession session, MatchTeam team, TurnHistoryDTO historyItem)
        {
            if (team == MatchTeam.RedTeam)
            {
                session.RedTeamTurnHistory.Add(historyItem);
            }
            else
            {
                session.BlueTeamTurnHistory.Add(historyItem);
            }
        }
    }
}
