using PASSWORD_LIS_Client.GameManagerServiceReference;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class ValidationTurnViewModel : BaseViewModel
    {
        public int TurnId { get; }
        public string Word { get; }
        public string Clue { get; }
        public string Language { get; }

        private bool penalizeSynonym;
        public bool PenalizeSynonym
        {
            get => penalizeSynonym;
            set => SetProperty(ref penalizeSynonym, value);
        }

        private bool penalizeMultiword;
        public bool PenalizeMultiword
        {
            get => penalizeMultiword;
            set => SetProperty(ref penalizeMultiword, value);
        }

        public ValidationTurnViewModel(TurnHistoryDTO turn, string language)
        {
            TurnId = turn.TurnId;
            Clue = turn.ClueUsed;
            Language = language;

            if (Language.StartsWith("es"))
            {
                Word = turn.Password.SpanishWord;
            }
            else
            {
                Word = turn.Password.EnglishWord;
            }
        }
    }
}
