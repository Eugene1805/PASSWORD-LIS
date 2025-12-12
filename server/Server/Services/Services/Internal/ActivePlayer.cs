using Services.Contracts;
using Services.Contracts.DTOs;

namespace Services.Services.Internal
{
    public class ActivePlayer
    {
        public IGameManagerCallback Callback 
        { 
            get; 
        }
        public PlayerDTO Player 
        { 
            get;
        }

        public ActivePlayer(IGameManagerCallback callback, PlayerDTO player)
        {
            Callback = callback;
            Player = player;
        }
    }
}
