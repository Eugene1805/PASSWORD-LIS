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
    {
        private readonly Mock<IOperationContextWrapper> mockOperationContext;
        private readonly Mock<IWordRepository> mockWordRepository;
        private readonly Mock<IMatchRepository> mockMatchRepository;
        private readonly Mock<IPlayerRepository> mockPlayerRepository;

        public GameManagerTests()
        {
            mockOperationContext = new Mock<IOperationContextWrapper>();
            mockWordRepository = new Mock<IWordRepository>();
            mockMatchRepository = new Mock<IMatchRepository>();
            mockPlayerRepository = new Mock<IPlayerRepository>();
        }

        private static List<PlayerDTO> MakePlayers()
        {
            return new List<PlayerDTO>
            {
                new PlayerDTO{ Id =1, Nickname = "RedClue", Team = MatchTeam.RedTeam, Role = PlayerRole.ClueGuy },
                new PlayerDTO{ Id =2, Nickname = "BlueClue", Team = MatchTeam.BlueTeam, Role = PlayerRole.ClueGuy },
                new PlayerDTO{ Id =3, Nickname = "RedGuess", Team = MatchTeam.RedTeam, Role = PlayerRole.Guesser },
                new PlayerDTO{ Id =4, Nickname = "BlueGuess", Team = MatchTeam.BlueTeam, Role = PlayerRole.Guesser }
            };
        }

        private static List<PasswordWord> MakeWords(int count =5)
        {
            var list = new List<PasswordWord>();
            for (int i =0; i < count; i++)
            {
                list.Add(new PasswordWord
                {
                    EnglishWord = $"WORD{i+1}",
                    SpanishWord = $"PALABRA{i+1}",
                    EnglishDescription = $"ED{i+1}",
                    SpanishDescription = $"SD{i+1}"
                });
            }
            return list;
        }

        private (GameManager sut, ConcurrentQueue<Mock<IGameManagerCallback>> callbacks) CreateSut()
        {
            var cbQueue = new ConcurrentQueue<Mock<IGameManagerCallback>>();
            mockOperationContext
                .Setup(o => o.GetCallbackChannel<IGameManagerCallback>())
                .Returns(() =>
                {
                    if (cbQueue.TryDequeue(out var cb)) return cb.Object;
                    return new Mock<IGameManagerCallback>().Object;
                });

            var sut = new GameManager(mockOperationContext.Object, mockWordRepository.Object, mockMatchRepository.Object, mockPlayerRepository.Object);
            return (sut, cbQueue);
        }

        [Fact]
        public void CreateMatch_ShouldSucceed_WithFourPlayers_AndFailOnDuplicate()
        {
            // Arrange
            var (sut, _) = CreateSut();
            var players = MakePlayers();

            // Act
            var ok = sut.CreateMatch("ABCDE", players);
            var dup = sut.CreateMatch("ABCDE", players);

            // Assert
            Assert.True(ok);
            Assert.False(dup);
        }

        [Fact]
        public void CreateMatch_ShouldFail_WithInvalidPlayersCount()
        {
            // Arrange
            var (sut, _) = CreateSut();

            // Act
            var nullList = sut.CreateMatch("CODE1", null);
            var less = sut.CreateMatch("CODE2", new List<PlayerDTO> { new PlayerDTO() });
            var more = sut.CreateMatch("CODE3", new List<PlayerDTO> { new PlayerDTO(), new PlayerDTO(), new PlayerDTO(), new PlayerDTO(), new PlayerDTO() });

            // Assert
            Assert.False(nullList);
            Assert.False(less);
            Assert.False(more);
        }

        [Fact]
        public async Task SubscribeToMatch_ShouldBroadcastInit_OnLastPlayer_AndSendFirstWordToRedClue()
        {
            // Arrange
            var (sut, queue) = CreateSut();
            var players = MakePlayers();
            Assert.True(sut.CreateMatch("GAME1", players));

            // prepare callbacks (order matches subscribe order)
            var redClue = new Mock<IGameManagerCallback>();
            var blueClue = new Mock<IGameManagerCallback>();
            var redGuess = new Mock<IGameManagerCallback>();
            var blueGuess = new Mock<IGameManagerCallback>();
            queue.Enqueue(redClue);
            queue.Enqueue(blueClue);
            queue.Enqueue(redGuess);
            queue.Enqueue(blueGuess);

            mockWordRepository.Setup(w => w.GetRandomWordsAsync(It.IsAny<int>())).ReturnsAsync(MakeWords());

            // Act
            await sut.SubscribeToMatchAsync("GAME1",1);
            await sut.SubscribeToMatchAsync("GAME1",2);
            await sut.SubscribeToMatchAsync("GAME1",3);
            await sut.SubscribeToMatchAsync("GAME1",4); // last triggers init + first password

            // Assert: everyone gets initialized
            redClue.Verify(c => c.OnMatchInitialized(It.IsAny<MatchInitStateDTO>()), Times.Once);
            blueClue.Verify(c => c.OnMatchInitialized(It.IsAny<MatchInitStateDTO>()), Times.Once);
            redGuess.Verify(c => c.OnMatchInitialized(It.IsAny<MatchInitStateDTO>()), Times.Once);
            blueGuess.Verify(c => c.OnMatchInitialized(It.IsAny<MatchInitStateDTO>()), Times.Once);

            // Red clue receives first word
            redClue.Verify(c => c.OnNewPassword(It.Is<PasswordWordDTO>(p => p.EnglishWord == "WORD1")), Times.Once);
            blueClue.Verify(c => c.OnNewPassword(It.IsAny<PasswordWordDTO>()), Times.Never);
            redGuess.Verify(c => c.OnNewPassword(It.IsAny<PasswordWordDTO>()), Times.Never);
            blueGuess.Verify(c => c.OnNewPassword(It.IsAny<PasswordWordDTO>()), Times.Never);
        }

        [Fact]
        public async Task SubscribeToMatch_ShouldThrow_WhenMatchMissing()
        {
            // Arrange
            var (sut, _) = CreateSut();

            // Act + Assert
            await Assert.ThrowsAsync<FaultException>(async () => await sut.SubscribeToMatchAsync("NOPE",1));
        }

        [Fact]
        public async Task SubscribeToMatch_ShouldThrow_WhenUnauthorizedPlayer()
        {
            // Arrange
            var (sut, queue) = CreateSut();
            var players = MakePlayers();
            sut.CreateMatch("GAME2", players);
            queue.Enqueue(new Mock<IGameManagerCallback>());

            // Act + Assert
            await Assert.ThrowsAsync<FaultException>(async () => await sut.SubscribeToMatchAsync("GAME2",999));
        }

        [Fact]
        public async Task SubscribeToMatch_ShouldThrow_WhenAlreadyConnected()
        {
            // Arrange
            var (sut, queue) = CreateSut();
            var players = MakePlayers();
            sut.CreateMatch("GAME3", players);
            queue.Enqueue(new Mock<IGameManagerCallback>());
            await sut.SubscribeToMatchAsync("GAME3",1);
            queue.Enqueue(new Mock<IGameManagerCallback>());

            // Act + Assert
            await Assert.ThrowsAsync<FaultException>(async () => await sut.SubscribeToMatchAsync("GAME3",1));
        }

        [Fact]
        public async Task SubscribeToMatch_ShouldThrow_WhenWrongStatus()
        {
            // Arrange
            var (sut, queue) = CreateSut();
            var players = MakePlayers();
            sut.CreateMatch("GAME4", players);

            var redClue = new Mock<IGameManagerCallback>();
            var blueClue = new Mock<IGameManagerCallback>();
            var redGuess = new Mock<IGameManagerCallback>();
            var blueGuess = new Mock<IGameManagerCallback>();
            queue.Enqueue(redClue);
            queue.Enqueue(blueClue);
            queue.Enqueue(redGuess);
            queue.Enqueue(blueGuess);

            mockWordRepository.Setup(w => w.GetRandomWordsAsync(It.IsAny<int>())).ReturnsAsync(MakeWords());

            await sut.SubscribeToMatchAsync("GAME4",1);
            await sut.SubscribeToMatchAsync("GAME4",2);
            await sut.SubscribeToMatchAsync("GAME4",3);
            await sut.SubscribeToMatchAsync("GAME4",4); // match starts

            // Act + Assert: now status is InProgress
            await Assert.ThrowsAsync<FaultException>(async () => await sut.SubscribeToMatchAsync("GAME4",1));
        }

        [Fact]
        public async Task SubmitClueAsync_ShouldDeliverToPartner_WhenValid()
        {
            // Arrange
            var (sut, queue) = CreateSut();
            var players = MakePlayers();
            sut.CreateMatch("GAME5", players);

            var redClue = new Mock<IGameManagerCallback>();
            var blueClue = new Mock<IGameManagerCallback>();
            var redGuess = new Mock<IGameManagerCallback>();
            var blueGuess = new Mock<IGameManagerCallback>();
            queue.Enqueue(redClue);
            queue.Enqueue(blueClue);
            queue.Enqueue(redGuess);
            queue.Enqueue(blueGuess);

            mockWordRepository.Setup(w => w.GetRandomWordsAsync(It.IsAny<int>())).ReturnsAsync(MakeWords());

            await sut.SubscribeToMatchAsync("GAME5",1);
            await sut.SubscribeToMatchAsync("GAME5",2);
            await sut.SubscribeToMatchAsync("GAME5",3);
            await sut.SubscribeToMatchAsync("GAME5",4);

            // Act
            await sut.SubmitClueAsync("GAME5",1, "my clue");

            // Assert: partner (red guesser id=3) gets clue
            redGuess.Verify(c => c.OnClueReceived("my clue"), Times.Once);
            redClue.Verify(c => c.OnClueReceived(It.IsAny<string>()), Times.Never);
            blueClue.Verify(c => c.OnClueReceived(It.IsAny<string>()), Times.Never);
            blueGuess.Verify(c => c.OnClueReceived(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SubmitClueAsync_ShouldNoop_WhenInvalid()
        {
            // Arrange
            var (sut, _) = CreateSut();
            // No match created -> noop

            // Act + Assert (no exception)
            await sut.SubmitClueAsync("NOPE",1, "x");
        }

        [Fact]
        public async Task SubmitGuessAsync_ShouldBroadcastCorrect_AndAdvanceWord()
        {
            // Arrange
            var (sut, queue) = CreateSut();
            var players = MakePlayers();
            sut.CreateMatch("GAME6", players);

            var redClue = new Mock<IGameManagerCallback>();
            var blueClue = new Mock<IGameManagerCallback>();
            var redGuess = new Mock<IGameManagerCallback>();
            var blueGuess = new Mock<IGameManagerCallback>();
            queue.Enqueue(redClue);
            queue.Enqueue(blueClue);
            queue.Enqueue(redGuess);
            queue.Enqueue(blueGuess);

            mockWordRepository.Setup(w => w.GetRandomWordsAsync(It.IsAny<int>())).ReturnsAsync(MakeWords());

            await sut.SubscribeToMatchAsync("GAME6",1);
            await sut.SubscribeToMatchAsync("GAME6",2);
            await sut.SubscribeToMatchAsync("GAME6",3);
            await sut.SubscribeToMatchAsync("GAME6",4);

            // Act: red guesser guesses correctly first word
            await sut.SubmitGuessAsync("GAME6",3, "WORD1");

            // Assert: all get result correct
            redClue.Verify(c => c.OnGuessResult(It.Is<GuessResultDTO>(r => r.IsCorrect && r.Team == MatchTeam.RedTeam)), Times.Once);
            blueClue.Verify(c => c.OnGuessResult(It.IsAny<GuessResultDTO>()), Times.Once);
            redGuess.Verify(c => c.OnGuessResult(It.IsAny<GuessResultDTO>()), Times.Once);
            blueGuess.Verify(c => c.OnGuessResult(It.IsAny<GuessResultDTO>()), Times.Once);

            // Red clue gets next word (WORD2)
            redClue.Verify(c => c.OnNewPassword(It.Is<PasswordWordDTO>(p => p.EnglishWord == "WORD2")), Times.Once);
        }

        [Fact]
        public async Task SubmitGuessAsync_ShouldNotifyOnlyTeam_WhenIncorrect()
        {
            // Arrange
            var (sut, queue) = CreateSut();
            var players = MakePlayers();
            sut.CreateMatch("GAME7", players);

            var redClue = new Mock<IGameManagerCallback>();
            var blueClue = new Mock<IGameManagerCallback>();
            var redGuess = new Mock<IGameManagerCallback>();
            var blueGuess = new Mock<IGameManagerCallback>();
            queue.Enqueue(redClue);
            queue.Enqueue(blueClue);
            queue.Enqueue(redGuess);
            queue.Enqueue(blueGuess);

            mockWordRepository.Setup(w => w.GetRandomWordsAsync(It.IsAny<int>())).ReturnsAsync(MakeWords());

            await sut.SubscribeToMatchAsync("GAME7",1);
            await sut.SubscribeToMatchAsync("GAME7",2);
            await sut.SubscribeToMatchAsync("GAME7",3);
            await sut.SubscribeToMatchAsync("GAME7",4);

            // Act: wrong guess
            await sut.SubmitGuessAsync("GAME7",3, "WRONG");

            // Assert: only red team gets it
            redGuess.Verify(c => c.OnGuessResult(It.Is<GuessResultDTO>(r => !r.IsCorrect && r.Team == MatchTeam.RedTeam)), Times.Once);
            redClue.Verify(c => c.OnGuessResult(It.IsAny<GuessResultDTO>()), Times.Once);
            blueClue.Verify(c => c.OnGuessResult(It.IsAny<GuessResultDTO>()), Times.Never);
            blueGuess.Verify(c => c.OnGuessResult(It.IsAny<GuessResultDTO>()), Times.Never);
        }

        [Fact]
        public async Task SubmitValidationVotesAsync_ShouldApplyPenalties_AndSwitchTurn()
        {
            // Arrange
            var (sut, queue) = CreateSut();
            var players = MakePlayers();
            sut.CreateMatch("GAME8", players);

            var redClue = new Mock<IGameManagerCallback>();
            var blueClue = new Mock<IGameManagerCallback>();
            var redGuess = new Mock<IGameManagerCallback>();
            var blueGuess = new Mock<IGameManagerCallback>();
            queue.Enqueue(redClue);
            queue.Enqueue(blueClue);
            queue.Enqueue(redGuess);
            queue.Enqueue(blueGuess);

            mockWordRepository.Setup(w => w.GetRandomWordsAsync(It.IsAny<int>())).ReturnsAsync(MakeWords());

            await sut.SubscribeToMatchAsync("GAME8",1);
            await sut.SubscribeToMatchAsync("GAME8",2);
            await sut.SubscribeToMatchAsync("GAME8",3);
            await sut.SubscribeToMatchAsync("GAME8",4);

            // Create history for TurnId0 and1 then complete5 correct guesses for red team to enter validation
            await sut.SubmitClueAsync("GAME8",1, "clue-0"); // TurnId0
            await sut.SubmitGuessAsync("GAME8",3, "WORD1");
            await sut.SubmitClueAsync("GAME8",1, "clue-1"); // TurnId1
            await sut.SubmitGuessAsync("GAME8",3, "WORD2");
            await sut.SubmitGuessAsync("GAME8",3, "WORD3");
            await sut.SubmitGuessAsync("GAME8",3, "WORD4");
            await sut.SubmitGuessAsync("GAME8",3, "WORD5");

            // Validators receive history to validate (Blue team only)
            blueClue.Verify(c => c.OnBeginRoundValidation(It.Is<List<TurnHistoryDTO>>(l => l.Count >=2 && l[0].TurnId ==0 && l[1].TurnId ==1)), Times.Once);
            blueGuess.Verify(c => c.OnBeginRoundValidation(It.Is<List<TurnHistoryDTO>>(l => l.Count >=2)), Times.Once);
            redClue.Verify(c => c.OnBeginRoundValidation(It.IsAny<List<TurnHistoryDTO>>()), Times.Never);
            redGuess.Verify(c => c.OnBeginRoundValidation(It.IsAny<List<TurnHistoryDTO>>()), Times.Never);

            // Two blue validators vote
            var votes1 = new List<ValidationVoteDTO> { new ValidationVoteDTO { TurnId =0, PenalizeSynonym = true } };
            var votes2 = new List<ValidationVoteDTO> { new ValidationVoteDTO { TurnId =1, PenalizeMultiword = true } };
            await sut.SubmitValidationVotesAsync("GAME8",2, votes1); // Blue Clue

            // After the first vote, there should be no validation result yet
            redClue.Verify(c => c.OnValidationComplete(It.IsAny<ValidationResultDTO>()), Times.Never);
            redGuess.Verify(c => c.OnValidationComplete(It.IsAny<ValidationResultDTO>()), Times.Never);
            blueClue.Verify(c => c.OnValidationComplete(It.IsAny<ValidationResultDTO>()), Times.Never);
            blueGuess.Verify(c => c.OnValidationComplete(It.IsAny<ValidationResultDTO>()), Times.Never);

            await sut.SubmitValidationVotesAsync("GAME8",4, votes2); // Blue Guess

            // Assert: everyone gets validation result
            redClue.Verify(c => c.OnValidationComplete(It.Is<ValidationResultDTO>(v => v.TeamThatWasValidated == MatchTeam.RedTeam && v.TotalPenaltyApplied ==3)), Times.Once);
            redGuess.Verify(c => c.OnValidationComplete(It.IsAny<ValidationResultDTO>()), Times.Once);
            blueClue.Verify(c => c.OnValidationComplete(It.IsAny<ValidationResultDTO>()), Times.Once);
            blueGuess.Verify(c => c.OnValidationComplete(It.IsAny<ValidationResultDTO>()), Times.Once);

            // Turn switches to Blue and Blue Clue receives a new word for next turn
            blueClue.Verify(c => c.OnNewPassword(It.IsAny<PasswordWordDTO>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task PassTurnAsync_ShouldComplete()
        {
            // Arrange
            var (sut, _) = CreateSut();

            // Act + Assert
            await sut.PassTurnAsync("ANY",1);
        }
    }
}