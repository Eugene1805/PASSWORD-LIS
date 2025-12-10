using Data.DAL.Implementations;
using Data.DAL.Interfaces;
using Data.Model;
using Effort;
using Moq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Linq;

namespace TestsEF.DataTests
{
    public abstract class DataTestsBase : IDisposable
    {
        protected readonly DbConnection Connection;
        protected readonly PasswordLISEntities Context;
        protected readonly Mock<IDbContextFactory> MockFactory;

        protected DataTestsBase()
        {
            Connection = EntityConnectionFactory.CreateTransient("name=PasswordLISEntities");
            Context = new PasswordLISEntities(Connection, true);

            MockFactory = new Mock<IDbContextFactory>();
            MockFactory.Setup(f => f.CreateDbContext()).Returns(Context);
        }

        protected PasswordLISEntities NewContext()
        {
            return new PasswordLISEntities(Connection, false);
        }
        protected IDbContextFactory CreatePerRequestFactory()
        {
            var factory = new Mock<IDbContextFactory>();
            factory.Setup(f => f.CreateDbContext())
                   .Returns(() => NewContext());
            return factory.Object;
        }

        protected AccountRepository CreateAccountRepository() => new AccountRepository(CreatePerRequestFactory());

        protected BanRepository CreateBanRepository() => new BanRepository(CreatePerRequestFactory());

        protected FriendshipRepository CreateFriendshipRepository() 
            => new FriendshipRepository(CreatePerRequestFactory());

        protected MatchRepository CreateMatchRepository() => new MatchRepository(CreatePerRequestFactory());

        protected PlayerRepository CreatePlayerRepository() => new PlayerRepository(CreatePerRequestFactory());

        protected ReportRepository CreateReportRepository() => new ReportRepository(CreatePerRequestFactory());

        protected StatisticsRepository CreateStatisticsRepository() 
            => new StatisticsRepository(CreatePerRequestFactory());

        protected WordRepository CreateWordRepository() => new WordRepository(CreatePerRequestFactory());

        protected TRepository CreateRepository<TRepository>(Func<IDbContextFactory, TRepository> repoFactory)
        {
            return repoFactory(CreatePerRequestFactory());
        }

        protected UserAccount CreateValidUser(string email = "test@test.com", string nickname = "TestUser")
        {
            return new UserAccount
            {
                Email = email,
                Nickname = nickname,
                PasswordHash = "dummy-hash-123456",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                EmailVerified = false,
                FirstName = "Test",
                LastName = "User",
                PhotoId = null,
                LastLoginAt = null
            };
        }

        protected void SeedPlayers(int count, string emailPrefix = "user", string nicknamePrefix = "User",
            Func<int, int> pointsSelector = null, bool emailVerified = true)
        {
            for (int i = 1; i <= count; i++)
            {
                var ua = CreateValidUser($"{emailPrefix}{i}@test.com", $"{nicknamePrefix}{i}");
                ua.EmailVerified = emailVerified;
                var player = new Player
                {
                    UserAccount = ua,
                    TotalPoints = pointsSelector != null ? pointsSelector(i) : 0
                };
                Context.UserAccount.Add(ua);
                Context.Player.Add(player);
            }
            Context.SaveChanges();
        }

        protected void SeedWords(int count)
        {
            using (var ctx = NewContext())
            {
                for (int i = 1; i <= count; i++)
                {
                    ctx.PasswordWord.Add(new PasswordWord
                    {
                        EnglishWord = "W" + i,
                        SpanishWord = "P" + i,
                        EnglishDescription = "ED" + i,
                        SpanishDescription = "SD" + i
                    });
                }
                ctx.SaveChanges();
            }
        }

        protected void SeedTeams(params (int points, int players)[] teams)
        {
            using (var ctx = NewContext())
            {
                var match = new Data.Model.Match { StartedAt = DateTime.UtcNow, EndedAt = DateTime.UtcNow };
                ctx.Match.Add(match);

                int userIndex = 1;
                foreach (var tp in teams)
                {
                    var team = new Team
                    {
                        Match = match,
                        TotalPoints = tp.points
                    };

                    for (int i = 0; i < tp.players; i++)
                    {
                        var ua = CreateValidUser($"u{userIndex}@test.com", $"User{userIndex}");
                        ua.EmailVerified = true;
                        var player = new Player { UserAccount = ua };
                        team.Player.Add(player);
                        userIndex++;
                    }

                    ctx.Team.Add(team);
                }

                ctx.SaveChanges();
            }
        }

        protected (int playerId, int userAccountId) GetPlayerByEmail(string email)
        {
            using (var ctx = NewContext())
            {
                var player = ctx.Player.Include("UserAccount").First(p => p.UserAccount.Email == email);
                return (player.Id, player.UserAccountId);
            }
        }

        protected int GetFirstPlayerId()
        {
            using (var ctx = NewContext())
            {
                return ctx.Player.OrderBy(p => p.Id).First().Id;
            }
        }

        protected List<int> GetFirstPlayerIds(int take)
        {
            using (var ctx = NewContext())
            {
                return ctx.Player.OrderBy(p => p.Id).Take(take).Select(p => p.Id).ToList();
            }
        }

        public virtual void Dispose()
        {
            Context?.Dispose();
            Connection?.Dispose();
        }
    }
}
