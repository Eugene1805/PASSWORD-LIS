namespace Services.Services.Internal
{
    public class ClueData
    {
        public ActivePlayer Sender 
        { 
            get;
        }
        public string Clue 
        { 
            get;
        }

        public ClueData(ActivePlayer sender, string clue)
        {
            Sender = sender;
            Clue = clue;
        }
    }
}
