using log4net;
using PASSWORD_LIS_Client.GameManagerServiceReference;
using System;
using System.Collections.Generic;
using System.ServiceModel;
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

        private readonly DuplexChannelFactory<IGameManager> factory;
        private IGameManager proxy;
        private static readonly ILog log = LogManager.GetLogger(typeof(WcfGameManagerService));
        public WcfGameManagerService()
        {
            var context = new InstanceContext(this);
            factory = new DuplexChannelFactory<IGameManager>(context, "*");
            proxy = GetProxy();
        }
        private IGameManager GetProxy()
        {
            ICommunicationObject channel = proxy as ICommunicationObject;

            if (proxy == null || channel == null ||
                channel.State == CommunicationState.Closed ||
                channel.State == CommunicationState.Faulted)
            {
                try
                {
                    if (channel != null && channel.State == CommunicationState.Faulted)
                    {
                        channel.Abort();
                    }
                }
                catch (Exception ex)
                {
                    log.WarnFormat("Error aborting previues channle: {0}", ex.Message);
                    throw;
                }

                proxy = factory.CreateChannel();
            }

            return proxy;
        }

        public void Cleanup()
        {
            try
            {
                if (proxy is ICommunicationObject channel)
                {
                    if (channel.State == CommunicationState.Opened)
                    {
                        channel.Close();
                    }
                    else
                    {
                        channel.Abort();
                    }
                }
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Error during Cleanup: {0}", ex.Message);
                (proxy as ICommunicationObject)?.Abort();
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
            return GetProxy().PassTurnAsync(gameCode, senderPlayerId);
        }

        public Task SubmitClueAsync(string gameCode, int senderPlayerId, string clue)
        {
            return GetProxy().SubmitClueAsync(gameCode, senderPlayerId, clue);
        }

        public Task SubmitGuessAsync(string gameCode, int senderPlayerId, string guess)
        {
            return GetProxy().SubmitGuessAsync(gameCode, senderPlayerId, guess);
        }

        public Task SubmitValidationVotesAsync(string gameCode, int senderPlayerId, List<ValidationVoteDTO> votes)
        {
            return GetProxy().SubmitValidationVotesAsync(gameCode, senderPlayerId, votes);
        }

        public Task SubscribeToMatchAsync(string gameCode, int playerId)
        {
            return GetProxy().SubscribeToMatchAsync(gameCode, playerId);
        }

        public void OnValidationComplete(ValidationResultDTO result)
        {
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
