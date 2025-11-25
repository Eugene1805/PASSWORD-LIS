using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Services.Internal
{
    public class GameBroadcaster
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GameBroadcaster));

        public async Task<IEnumerable<int>> BroadcastAsync(MatchSession match, Action<IGameManagerCallback> action)
        {
            var disconnectedPlayers = new ConcurrentBag<int>();

            var tasks = match.ActivePlayers.Select(async playerEntry =>
            {
                try
                {
                    action(playerEntry.Value.Callback);
                    await Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    log.WarnFormat("Failed to contact player {0}: {1}", playerEntry.Key, ex.Message);
                    disconnectedPlayers.Add(playerEntry.Key);
                }
            });

            await Task.WhenAll(tasks);
            return disconnectedPlayers;
        }
        public async Task BroadcastToGroupAsync(IEnumerable<(IGameManagerCallback Callback, PlayerDTO Player)> targets,
            Action<IGameManagerCallback> action)
        {
            var tasks = targets.Select(target => Task.Run(() =>
            {
                try
                {
                    action(target.Callback);
                }
                catch (Exception ex)
                {
                    log.Warn($"Failed to send to specific group player {target.Player.Id}: {ex.Message}");
                }
            }));
            await Task.WhenAll(tasks);
        }

        public void SendToPlayer((IGameManagerCallback Callback, PlayerDTO Player) player, Action<IGameManagerCallback> action)
        {
            if (player.Callback == null) return;
            try
            {
                action(player.Callback);
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to send to player {player.Player.Id}: {ex.Message}");
                throw;
            }
        }
    }
}
