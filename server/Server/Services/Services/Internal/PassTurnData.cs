using Data.Model;

namespace Services.Services.Internal
{
    public class PassTurnData
    {
        public ActivePlayer Sender { get; }
        public PasswordWord NextWord { get; }

        public PassTurnData(ActivePlayer sender, PasswordWord nextWord)
        {
            Sender = sender;
            NextWord = nextWord;
        }
    }
}
