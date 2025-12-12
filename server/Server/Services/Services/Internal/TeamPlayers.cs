namespace Services.Services.Internal
{
    public class TeamPlayers
    {
        public ActivePlayer ClueGuy 
        { 
            get; 
        }
        public ActivePlayer Guesser 
        { 
            get;
        }

        public TeamPlayers(ActivePlayer ClueGuy, ActivePlayer Guesser)
        {
            this.ClueGuy = ClueGuy;
            this.Guesser = Guesser;
        }
    }
}
