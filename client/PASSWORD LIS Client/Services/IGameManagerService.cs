using log4net;
using PASSWORD_LIS_Client.GameManagerServiceReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PASSWORD_LIS_Client.Services
{
    public interface IGameManagerService
    {
        event Action<MatchInitStateDTO> MatchInitialized;
        event Action<int> TimerTick;
        event Action<PasswordWordDTO> NewPasswordReceived;
        event Action<string> ClueReceived;
        event Action<GuessResultDTO> GuessResult;
        event Action<List<TurnHistoryDTO>> BeginRoundValidation;
        event Action<ValidationResultDTO> ValidationComplete;
        event Action<MatchSummaryDTO> MatchOver;
        event Action<string> MatchCancelled;
        event Action<RoundStartStateDTO> NewRoundStarted;
        event Action<int> ValidationTimerTick;
        event Action SuddenDeathStarted;

        Task SubscribeToMatchAsync(string gameCode, int playerId);
        Task SubmitClueAsync(string gameCode, int senderPlayerId, string clue);
        Task SubmitGuessAsync(string gameCode, int senderPlayerId, string guess);
        Task PassTurnAsync(string gameCode, int senderPlayerId);
        Task SubmitValidationVotesAsync(string gameCode, int senderPlayerId, List<ValidationVoteDTO> votes);
        void Cleanup();
    }

    public class WcfGameManagerService : IGameManagerService, IGameManagerCallback
    {
        
        public event Action<MatchInitStateDTO> MatchInitialized;
        public event Action<int> TimerTick;
        public event Action<PasswordWordDTO> NewPasswordReceived;
        public event Action<string> ClueReceived;
        public event Action<GuessResultDTO> GuessResult;
        public event Action<List<TurnHistoryDTO>> BeginRoundValidation;
        public event Action<ValidationResultDTO> ValidationComplete;
        public event Action<MatchSummaryDTO> MatchOver;
        public event Action<string> MatchCancelled;
        public event Action<RoundStartStateDTO> NewRoundStarted;
        public event Action<int> ValidationTimerTick;
        public event Action SuddenDeathStarted;

        private readonly GameManagerClient client;
        private static readonly ILog log = LogManager.GetLogger(typeof(WcfGameManagerService));
        public WcfGameManagerService()
        {
            var context = new InstanceContext(this);
            client = new GameManagerClient(context);
        }

        public void Cleanup()
        {
            try
            {
                if (client.State == CommunicationState.Opened)
                {
                    client.Close();
                }
            }
            catch (CommunicationException)
            {
                client.Abort();
            }
            catch (TimeoutException)
            {
                client.Abort();
            }
            catch (Exception)
            {
                client.Abort();
            }
        }

        public void OnBeginRoundValidation(List<TurnHistoryDTO> turns)
        {
            BeginRoundValidation?.Invoke(turns);
        }

        public void OnClueReceived(string clue)
        {
            ClueReceived?.Invoke(clue);
        }

        public void OnGuessResult(GuessResultDTO result)
        {
            GuessResult?.Invoke(result);
        }

        public void OnMatchCancelled(string reason)
        {
            MatchCancelled?.Invoke(reason);
            Cleanup();
        }

        public void OnMatchInitialized(MatchInitStateDTO state)
        {
            MatchInitialized?.Invoke(state);
        }

        public void OnMatchOver(MatchSummaryDTO summary)
        {
            MatchOver?.Invoke(summary);
            Cleanup();
        }

        public void OnNewPassword(PasswordWordDTO password)
        {
            NewPasswordReceived?.Invoke(password);
        }

        public void OnTimerTick(int secondsLeft)
        {
            TimerTick?.Invoke(secondsLeft);
        }

        public Task PassTurnAsync(string gameCode, int senderPlayerId)
        {
            return client.PassTurnAsync(gameCode, senderPlayerId);
        }

        public Task SubmitClueAsync(string gameCode, int senderPlayerId, string clue)
        {
            return client.SubmitClueAsync(gameCode, senderPlayerId, clue);
        }

        public Task SubmitGuessAsync(string gameCode, int senderPlayerId, string guess)
        {
            return client.SubmitGuessAsync(gameCode, senderPlayerId, guess);
        }

        public Task SubmitValidationVotesAsync(string gameCode, int senderPlayerId, List<ValidationVoteDTO> votes)
        {
            log.Info($"SubmitValidationVotesAsync called - GameCode: {gameCode}, PlayerId: {senderPlayerId}, VotesCount: {votes?.Count}");
            return client.SubmitValidationVotesAsync(gameCode, senderPlayerId, votes);
        }

        public Task SubscribeToMatchAsync(string gameCode, int playerId)
        {
            return client.SubscribeToMatchAsync(gameCode, playerId);
        }

        public void OnValidationComplete(ValidationResultDTO result)
        {
            log.Info($"OnValidationComplete received - RedScore: {result.NewRedTeamScore}, BlueScore: {result.NewBlueTeamScore}");
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    ValidationComplete?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error($"Error in OnValidationComplete callback: {ex.Message}", ex);
                }
            }));
        }

        public void OnNewRoundStarted(RoundStartStateDTO state)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    NewRoundStarted?.Invoke(state);
                }
                catch (Exception ex)
                {
                    log.Error($"Error in OnNewRoundStarted callback: {ex.Message}", ex);
                }
            }));
        }

        public void OnValidationTimerTick(int secondsLeft)
        {
            ValidationTimerTick?.Invoke(secondsLeft);
        }

        public void OnSuddenDeathStarted()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    log.Info("OnSuddenDeathStarted received");
                    SuddenDeathStarted?.Invoke();
                }
                catch (Exception ex)
                {
                    log.Error($"Error in OnSuddenDeathStarted callback: {ex.Message}", ex);
                }
            }));
        }
    }
}
