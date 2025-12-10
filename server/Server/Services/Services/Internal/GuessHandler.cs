using Data.Model;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using System;
using System.Threading.Tasks;

namespace Services.Services.Internal
{
    public class GuessHandler
    {
        public bool IsGuessCorrect(PasswordWord currentPassword, string guess)
        {
            if (currentPassword == null)
            {
                return false;
            }
            return guess.Equals(currentPassword.EnglishWord, StringComparison.OrdinalIgnoreCase) ||
                   guess.Equals(currentPassword.SpanishWord, StringComparison.OrdinalIgnoreCase);
        }

        public async Task HandleCorrectGuessAsync(MatchSession session, MatchTeam team,
            TurnHistoryManager turnHistoryManager, WordDistributor wordDistributor,
            Func<MatchSession, Action<IGameManagerCallback>, Task> broadcastAction,
            Func<MatchSession, MatchTeam?, Task> onGameEnd,
            Func<MatchSession, int, Task> onDisconnection)
        {
            if (session.Status == MatchStatus.SuddenDeath)
            {
                session.Status = MatchStatus.Finished;
                session.StopTimers();
                session.AddScore(team);
                await onGameEnd(session, team);
                return;
            }

            session.AddScore(team);
            turnHistoryManager.AdvanceWordIndex(session, team);

            int newScore = (team == MatchTeam.RedTeam) ? session.RedTeamScore : session.BlueTeamScore;
            var resultDto = new GuessResultDTO { IsCorrect = true, Team = team, NewScore = newScore };

            await broadcastAction(session, cb => cb.OnGuessResult(resultDto));
            await wordDistributor.SendNextWordToTeamAsync(session, team, onDisconnection);
        }

        public async Task HandleIncorrectGuessAsync(MatchSession session, ActivePlayer sender,
            MatchTeam team, int currentScore, Func<MatchSession, int, Task> onDisconnection)
        {
            var resultDto = new GuessResultDTO { IsCorrect = false, Team = team, NewScore = currentScore };
            var clueGuy = session.GetPlayerByRole(team, PlayerRole.ClueGuy);

            try
            {
                GameBroadcaster.SendToPlayer(sender, cb => cb.OnGuessResult(resultDto));
                if (clueGuy?.Callback != null)
                {
                    GameBroadcaster.SendToPlayer(clueGuy, cb => cb.OnGuessResult(resultDto));
                }
            }
            catch
            {
                await onDisconnection(session, sender.Player.Id);
                if (clueGuy?.Player != null)
                {
                    await onDisconnection(session, clueGuy.Player.Id);
                }
            }
        }
    }
}
