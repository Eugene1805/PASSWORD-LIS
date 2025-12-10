namespace Services.Services.Internal
{
    public class TeamPlayers
    {
        public ActivePlayer ClueGuy { get; }
        public ActivePlayer Guesser { get; }

        public TeamPlayers(ActivePlayer clueGuy, ActivePlayer guesser)
        {
            ClueGuy = clueGuy;
            Guesser = guesser;
        }
    }
}
