﻿using Data.DAL.Interfaces;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Wrappers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class WaitingRoomManager : IWaitingRoomManager
    {
        sealed class Game
        {
            public string GameCode { get; set; }
            public int HostPlayerId { get; set; }
            public ConcurrentDictionary<int, (IWaitingRoomCallback, PlayerDTO)> Players { get; } = new ConcurrentDictionary<int, (IWaitingRoomCallback, PlayerDTO)>();
        }

        private readonly ConcurrentDictionary<string, Game> games = new ConcurrentDictionary<string, Game>();
        private readonly IPlayerRepository repository;
        private readonly IOperationContextWrapper operationContext;
        private static int guestIdCounter = 0;
        private const int MaxPlayersPerGame = 4;
        private static readonly Random random = new Random();

        public WaitingRoomManager(IPlayerRepository playerRepository, IOperationContextWrapper operationContextWrapper)
        {
            repository = playerRepository;
            operationContext = operationContextWrapper;
        }

        public async Task<string> CreateGameAsync(string email)
        {
            var gameCode = GenerateGameCode();
            var newGame = new Game { GameCode = gameCode };

            if (!games.TryAdd(gameCode, newGame))
            {
                return await CreateGameAsync(email);
            }

            var playerId = await JoinGameAsRegisteredPlayerAsync(gameCode, email);
            if (playerId > 0)
            {
                newGame.HostPlayerId = playerId;
                return gameCode;
            }

            games.TryRemove(gameCode, out _);
            // ADD personalize exception
            throw new FaultException("No se pudo crear la partida");
        }
        // TODO ADD fault exceptions
        public async Task<int> JoinGameAsRegisteredPlayerAsync(string gameCode, string email)
        {
            if (!games.TryGetValue(gameCode, out var game) || game.Players.Count >= MaxPlayersPerGame)
            {
                return -1;
            }

            var playerEntity = repository.GetPlayerByEmail(email);
            if (playerEntity == null)
            {
                return -1;
            }

            var playerDto = new PlayerDTO
            {
                Id = playerEntity.Id,
                PhotoId = playerEntity.UserAccount.PhotoId ?? 0,
                Nickname = playerEntity.UserAccount.Nickname
            };

            AssignTeamAndRole(playerDto, game.Players.Count);
            if (game.Players.ContainsKey(playerDto.Id))
            {
                return -1;
            }

            var success = await TryAddPlayerAsync(game, playerDto);
            return success ? playerDto.Id : -1;
        }
        // ADD fault exceptions
        public async Task<bool> JoinGameAsGuestAsync(string gameCode, string nickname)
        {
            if (!games.TryGetValue(gameCode, out var game) || game.Players.Count >= MaxPlayersPerGame)
            {
                return false;
            }

            int guestId = Interlocked.Decrement(ref guestIdCounter);

            var playerDto = new PlayerDTO
            {
                Id = guestId,
                Nickname = nickname
            };

            AssignTeamAndRole(playerDto, game.Players.Count);

            if (game.Players.ContainsKey(playerDto.Id))
            {
                return false;
            }

            return await TryAddPlayerAsync(game, playerDto);
        }

        public async Task LeaveGameAsync(string gameCode, int playerId)
        {
            if (games.TryGetValue(gameCode, out var game) && game.Players.TryRemove(playerId, out _))
            {
                bool wasHost = game.HostPlayerId == playerId;
                await BroadcastAsync(game, client => client.Item1.OnPlayerLeft(playerId));

                if (wasHost)
                {
                    await HostLeftAsync(gameCode);
                }
                else if (game.Players.IsEmpty)
                {
                    games.TryRemove(gameCode, out _);
                }
            }
        }

        public async Task SendMessageAsync(string gameCode, ChatMessageDTO message)
        {
            if (games.TryGetValue(gameCode, out var game))
            {
                await BroadcastAsync(game, client => client.Item1.OnMessageReceived(message));
            }
        }

        public async Task StartGameAsync(string gameCode)
        {
            if (games.TryRemove(gameCode, out var game))
            {
                await BroadcastAsync(game, client => client.Item1.OnGameStarted());
            }
        }

        public Task<List<PlayerDTO>> GetPlayersInGameAsync(string gameCode)
        {
            if (games.TryGetValue(gameCode, out var game))
            {
                var players = game.Players.Values.Select(p => p.Item2).ToList();
                return Task.FromResult(players);
            }

            return Task.FromResult(new List<PlayerDTO>());
        }
        public async Task HostLeftAsync(string gameCode)
        {
            if (games.TryGetValue(gameCode, out var game))
            {
                await BroadcastAsync(game, client => client.Item1.OnHostLeft());

                games.TryRemove(gameCode, out _);
            }
        }
        private async Task<bool> TryAddPlayerAsync(Game game, PlayerDTO player)
        {
            var callback = operationContext.GetCallbackChannel<IWaitingRoomCallback>();

            if (!game.Players.TryAdd(player.Id, (callback, player)))
            {
                return false;
            }

            await BroadcastAsync(game, client => client.Item1.OnPlayerJoined(player));
            return true;
        }

        private static void AssignTeamAndRole(PlayerDTO player, int playerCountInRoom)
        {
            switch (playerCountInRoom)
            {
                case 0:
                    player.Team = MatchTeam.RedTeam;
                    player.Role = PlayerRole.ClueGuy;
                    break;
                case 1:
                    player.Team = MatchTeam.BlueTeam;
                    player.Role = PlayerRole.ClueGuy;
                    break;
                case 2:
                    player.Team = MatchTeam.RedTeam;
                    player.Role = PlayerRole.Guesser;
                    break;
                case 3:
                    player.Team = MatchTeam.BlueTeam;
                    player.Role = PlayerRole.Guesser;
                    break;
            }
        }
        // TODO ADD a better exception handling strategy
        private async Task BroadcastAsync(Game game, Action<(IWaitingRoomCallback, PlayerDTO)> action)
        {
            var tasks = game.Players.Select(async playerEntry =>
            {
                try
                {
                    action(playerEntry.Value);
                }
                catch
                {
                    await LeaveGameAsync(game.GameCode, playerEntry.Key);
                }
            });

            await Task.WhenAll(tasks);
        }

        private static string GenerateGameCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            lock (random)
            {
                return new string(Enumerable.Repeat(chars, 5)
                    .Select(s => chars[random.Next(chars.Length)]).ToArray());
            }
        }
    }
}
