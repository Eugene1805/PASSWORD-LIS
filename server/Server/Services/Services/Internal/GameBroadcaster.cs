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
                catch (Exception ex) // Atrapamos CommunicationException, Timeout, etc.
                {
                    log.WarnFormat("Failed to contact player {0}: {1}", playerEntry.Key, ex.Message);
                    // Agregamos el ID a la bolsa de desconectados
                    disconnectedPlayers.Add(playerEntry.Key);
                }
            });

            await Task.WhenAll(tasks);

            // Devolvemos la lista de IDs caídos al GameManager
            return disconnectedPlayers;
        }

        // Sobrecarga para enviar solo a un subconjunto (ej. solo un equipo)
        public async Task BroadcastToGroupAsync(
            System.Collections.Generic.IEnumerable<(IGameManagerCallback Callback, PlayerDTO Player)> targets,
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

        // Helper para enviar a un solo jugador con seguridad
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
                // Aquí podrías lanzar una excepción custom si quieres que el GameManager desconecte al jugador
                throw;
            }
        }
    }
}
