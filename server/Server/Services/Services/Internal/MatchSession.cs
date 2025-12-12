using Data.Model;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Services.Services.Internal
{
    public class MatchSession : IDisposable
    {
        private readonly object scoreLock = new object();
        private bool disposed;
        private Timer roundTimer;
        private Timer validationTimer;

        private const int InvalidPasswordId = -1;
        private const int TickIntervalMilliseconds = 1000;
        private const int MinimumPenalization = 0;
        private const int InitialWordIndex = 0;
        private const int SkippedWordsInRound = 1;
        private const int DefaultScorePoints = 1;
        private const int DefaultOffset = 0;
        public string GameCode 
        { 
            get;
        }
        public MatchStatus Status 
        { 
            get;
            set;
        }
        public List<PlayerDTO> ExpectedPlayers 
        { 
            get;
        }
        public ConcurrentDictionary<int, ActivePlayer> ActivePlayers 
        { 
            get;
        }
        public int RedTeamScore 
        { 
            get;
            private set; 
        }
        public int BlueTeamScore 
        { 
            get; 
            private set; 
        }
        public int CurrentRound 
        { 
            get; 
            set; 
        }
        
        private int secondsLeft;
        public int SecondsLeft 
        { 
            get => Volatile.Read(ref secondsLeft);
            private set => secondsLeft = value; 
        }

        private int validationSecondsLeft;
        public int ValidationSecondsLeft 
        { 
            get => Volatile.Read(ref validationSecondsLeft);
            private set => validationSecondsLeft = value; 
        }
        
        public List<PasswordWord> AllRedWords 
        { 
            get; 
            set; 
        } = new List<PasswordWord>();
        public List<PasswordWord> AllBlueWords 
        { 
            get; 
            set; 
        } = new List<PasswordWord>();
        public List<PasswordWord> RedTeamWords 
        { 
            get;
            set; 
        } = new List<PasswordWord>();
        public List<PasswordWord> BlueTeamWords 
        { 
            get; 
            set; 
        } = new List<PasswordWord>();
        public int RedTeamWordIndex 
        { 
            get; 
            set; 
        }
        public int BlueTeamWordIndex 
        { 
            get; 
            set; 
        }
        public int SuddenDeathWordOffset 
        { 
            get; 
            set; 
        } = DefaultOffset;
        public List<TurnHistoryDTO> RedTeamTurnHistory 
        { 
            get; 
            set; 
        } = new List<TurnHistoryDTO>();
        public List<TurnHistoryDTO> BlueTeamTurnHistory 
        { 
            get; 
            set; 
        } = new List<TurnHistoryDTO>();
        public bool RedTeamPassedThisRound 
        { 
            get; 
            set;
        }
        public bool BlueTeamPassedThisRound 
        { 
            get; 
            set;
        }
        public List<(MatchTeam VoterTeam, List<ValidationVoteDTO> Votes)> ReceivedVotes 
        { 
            get;
        } = new List<(MatchTeam, List<ValidationVoteDTO>)>();
        public HashSet<int> PlayersWhoVoted 
        { 
            get; 
        } = new HashSet<int>();
        
        public MatchSession(string GameCode, List<PlayerDTO> ExpectedPlayers)
        {
            this.GameCode = GameCode;
            this.ExpectedPlayers = ExpectedPlayers;
            Status = MatchStatus.WaitingForPlayers;
            ActivePlayers = new ConcurrentDictionary<int, ActivePlayer>();
        }
        
        public int DecrementSecondsLeft() => Interlocked.Decrement(ref secondsLeft);
        public int DecrementValidationSecondsLeft() => Interlocked.Decrement(ref validationSecondsLeft);

        public void AddScore(MatchTeam team, int points = DefaultScorePoints)
        {
            lock (scoreLock)
            {
                if (team == MatchTeam.RedTeam)
                {
                    RedTeamScore += points;
                }
                else
                {
                    BlueTeamScore += points;
                }
            }
        }

        public void ApplyPenalties(int redPenalty, int bluePenalty)
        {
            lock (scoreLock)
            {
                RedTeamScore = Math.Max(MinimumPenalization, RedTeamScore - redPenalty);
                BlueTeamScore = Math.Max(MinimumPenalization, BlueTeamScore - bluePenalty);
            }
        }

        public PasswordWord GetCurrentPassword(MatchTeam team)
        {
            var list = (team == MatchTeam.RedTeam) ? RedTeamWords : BlueTeamWords;
            var index = (team == MatchTeam.RedTeam) ? RedTeamWordIndex : BlueTeamWordIndex;
            if (index < list.Count)
            {
                return list[index];
            }
            return new PasswordWord 
            { 
                Id = InvalidPasswordId 
            };
        }

        public ActivePlayer GetPlayerById(int id)
        {
            ActivePlayers.TryGetValue(id, out var result);
            return result;
        }

        public ActivePlayer GetPlayerByRole(MatchTeam team, PlayerRole role)
        {
            return ActivePlayers.Values.FirstOrDefault(p => p.Player.Team == team && p.Player.Role == role);
        }

        public ActivePlayer GetPartner(ActivePlayer player)
        {
            return ActivePlayers.Values.FirstOrDefault(
                p => p.Player.Team == player.Player.Team && p.Player.Id != player.Player.Id);
        }

        public IEnumerable<ActivePlayer> GetPlayersByTeam(MatchTeam team)
        {
            return ActivePlayers.Values.Where(p => p.Player.Team == team);
        }

        public void LoadWordsForRound(int wordsPerRound)
        {
            int skipCount = (CurrentRound - SkippedWordsInRound) * wordsPerRound;
            RedTeamWords = AllRedWords.Skip(skipCount).Take(wordsPerRound).ToList();
            BlueTeamWords = AllBlueWords.Skip(skipCount).Take(wordsPerRound).ToList();

            RedTeamWordIndex = InitialWordIndex;
            BlueTeamWordIndex = InitialWordIndex;
        }

        public bool LoadNextSuddenDeathWord(int totalRegularWords)
        {
            int indexToTake = totalRegularWords + SuddenDeathWordOffset;

            if (indexToTake >= AllRedWords.Count || indexToTake >= AllBlueWords.Count)
            {
                return false;
            }
            RedTeamWords = new List<PasswordWord> 
            { 
                AllRedWords[indexToTake] 
            };
            BlueTeamWords = new List<PasswordWord> 
            { 
                AllBlueWords[indexToTake] 
            };

            SuddenDeathWordOffset++;

            RedTeamWordIndex = InitialWordIndex;
            BlueTeamWordIndex = InitialWordIndex;
            return true;
        }

        public void StartRoundTimer(TimerCallback callback, object state, int durationSeconds)
        {
            roundTimer?.Dispose();
            SecondsLeft = durationSeconds;
            roundTimer = new Timer(callback, state, TickIntervalMilliseconds, TickIntervalMilliseconds);
        }

        public void StartValidationTimer(TimerCallback callback, object state, int durationSeconds)
        {
            validationTimer?.Dispose();
            ValidationSecondsLeft = durationSeconds;
            validationTimer = new Timer(callback, state, TickIntervalMilliseconds, TickIntervalMilliseconds);
        }

        public void StopTimers()
        {
            roundTimer?.Dispose();
            validationTimer?.Dispose();
            roundTimer = null;
            validationTimer = null;
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    StopTimers();
                }
                disposed = true;
            }
        }
    }
}
