using Data.Model;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Services.Services.Internal
{
    public class MatchSession :IDisposable
    {
        public string GameCode { get; }
        public MatchStatus Status { get; set; }
        public List<PlayerDTO> ExpectedPlayers { get; }
        public ConcurrentDictionary<int, (IGameManagerCallback Callback, PlayerDTO Player)> ActivePlayers { get; }
        public int RedTeamScore { get; private set; }
        public int BlueTeamScore { get; private set; }
        public int CurrentRound { get; set; }
        // Backing fields for atomic operations
        private int _secondsLeft;
        private int _validationSecondsLeft;
        public int SecondsLeft { get => Volatile.Read(ref _secondsLeft); private set => _secondsLeft = value; }
        public int ValidationSecondsLeft { get => Volatile.Read(ref _validationSecondsLeft); private set => _validationSecondsLeft = value; }
        private Timer roundTimer;
        private Timer validationTimer;
        public List<PasswordWord> RedTeamWords { get; set; } = new List<PasswordWord>();
        public List<PasswordWord> BlueTeamWords { get; set; } = new List<PasswordWord>();
        public int RedTeamWordIndex { get; set; }
        public int BlueTeamWordIndex { get; set; }
        public List<TurnHistoryDTO> RedTeamTurnHistory { get; set; } = new List<TurnHistoryDTO>();
        public List<TurnHistoryDTO> BlueTeamTurnHistory { get; set; } = new List<TurnHistoryDTO>();
        public bool RedTeamPassedThisRound { get; set; }
        public bool BlueTeamPassedThisRound { get; set; }
        public List<(MatchTeam VoterTeam, List<ValidationVoteDTO> Votes)> ReceivedVotes { get; } 
            = new List<(MatchTeam, List<ValidationVoteDTO>)>();
        public HashSet<int> PlayersWhoVoted { get; } = new HashSet<int>();
        private readonly object lockObj = new object();
        private bool disposed;
        public MatchSession(string gameCode, List<PlayerDTO> expectedPlayers)
        {
            GameCode = gameCode;
            ExpectedPlayers = expectedPlayers;
            Status = MatchStatus.WaitingForPlayers;
            ActivePlayers = new ConcurrentDictionary<int, (IGameManagerCallback, PlayerDTO)>();
        }
        // Atomic decrement helpers
        public int DecrementSecondsLeft() => Interlocked.Decrement(ref _secondsLeft);
        public int DecrementValidationSecondsLeft() => Interlocked.Decrement(ref _validationSecondsLeft);
        public void AddScore(MatchTeam team, int points = 1)
        {
            lock (lockObj)
            {
                if (team == MatchTeam.RedTeam) RedTeamScore += points;
                else BlueTeamScore += points;
            }
        }
        public void ApplyPenalties(int redPenalty, int bluePenalty)
        {
            lock (lockObj)
            {
                RedTeamScore = Math.Max(0, RedTeamScore - redPenalty);
                BlueTeamScore = Math.Max(0, BlueTeamScore - bluePenalty);
            }
        }
        public PasswordWord GetCurrentPassword(MatchTeam team)
        {
            var list = (team == MatchTeam.RedTeam) ? RedTeamWords : BlueTeamWords;
            var index = (team == MatchTeam.RedTeam) ? RedTeamWordIndex : BlueTeamWordIndex;
            if (index < list.Count) return list[index];
            return null; 
        }
        public (IGameManagerCallback Callback, PlayerDTO Player) GetPlayerById(int id)
        {
            ActivePlayers.TryGetValue(id, out var result);
            return result;
        }
        public (IGameManagerCallback Callback, PlayerDTO Player) GetPlayerByRole(MatchTeam team, PlayerRole role)
        {
            return ActivePlayers.Values.FirstOrDefault(p => p.Player.Team == team && p.Player.Role == role);
        }
        public (IGameManagerCallback Callback, PlayerDTO Player) GetPartner((IGameManagerCallback Callback,
            PlayerDTO Player) player)
        {
            return ActivePlayers.Values.FirstOrDefault(
                p => p.Player.Team == player.Player.Team && p.Player.Id != player.Player.Id);
        }
        public IEnumerable<(IGameManagerCallback Callback, PlayerDTO Player)> GetPlayersByTeam(MatchTeam team)
        {
            return ActivePlayers.Values.Where(p => p.Player.Team == team);
        }
        public void StartRoundTimer(TimerCallback callback, object state, int durationSeconds)
        {
            roundTimer?.Dispose();
            SecondsLeft = durationSeconds;
            roundTimer = new Timer(callback, state, 1000, 1000);
        }
        public void StartValidationTimer(TimerCallback callback, object state, int durationSeconds)
        {
            validationTimer?.Dispose();
            ValidationSecondsLeft = durationSeconds;
            validationTimer = new Timer(callback, state, 1000, 1000);
        }
        public void StopTimers()
        {
            roundTimer?.Dispose();
            validationTimer?.Dispose();
            roundTimer = null;
            validationTimer = null;
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
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
