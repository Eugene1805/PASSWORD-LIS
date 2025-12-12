using Services.Contracts;
using Services.Contracts.Enums;
using System;
using System.Threading.Tasks;

namespace Services.Services.Internal
{
    public class GuessContext
    {
        public TurnHistoryManager TurnHistoryManager 
        { 
            get;
        }
        public WordDistributor WordDistributor 
        { 
            get;
        }
        public Func<MatchSession, Action<IGameManagerCallback>, Task> BroadcastAction 
        { 
            get;
        }
        public Func<MatchSession, MatchTeam?, Task> OnGameEnd 
        { 
            get;
        }
        public Func<MatchSession, int, Task> OnDisconnection 
        { 
            get;
        }

        public GuessContext(
            TurnHistoryManager TurnHistoryManager,
            WordDistributor WordDistributor,
            Func<MatchSession, Action<IGameManagerCallback>, Task> BroadcastAction,
            Func<MatchSession, MatchTeam?, Task> OnGameEnd,
            Func<MatchSession, int, Task> OnDisconnection)
        {
            this.TurnHistoryManager = TurnHistoryManager;
            this.WordDistributor = WordDistributor;
            this.BroadcastAction = BroadcastAction;
            this.OnGameEnd = OnGameEnd;
            this.OnDisconnection = OnDisconnection;
        }
    }
}
