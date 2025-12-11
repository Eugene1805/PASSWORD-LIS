using Data.Model;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;

namespace Services.Services.Internal
{
    public class TurnHistoryManager
    {
        private const int InvalidPasswordId = -1;
        private const string PassIdentifier = "[]";
        public void RecordClue(MatchSession session, TurnClueData turnClueData)
        {
            var historyItem = new TurnHistoryDTO
            {
                TurnId = GetCurrentWordIndex(session, turnClueData.Team),
                Password = DTOMapper.ToWordDTO(turnClueData.Password),
                ClueUsed = turnClueData.Clue
            };

            AddToTeamHistory(session, turnClueData.Team, historyItem);
        }

        public void RecordPass(MatchSession session, MatchTeam team, PasswordWord password)
        {
            if (password == null || password.Id == InvalidPasswordId)
            {
                return;
            }

            var turnClueData = new TurnClueData(team, password, PassIdentifier);
            RecordClue(session, turnClueData);
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

        private int GetCurrentWordIndex(MatchSession session, MatchTeam team)
        {
            return (team == MatchTeam.RedTeam) ? session.RedTeamWordIndex : session.BlueTeamWordIndex;
        }

        private void AddToTeamHistory(MatchSession session, MatchTeam team, TurnHistoryDTO historyItem)
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
