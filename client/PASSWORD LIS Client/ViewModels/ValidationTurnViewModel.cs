using PASSWORD_LIS_Client.GameManagerServiceReference;
using System.Linq;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class ValidationTurnViewModel : BaseViewModel
    {
        private const string SpanishLanguageCode = "es";
        private const string PassedTurnToken = "[]";
        private const string ClueSeparator = ", ";

        public int TurnId { get; }
        public string Word { get; }
        public string Clues { get; }
        public string Language { get; }
        public bool IsPassed { get; }

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

        public ValidationTurnViewModel(IGrouping<int, TurnHistoryDTO> turnGroup, string language)
        {
            var firstTurn = turnGroup.First();
            TurnId = firstTurn.TurnId;
            Language = language;

            if (Language.StartsWith(SpanishLanguageCode))
            {
                Word = firstTurn.Password.SpanishWord;
            }
            else
            {
                Word = firstTurn.Password.EnglishWord;
            }

            var cluesList = turnGroup.Select(t => t.ClueUsed).ToList();

            IsPassed = cluesList.Contains(PassedTurnToken);

            if (IsPassed)
            {
                Clues = Properties.Langs.Lang.wordPassedText;                                                     
            }
            else
            {
                Clues = string.Join(ClueSeparator, cluesList);
            }
        }
    }
}
