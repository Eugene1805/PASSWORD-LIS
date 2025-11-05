using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Services;
using Services.Wrappers;
using Xunit;

namespace Test.ServicesTests
{
    public class GameManagerTests
    {/*
        private class SutContext
        {
            public GameManager SUT { get; set; }
            public Mock<IOperationContextWrapper> OperationContext { get; set; }
            public Mock<IWordRepository> WordRepository { get; set; }
            public ConcurrentQueue<Mock<IGameManagerCallback>> CallbackQueue { get; set; }
            public Dictionary<int, Mock<IGameManagerCallback>> CallbackByPlayerId { get; set; }
            public string GameCode { get; set; } = "ABCDE";
            public List<PlayerDTO> Players { get; set; }
        }

        private static SutContext CreateSut()
        {
            var ctx = new SutContext
            {
                OperationContext = new Mock<IOperationContextWrapper>(MockBehavior.Strict),
                WordRepository = new Mock<IWordRepository>(MockBehavior.Strict),
                CallbackQueue = new ConcurrentQueue<Mock<IGameManagerCallback>>(),
                CallbackByPlayerId = new Dictionary<int, Mock<IGameManagerCallback>>()
            };

            // Players: Red(Clue, Guesser) then Blue(Clue, Guesser)
            ctx.Players = new List<PlayerDTO>
            {
                new PlayerDTO { Id =1, Nickname = "RedClue", Role = PlayerRole.ClueGuy, Team = MatchTeam.RedTeam },
                new PlayerDTO { Id =2, Nickname = "RedGuess", Role = PlayerRole.Guesser, Team = MatchTeam.RedTeam },
                new PlayerDTO { Id =3, Nickname = "BlueClue", Role = PlayerRole.ClueGuy, Team = MatchTeam.BlueTeam },
                new PlayerDTO { Id =4, Nickname = "BlueGuess", Role = PlayerRole.Guesser, Team = MatchTeam.BlueTeam }
            };

            // Words for the round
            ctx.WordRepository.Setup(r => r.GetRandomWordsAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<PasswordWord> { "alpha", "bravo", "charlie", "delta", "echo" });

            // OperationContext returns one callback per subscription in enqueue order
            ctx.OperationContext.Setup(o => o.GetCallbackChannel<IGameManagerCallback>())
            .Returns(() =>
            {
                if (ctx.CallbackQueue.TryDequeue(out var cb))
                {
                    return cb.Object;
                }
                // fallback mock to avoid nulls if out of order
                return new Mock<IGameManagerCallback>().Object;
            });

            ctx.SUT = new GameManager(ctx.OperationContext.Object, ctx.WordRepository.Object);
            return ctx;
        }

        private static async Task SubscribeAllAsync(SutContext ctx)
        {
            // enqueue callbacks matching players in same order
            foreach (var p in ctx.Players)
            {
                var cb = new Mock<IGameManagerCallback>(MockBehavior.Loose);
                ctx.CallbackByPlayerId[p.Id] = cb;
                ctx.CallbackQueue.Enqueue(cb);
            }

            // create match with the4 expected players
            Assert.True(ctx.SUT.CreateMatch(ctx.GameCode, ctx.Players));

            // subscribe all players
            foreach (var p in ctx.Players)
            {
                await ctx.SUT.SubscribeToMatchAsync(ctx.GameCode, p.Id);
            }
        }

        [Fact]
        public void CreateMatch_ShouldRequireExactlyFourPlayers_AndUniqueCode()
        {
            var ctx = CreateSut();

            // wrong counts
            Assert.False(ctx.SUT.CreateMatch("A1", null));
            Assert.False(ctx.SUT.CreateMatch("A2", new List<PlayerDTO>()));
            Assert.False(ctx.SUT.CreateMatch("A3", new List<PlayerDTO> { new PlayerDTO(), new PlayerDTO(), new PlayerDTO() }));
            Assert.False(ctx.SUT.CreateMatch("A4", new List<PlayerDTO> { new PlayerDTO(), new PlayerDTO(), new PlayerDTO(), new PlayerDTO(), new PlayerDTO() }));

            // ok with4
            var ok = ctx.SUT.CreateMatch("A5", new List<PlayerDTO> { new PlayerDTO(), new PlayerDTO(), new PlayerDTO(), new PlayerDTO() });
            Assert.True(ok);

            // duplicate code should fail
            var dup = ctx.SUT.CreateMatch("A5", new List<PlayerDTO> { new PlayerDTO(), new PlayerDTO(), new PlayerDTO(), new PlayerDTO() });
            Assert.False(dup);
        }

        [Fact]
        public async Task SubscribeToMatch_ShouldStartGame_BroadcastInit_AndSendFirstPasswordToRedClue()
        {
            var ctx = CreateSut();

            // expectations: all receive OnMatchInitialized once
            foreach (var p in ctx.Players)
            {
                var cb = new Mock<IGameManagerCallback>(MockBehavior.Loose);
                ctx.CallbackByPlayerId[p.Id] = cb;
                ctx.CallbackQueue.Enqueue(cb);
                cb.Setup(c => c.OnMatchInitialized(It.IsAny<MatchInitStateDTO>()))
                .Verifiable();
            }

            // create and subscribe all (Match starts after last subscribe)
            Assert.True(ctx.SUT.CreateMatch(ctx.GameCode, ctx.Players));
            foreach (var p in ctx.Players)
            {
                await ctx.SUT.SubscribeToMatchAsync(ctx.GameCode, p.Id);
            }

            // verify init notifications
            foreach (var p in ctx.Players)
            {
                ctx.CallbackByPlayerId[p.Id].Verify(c => c.OnMatchInitialized(It.IsAny<MatchInitStateDTO>()), Times.Once);
            }

            // First password sent only to Red Clue
            ctx.CallbackByPlayerId[1].Verify(c => c.OnNewPassword("alpha"), Times.Once);
            ctx.CallbackByPlayerId[2].Verify(c => c.OnNewPassword(It.IsAny<PasswordWord>()), Times.Never);
            ctx.CallbackByPlayerId[3].Verify(c => c.OnNewPassword(It.IsAny<PasswordWord>()), Times.Never);
            ctx.CallbackByPlayerId[4].Verify(c => c.OnNewPassword(It.IsAny<PasswordWord>()), Times.Never);
        }

        [Fact]
        public async Task SubscribeToMatch_InvalidPlayer_ShouldThrowFault()
        {
            var ctx = CreateSut();

            // Prepare3 valid joins first (still waiting state)
            Assert.True(ctx.SUT.CreateMatch(ctx.GameCode, ctx.Players));
            for (int i = 0; i < 3; i++)
            {
                var cb = new Mock<IGameManagerCallback>();
                ctx.CallbackQueue.Enqueue(cb);
                await ctx.SUT.SubscribeToMatchAsync(ctx.GameCode, ctx.Players[i].Id);
            }

            //4th call uses non-expected player id
            ctx.CallbackQueue.Enqueue(new Mock<IGameManagerCallback>());
            await Assert.ThrowsAsync<FaultException>(async () =>
            await ctx.SUT.SubscribeToMatchAsync(ctx.GameCode, 999));
        }

        [Fact]
        public async Task SubmitClue_FromCurrentClueGuy_SendsClueToPartner()
        {
            var ctx = CreateSut();
            await SubscribeAllAsync(ctx);

            // Act: red clue sends a clue
            await ctx.SUT.SubmitClueAsync(ctx.GameCode, 1, "one-word-clue");

            // Assert: red guesser receives clue
            ctx.CallbackByPlayerId[2].Verify(c => c.OnClueReceived("one-word-clue"), Times.Once);
            // Others do not receive clue through OnClueReceived
            ctx.CallbackByPlayerId[1].Verify(c => c.OnClueReceived(It.IsAny<string>()), Times.Never);
            ctx.CallbackByPlayerId[3].Verify(c => c.OnClueReceived(It.IsAny<string>()), Times.Never);
            ctx.CallbackByPlayerId[4].Verify(c => c.OnClueReceived(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SubmitClue_FromWrongRoleOrWrongTeam_ShouldBeIgnored()
        {
            var ctx = CreateSut();
            await SubscribeAllAsync(ctx);

            // Blue clue tries to send during Red's turn
            await ctx.SUT.SubmitClueAsync(ctx.GameCode, 3, "blue-clue");

            // No one receives clue
            foreach (var kv in ctx.CallbackByPlayerId)
            {
                kv.Value.Verify(c => c.OnClueReceived(It.IsAny<string>()), Times.Never);
            }

            // Guesser (red) tries to send clue -> ignored
            await ctx.SUT.SubmitClueAsync(ctx.GameCode, 2, "should-be-ignored");
            foreach (var kv in ctx.CallbackByPlayerId)
            {
                kv.Value.Verify(c => c.OnClueReceived("should-be-ignored"), Times.Never);
            }
        }

        [Fact]
        public async Task SubmitGuess_Correct_ShouldBroadcastAndAdvanceWord_ThenSendNextPasswordToClueGuy()
        {
            var ctx = CreateSut();
            await SubscribeAllAsync(ctx);

            // Act: red guesser guesses current password (alpha)
            await ctx.SUT.SubmitGuessAsync(ctx.GameCode, 2, "alpha");

            // Assert: all receive guess result (correct)
            foreach (var p in ctx.Players)
            {
                ctx.CallbackByPlayerId[p.Id].Verify(c => c.OnGuessResult(It.Is<GuessResultDTO>(r => r.IsCorrect && r.Team == MatchTeam.RedTeam && r.NewScore == 1)), Times.Once);
            }

            // Red Clue gets next password "bravo"
            ctx.CallbackByPlayerId[1].Verify(c => c.OnNewPassword("bravo"), Times.Once);
        }

        [Fact]
        public async Task SubmitGuess_Wrong_ShouldNotifyOnlyActiveTeam()
        {
            var ctx = CreateSut();
            await SubscribeAllAsync(ctx);

            // Act: wrong guess from red guesser
            await ctx.SUT.SubmitGuessAsync(ctx.GameCode, 2, "wrong");

            // Assert: only red team notified (guesser and clue)
            ctx.CallbackByPlayerId[2].Verify(c => c.OnGuessResult(It.Is<GuessResultDTO>(r => !r.IsCorrect && r.Team == MatchTeam.RedTeam)), Times.Once);
            ctx.CallbackByPlayerId[1].Verify(c => c.OnGuessResult(It.Is<GuessResultDTO>(r => !r.IsCorrect && r.Team == MatchTeam.RedTeam)), Times.Once);

            // Blue team not notified
            ctx.CallbackByPlayerId[3].Verify(c => c.OnGuessResult(It.IsAny<GuessResultDTO>()), Times.Never);
            ctx.CallbackByPlayerId[4].Verify(c => c.OnGuessResult(It.IsAny<GuessResultDTO>()), Times.Never);
        }

        [Fact]
        public async Task PassTurnAsync_ShouldCompleteWithoutError()
        {
            var ctx = CreateSut();
            await ctx.SUT.PassTurnAsync("NO-GAME", 123);
            // no assert - just ensure it does not throw
        }

        [Fact]
        public async Task DisconnectionDuringClue_ShouldCancelMatch_AndNotifyRemainingPlayers()
        {
            var ctx = CreateSut();

            // prepare callbacks with behavior: red guesser throws on OnClueReceived
            foreach (var p in ctx.Players)
            {
                var cb = new Mock<IGameManagerCallback>(MockBehavior.Loose);
                ctx.CallbackByPlayerId[p.Id] = cb;
                ctx.CallbackQueue.Enqueue(cb);
            }
            ctx.CallbackByPlayerId[2]
            .Setup(c => c.OnClueReceived(It.IsAny<string>()))
            .Throws(new Exception("client disconnected"));

            // create and start match
            Assert.True(ctx.SUT.CreateMatch(ctx.GameCode, ctx.Players));
            foreach (var p in ctx.Players)
            {
                await ctx.SUT.SubscribeToMatchAsync(ctx.GameCode, p.Id);
            }

            // Act: clue from red clue to red guesser (who throws)
            await ctx.SUT.SubmitClueAsync(ctx.GameCode, 1, "boom");

            // Assert: remaining players are notified of cancellation
            ctx.CallbackByPlayerId[1].Verify(c => c.OnMatchCancelled(It.IsAny<string>()), Times.Once);
            ctx.CallbackByPlayerId[3].Verify(c => c.OnMatchCancelled(It.IsAny<string>()), Times.Once);
            ctx.CallbackByPlayerId[4].Verify(c => c.OnMatchCancelled(It.IsAny<string>()), Times.Once);

            // Disconnected player did not receive cancellation
            ctx.CallbackByPlayerId[2].Verify(c => c.OnMatchCancelled(It.IsAny<string>()), Times.Never);
        }*/
    }
}