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

        public async Task HandleCorrectGuessAsync(MatchSession session, MatchTeam team, GuessContext context)
        {
            if (session.Status == MatchStatus.SuddenDeath)
            {
                session.Status = MatchStatus.Finished;
                session.StopTimers();
                session.AddScore(team);
                await context.OnGameEnd(session, (MatchTeam?)team);
                return;
            }

            session.AddScore(team);
            context.TurnHistoryManager.AdvanceWordIndex(session, team);

            int newScore = (team == MatchTeam.RedTeam) ? session.RedTeamScore : session.BlueTeamScore;
            var resultDto = new GuessResultDTO { IsCorrect = true, Team = team, NewScore = newScore };

            await context.BroadcastAction(session, cb => cb.OnGuessResult(resultDto));
            await context.WordDistributor.SendNextWordToTeamAsync(session, team, context.OnDisconnection);
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
