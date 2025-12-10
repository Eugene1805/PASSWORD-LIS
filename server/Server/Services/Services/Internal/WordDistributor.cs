using Data.Model;
using log4net;
using Services.Contracts.Enums;
using System;
using System.Threading.Tasks;

namespace Services.Services.Internal
{
    public class WordDistributor
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WordDistributor));

        public async Task DistributeWordToTeamAsync(MatchSession session, MatchTeam team,
            Func<MatchSession, int, Task> onDisconnection)
        {
            var teamPlayers = GetTeamPlayers(session, team);
            var word = session.GetCurrentPassword(team);

            try
            {
                SendFullWordToClueGuy(teamPlayers.ClueGuy, word);
                SendMaskedWordToGuesser(teamPlayers.Guesser, word);
            }
            catch (Exception ex)
            {
                log.WarnFormat("Player disconnected during word distribution in match {0}: {1}",
                    session.GameCode, ex.Message);

                await HandleDistributionDisconnections(session, teamPlayers, onDisconnection);
            }
        }

        public async Task SendNextWordToTeamAsync(MatchSession session, MatchTeam team,
            Func<MatchSession, int, Task> onDisconnection)
        {
            var nextWord = session.GetCurrentPassword(team);
            var teamPlayers = GetTeamPlayers(session, team);

            try
            {
                SendFullWordToClueGuy(teamPlayers.ClueGuy, nextWord);
                SendMaskedWordToGuesser(teamPlayers.Guesser, nextWord);
            }
            catch
            {
                await HandleNextWordDisconnections(session, teamPlayers, onDisconnection);
            }
        }

        public async Task SendPassTurnUpdatesAsync(MatchSession session, PassTurnData passTurnData,
            Func<MatchSession, int, Task> onDisconnection)
        {
            try
            {
                GameBroadcaster.SendToPlayer(passTurnData.Sender,
                    cb => cb.OnNewPassword(DTOMapper.ToWordDTO(passTurnData.NextWord)));
            }
            catch
            {
                await onDisconnection(session, passTurnData.Sender.Player.Id);
            }

            var partner = session.GetPartner(passTurnData.Sender);
            if (partner?.Callback != null)
            {
                try
                {
                    GameBroadcaster.SendToPlayer(partner, cb =>
                    {
                        cb.OnNewPassword(DTOMapper.ToMaskedWordDTO(passTurnData.NextWord));
                        cb.OnClueReceived("Your partner passed the word.");
                    });
                }
                catch
                {
                    await onDisconnection(session, partner.Player.Id);
                }
            }
        }

        public async Task NotifyPartnerOfClueAsync(MatchSession session, ClueData clueData,
            Func<MatchSession, int, Task> onDisconnection)
        {
            var partner = session.GetPartner(clueData.Sender);
            if (partner?.Callback == null)
            {
                return;
            }

            try
            {
                GameBroadcaster.SendToPlayer(partner, cb => cb.OnClueReceived(clueData.Clue));
            }
            catch
            {
                await onDisconnection(session, partner.Player.Id);
            }
        }

        private static TeamPlayers GetTeamPlayers(MatchSession session, MatchTeam team)
        {
            var clueGuy = session.GetPlayerByRole(team, PlayerRole.ClueGuy);
            var guesser = session.GetPlayerByRole(team, PlayerRole.Guesser);
            return new TeamPlayers(clueGuy, guesser);
        }

        private static void SendFullWordToClueGuy(ActivePlayer clueGuy, PasswordWord word)
        {
            if (clueGuy?.Callback != null)
            {
                GameBroadcaster.SendToPlayer(clueGuy, cb => cb.OnNewPassword(DTOMapper.ToWordDTO(word)));
            }
        }

        private static void SendMaskedWordToGuesser(ActivePlayer guesser, PasswordWord word)
        {
            if (guesser?.Callback != null)
            {
                GameBroadcaster.SendToPlayer(guesser, cb => cb.OnNewPassword(DTOMapper.ToMaskedWordDTO(word)));
            }
        }

        private static async Task HandleDistributionDisconnections(MatchSession session,
            TeamPlayers teamPlayers, Func<MatchSession, int, Task> onDisconnection)
        {
            if (teamPlayers.ClueGuy?.Player != null)
            {
                await onDisconnection(session, teamPlayers.ClueGuy.Player.Id);
            }
            if (teamPlayers.Guesser?.Player != null)
            {
                await onDisconnection(session, teamPlayers.Guesser.Player.Id);
            }
        }

        private static async Task HandleNextWordDisconnections(MatchSession session,
            TeamPlayers teamPlayers, Func<MatchSession, int, Task> onDisconnection)
        {
            if (teamPlayers.ClueGuy?.Callback != null)
            {
                await onDisconnection(session, teamPlayers.ClueGuy.Player.Id);
            }
            if (teamPlayers.Guesser?.Callback != null)
            {
                await onDisconnection(session, teamPlayers.Guesser.Player.Id);
            }
        }
    }
}
