using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public async Task SubscribeToMatch_ShouldBroadcastInit_AndSendFirstWordToBothTeams_ClueAndGuesser()
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
            await sut.SubscribeToMatchAsync("GAME1",4); // last triggers init + first passwords and round start

            // Assert: everyone gets initialized and round started
            redClue.Verify(c => c.OnMatchInitialized(It.IsAny<MatchInitStateDTO>()), Times.Once);
            blueClue.Verify(c => c.OnMatchInitialized(It.IsAny<MatchInitStateDTO>()), Times.Once);
            redGuess.Verify(c => c.OnMatchInitialized(It.IsAny<MatchInitStateDTO>()), Times.Once);
            blueGuess.Verify(c => c.OnMatchInitialized(It.IsAny<MatchInitStateDTO>()), Times.Once);

            redClue.Verify(c => c.OnNewRoundStarted(It.IsAny<RoundStartStateDTO>()), Times.Once);
            blueClue.Verify(c => c.OnNewRoundStarted(It.IsAny<RoundStartStateDTO>()), Times.Once);
            redGuess.Verify(c => c.OnNewRoundStarted(It.IsAny<RoundStartStateDTO>()), Times.Once);
            blueGuess.Verify(c => c.OnNewRoundStarted(It.IsAny<RoundStartStateDTO>()), Times.Once);

            // Clue guys receive first word of their team
            redClue.Verify(c => c.OnNewPassword(It.Is<PasswordWordDTO>(p => p.EnglishWord == "WORD1")), Times.Once);
            blueClue.Verify(c => c.OnNewPassword(It.Is<PasswordWordDTO>(p => p.EnglishWord == "WORD1")), Times.Once);

            // Guessers receive masked word with descriptions
            redGuess.Verify(c => c.OnNewPassword(It.Is<PasswordWordDTO>(p => p.EnglishWord == string.Empty && p.SpanishWord == string.Empty && p.EnglishDescription == "ED1" && p.SpanishDescription == "SD1")), Times.Once);
            blueGuess.Verify(c => c.OnNewPassword(It.Is<PasswordWordDTO>(p => p.EnglishWord == string.Empty && p.SpanishWord == string.Empty && p.EnglishDescription == "ED1" && p.SpanishDescription == "SD1")), Times.Once);
        }

        [Fact]
        public async Task SubscribeToMatch_ShouldThrow_WhenMatchMissing()
        {
            // Arrange
            var (sut, _) = CreateSut();

            // Act + Assert
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () => await sut.SubscribeToMatchAsync("NOPE",1));
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
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () => await sut.SubscribeToMatchAsync("GAME2",999));
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
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () => await sut.SubscribeToMatchAsync("GAME3",1));
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
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(async () => await sut.SubscribeToMatchAsync("GAME4",1));
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
        public async Task SubmitClueAsync_ShouldNoop_WhenInvalidInputsOrState()
        {
            // Arrange
            var (sut, queue) = CreateSut();

            // No match created -> noop, no exception
            await sut.SubmitClueAsync("NOPE",1, "x");

            // Create a match and subscribe but send empty/whitespace clue -> noop
            var players = MakePlayers();
            sut.CreateMatch("GAME5b", players);

            var redClue = new Mock<IGameManagerCallback>();
            var blueClue = new Mock<IGameManagerCallback>();
            var redGuess = new Mock<IGameManagerCallback>();
            var blueGuess = new Mock<IGameManagerCallback>();
            queue.Enqueue(redClue);
            queue.Enqueue(blueClue);
            queue.Enqueue(redGuess);
            queue.Enqueue(blueGuess);
            mockWordRepository.Setup(w => w.GetRandomWordsAsync(It.IsAny<int>())).ReturnsAsync(MakeWords());
            await sut.SubscribeToMatchAsync("GAME5b",1);
            await sut.SubscribeToMatchAsync("GAME5b",2);
            await sut.SubscribeToMatchAsync("GAME5b",3);
            await sut.SubscribeToMatchAsync("GAME5b",4);

            await sut.SubmitClueAsync("GAME5b",1, "   ");
            redGuess.Verify(c => c.OnClueReceived(It.IsAny<string>()), Times.Never);
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

            // Red team: clue gets next full word (WORD2) and guesser gets masked next word with ED2/SD2
            redClue.Verify(c => c.OnNewPassword(It.Is<PasswordWordDTO>(p => p.EnglishWord == "WORD2")), Times.Once);
            redGuess.Verify(c => c.OnNewPassword(It.Is<PasswordWordDTO>(p => p.EnglishWord == string.Empty && p.SpanishWord == string.Empty && p.EnglishDescription == "ED2" && p.SpanishDescription == "SD2")), Times.Once);
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
        public async Task SubmitGuessAsync_ShouldNoop_WhenInvalidInputsOrState()
        {
            var (sut, queue) = CreateSut();

            // No match
            await sut.SubmitGuessAsync("NOPE", 3, "WORD1");

            // Create match and subscribe
            var players = MakePlayers();
            sut.CreateMatch("GAME7b", players);

            var redClue = new Mock<IGameManagerCallback>();
            var blueClue = new Mock<IGameManagerCallback>();
            var redGuess = new Mock<IGameManagerCallback>();
            var blueGuess = new Mock<IGameManagerCallback>();
            queue.Enqueue(redClue);
            queue.Enqueue(blueClue);
            queue.Enqueue(redGuess);
            queue.Enqueue(blueGuess);
            mockWordRepository.Setup(w => w.GetRandomWordsAsync(It.IsAny<int>())).ReturnsAsync(MakeWords());
            await sut.SubscribeToMatchAsync("GAME7b",1);
            await sut.SubscribeToMatchAsync("GAME7b",2);
            await sut.SubscribeToMatchAsync("GAME7b",3);
            await sut.SubscribeToMatchAsync("GAME7b",4);

            // Empty guess -> noop
            await sut.SubmitGuessAsync("GAME7b", 3, "   ");
            redClue.Verify(c => c.OnGuessResult(It.IsAny<GuessResultDTO>()), Times.Never);
            redGuess.Verify(c => c.OnGuessResult(It.IsAny<GuessResultDTO>()), Times.Never);

            // Wrong role (clue guy tries to guess) -> noop
            await sut.SubmitGuessAsync("GAME7b", 1, "WORD1");
            redClue.Verify(c => c.OnGuessResult(It.IsAny<GuessResultDTO>()), Times.Never);
        }

        [Fact]
        public async Task ValidationPhase_ShouldSendHistory_ToOppositeTeam_AndProcessVotes_WhenAllVoted()
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

            // Access private state via reflection
            var matchesField = typeof(GameManager).GetField("matches", BindingFlags.NonPublic | BindingFlags.Instance);
            var dictObj = matchesField?.GetValue(sut);
            var dictType = dictObj?.GetType();
            var tryGetValue = dictType?.GetMethod("TryGetValue", new[] { typeof(string), dictType.GetGenericArguments()[1].MakeByRefType() });
            var args = new object[] { "GAME8", null };
            var found = (bool?)tryGetValue?.Invoke(dictObj, args);
            Assert.True(found);
            var matchStateObj = args[1];

            var matchStateType = matchStateObj.GetType();
            var redHistoryProp = matchStateType.GetProperty("RedTeamTurnHistory");
            var statusProp = matchStateType.GetProperty("Status");

            // Populate some history for Red team
            var history = new List<TurnHistoryDTO>
            {
                new TurnHistoryDTO { TurnId = 0, Password = new PasswordWordDTO{ EnglishWord = "WORD1", SpanishWord = "PALABRA1"}, ClueUsed = "clue-0" },
                new TurnHistoryDTO { TurnId = 1, Password = new PasswordWordDTO{ EnglishWord = "WORD2", SpanishWord = "PALABRA2"}, ClueUsed = "clue-1" },
            };
            redHistoryProp?.SetValue(matchStateObj, history);

            // Call StartValidationPhaseAsync via reflection
            var startValidation = typeof(GameManager).GetMethod("StartValidationPhaseAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task?)startValidation?.Invoke(sut, new object[] { matchStateObj });
            await task;

            // Opposite team (Blue) should receive history
            blueClue.Verify(c => c.OnBeginRoundValidation(It.Is<List<TurnHistoryDTO>>(l => l.Count == 2 && l[0].TurnId == 0 && l[1].TurnId == 1)), Times.Once);
            blueGuess.Verify(c => c.OnBeginRoundValidation(It.Is<List<TurnHistoryDTO>>(l => l.Count == 2)), Times.Once);
            redClue.Verify(c => c.OnBeginRoundValidation(It.IsAny<List<TurnHistoryDTO>>()), Times.Never);
            redGuess.Verify(c => c.OnBeginRoundValidation(It.IsAny<List<TurnHistoryDTO>>()), Times.Never);

            // Now everyone (4 players) votes to trigger processing immediately
            var votesBlueClue = new List<ValidationVoteDTO> { new ValidationVoteDTO { TurnId =0, PenalizeSynonym = true } };
            var votesBlueGuess = new List<ValidationVoteDTO> { new ValidationVoteDTO { TurnId =1, PenalizeMultiword = true } };
            var emptyVotes = new List<ValidationVoteDTO>();

            await sut.SubmitValidationVotesAsync("GAME8",2, votesBlueClue); // Blue Clue
            await sut.SubmitValidationVotesAsync("GAME8",4, votesBlueGuess); // Blue Guess
            await sut.SubmitValidationVotesAsync("GAME8",1, emptyVotes); // Red Clue
            await sut.SubmitValidationVotesAsync("GAME8",3, emptyVotes); // Red Guess

            await Task.Delay(100); // Allow async processing to complete

            // After all votes, validation should complete and either start new round or end (with TOTAL_ROUNDS=1 it ends)
            redClue.Verify(c => c.OnValidationComplete(It.Is<ValidationResultDTO>(v => v.TotalPenaltyApplied == 3)), Times.Once);
            redGuess.Verify(c => c.OnValidationComplete(It.IsAny<ValidationResultDTO>()), Times.Once);
            blueClue.Verify(c => c.OnValidationComplete(It.IsAny<ValidationResultDTO>()), Times.Once);
            blueGuess.Verify(c => c.OnValidationComplete(It.IsAny<ValidationResultDTO>()), Times.Once);

            // Since TOTAL_ROUNDS=1 and scores tie by default, sudden death should start
            redClue.Verify(c => c.OnSuddenDeathStarted(), Times.Once);
            redGuess.Verify(c => c.OnSuddenDeathStarted(), Times.Once);
            blueClue.Verify(c => c.OnSuddenDeathStarted(), Times.Once);
            blueGuess.Verify(c => c.OnSuddenDeathStarted(), Times.Once);
        }

        [Fact]
        public async Task SuddenDeath_CorrectGuess_ShouldFinishMatchAndDeclareWinner()
        {
            // Arrange
            var (sut, queue) = CreateSut();
            var players = MakePlayers();
            sut.CreateMatch("GAME9", players);

            var redClue = new Mock<IGameManagerCallback>();
            var blueClue = new Mock<IGameManagerCallback>();
            var redGuess = new Mock<IGameManagerCallback>();
            var blueGuess = new Mock<IGameManagerCallback>();
            queue.Enqueue(redClue);
            queue.Enqueue(blueClue);
            queue.Enqueue(redGuess);
            queue.Enqueue(blueGuess);

            mockWordRepository.Setup(w => w.GetRandomWordsAsync(It.IsAny<int>())).ReturnsAsync(MakeWords());

            await sut.SubscribeToMatchAsync("GAME9",1);
            await sut.SubscribeToMatchAsync("GAME9",2);
            await sut.SubscribeToMatchAsync("GAME9",3);
            await sut.SubscribeToMatchAsync("GAME9",4);

            // Force validation phase with empty histories to trigger immediate processing and tie -> sudden death
            var matchesField = typeof(GameManager).GetField("matches", BindingFlags.NonPublic | BindingFlags.Instance);
            var dictObj = matchesField?.GetValue(sut);
            var dictType = dictObj?.GetType();
            var indexer = dictType?.GetProperty("Item");
            var matchStateObj = indexer?.GetValue(dictObj, new object[] { "GAME9" });
            var startValidation = typeof(GameManager).GetMethod("StartValidationPhaseAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task?)startValidation?.Invoke(sut, new object[] { matchStateObj });

            // All four vote empty to finish validation quickly
            await sut.SubmitValidationVotesAsync("GAME9",1, new List<ValidationVoteDTO>());
            await sut.SubmitValidationVotesAsync("GAME9",2, new List<ValidationVoteDTO>());
            await sut.SubmitValidationVotesAsync("GAME9",3, new List<ValidationVoteDTO>());
            await sut.SubmitValidationVotesAsync("GAME9",4, new List<ValidationVoteDTO>());

            // Ensure sudden death started
            redClue.Verify(c => c.OnSuddenDeathStarted(), Times.Once);

            // Act: Red team guesses correct sudden-death word
            await sut.SubmitGuessAsync("GAME9",3, "WORD1");

            // Assert: Match over broadcast
            redClue.Verify(c => c.OnMatchOver(It.Is<MatchSummaryDTO>(s => s.WinnerTeam == MatchTeam.RedTeam || s.WinnerTeam == MatchTeam.BlueTeam)), Times.Once);
            blueClue.Verify(c => c.OnMatchOver(It.IsAny<MatchSummaryDTO>()), Times.Once);
            redGuess.Verify(c => c.OnMatchOver(It.IsAny<MatchSummaryDTO>()), Times.Once);
            blueGuess.Verify(c => c.OnMatchOver(It.IsAny<MatchSummaryDTO>()), Times.Once);
        }

        [Fact]
        public async Task PassTurnAsync_ShouldAdvanceWord_OncePerTeam_AndNotifyPartner()
        {
            // Arrange
            var (sut, queue) = CreateSut();
            var players = MakePlayers();
            sut.CreateMatch("GAME10", players);

            var redClue = new Mock<IGameManagerCallback>();
            var blueClue = new Mock<IGameManagerCallback>();
            var redGuess = new Mock<IGameManagerCallback>();
            var blueGuess = new Mock<IGameManagerCallback>();
            queue.Enqueue(redClue);
            queue.Enqueue(blueClue);
            queue.Enqueue(redGuess);
            queue.Enqueue(blueGuess);

            mockWordRepository.Setup(w => w.GetRandomWordsAsync(It.IsAny<int>())).ReturnsAsync(MakeWords());

            await sut.SubscribeToMatchAsync("GAME10",1);
            await sut.SubscribeToMatchAsync("GAME10",2);
            await sut.SubscribeToMatchAsync("GAME10",3);
            await sut.SubscribeToMatchAsync("GAME10",4);

            // Act: Red clue passes
            await sut.PassTurnAsync("GAME10", 1);

            // Assert: Red clue and guesser receive next password and pass notice to partner
            redClue.Verify(c => c.OnNewPassword(It.Is<PasswordWordDTO>(p => p.EnglishWord == "WORD2")), Times.AtLeastOnce);
            redGuess.Verify(c => c.OnNewPassword(It.Is<PasswordWordDTO>(p => p.EnglishDescription == "ED2")), Times.AtLeastOnce);
            redGuess.Verify(c => c.OnClueReceived(It.Is<string>(s => s.Contains("passed"))), Times.Once);

            // Second pass in same round should be ignored
            await sut.PassTurnAsync("GAME10", 1);
            redClue.Verify(c => c.OnNewPassword(It.Is<PasswordWordDTO>(p => p.EnglishWord == "WORD3")), Times.Never);
        }

        [Fact]
        public async Task PassTurnAsync_ShouldNoop_WhenInvalid()
        {
            var (sut, queue) = CreateSut();

            // No match
            await sut.PassTurnAsync("NOPE", 1);

            // Setup proper match
            var players = MakePlayers();
            sut.CreateMatch("GAME10b", players);
            var redClue = new Mock<IGameManagerCallback>();
            var blueClue = new Mock<IGameManagerCallback>();
            var redGuess = new Mock<IGameManagerCallback>();
            var blueGuess = new Mock<IGameManagerCallback>();
            queue.Enqueue(redClue);
            queue.Enqueue(blueClue);
            queue.Enqueue(redGuess);
            queue.Enqueue(blueGuess);
            mockWordRepository.Setup(w => w.GetRandomWordsAsync(It.IsAny<int>())).ReturnsAsync(MakeWords());
            await sut.SubscribeToMatchAsync("GAME10b",1);
            await sut.SubscribeToMatchAsync("GAME10b",2);
            await sut.SubscribeToMatchAsync("GAME10b",3);
            await sut.SubscribeToMatchAsync("GAME10b",4);

            // Guesser cannot pass
            await sut.PassTurnAsync("GAME10b", 3);
            redGuess.Verify(c => c.OnClueReceived(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Disconnection_DuringCallback_ShouldCancelMatch()
        {
            // Arrange
            var (sut, queue) = CreateSut();
            var players = MakePlayers();
            sut.CreateMatch("GAME11", players);

            var redClue = new Mock<IGameManagerCallback>();
            var blueClue = new Mock<IGameManagerCallback>();
            var redGuess = new Mock<IGameManagerCallback>();
            var blueGuess = new Mock<IGameManagerCallback>();

            // Simulate throw when sending initial password to red clue -> triggers disconnection handling and cancellation
            redClue.Setup(c => c.OnNewPassword(It.IsAny<PasswordWordDTO>())).Throws(new Exception("disconnect"));

            queue.Enqueue(redClue);
            queue.Enqueue(blueClue);
            queue.Enqueue(redGuess);
            queue.Enqueue(blueGuess);

            mockWordRepository.Setup(w => w.GetRandomWordsAsync(It.IsAny<int>())).ReturnsAsync(MakeWords());

            // Act
            await sut.SubscribeToMatchAsync("GAME11",1);
            await sut.SubscribeToMatchAsync("GAME11",2);
            await sut.SubscribeToMatchAsync("GAME11",3);
            await sut.SubscribeToMatchAsync("GAME11",4);

            // Assert: Others receive cancellation
            blueClue.Verify(c => c.OnMatchCancelled(It.Is<string>(s => s.Contains("disconnected"))), Times.AtLeastOnce);
            redGuess.Verify(c => c.OnMatchCancelled(It.IsAny<string>()), Times.AtLeastOnce);
            blueGuess.Verify(c => c.OnMatchCancelled(It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task PassTurnAsync_ShouldComplete()
        {
            // Arrange
            var (sut, queue) = CreateSut();

            // Act + Assert
            await sut.PassTurnAsync("ANY",1);

            // Arrange: provide a callback to ensure no unintended invocations occur for missing match
            var cb = new Mock<IGameManagerCallback>();
            queue.Enqueue(cb);

            // Act
            await sut.PassTurnAsync("ANY",1); // match does not exist -> noop

            // Assert: no callbacks should have been invoked
            cb.VerifyNoOtherCalls();
        }
    }
}