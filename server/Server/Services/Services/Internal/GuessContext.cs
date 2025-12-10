using Services.Contracts;
using Services.Contracts.Enums;
using System;
using System.Threading.Tasks;

namespace Services.Services.Internal
{
    public class GuessContext
    {
        public TurnHistoryManager TurnHistoryManager { get; }
        public WordDistributor WordDistributor { get; }
        public Func<MatchSession, Action<IGameManagerCallback>, Task> BroadcastAction { get; }
        public Func<MatchSession, MatchTeam?, Task> OnGameEnd { get; }
        public Func<MatchSession, int, Task> OnDisconnection { get; }

        public GuessContext(
            TurnHistoryManager turnHistoryManager,
            WordDistributor wordDistributor,
            Func<MatchSession, Action<IGameManagerCallback>, Task> broadcastAction,
            Func<MatchSession, MatchTeam?, Task> onGameEnd,
            Func<MatchSession, int, Task> onDisconnection)
        {
            TurnHistoryManager = turnHistoryManager;
            WordDistributor = wordDistributor;
            BroadcastAction = broadcastAction;
            OnGameEnd = onGameEnd;
            OnDisconnection = onDisconnection;
        }
    }
}
