using Data.DAL.Interfaces;
using Data.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.Entity;

namespace Data.DAL.Implementations
{
    public class MatchRepository : IMatchRepository
    {
        private readonly IDbContextFactory contextFactory;
        public MatchRepository(IDbContextFactory ContextFactory)
        {
            this.contextFactory = ContextFactory;
        }
        public async Task SaveMatchResultAsync(MatchResultData matchResultData)
        {
            if (matchResultData.RedTeamPlayerIds == null || matchResultData.BlueTeamPlayerIds == null) 
            {
                throw new ArgumentNullException(nameof(matchResultData));
            }

            using (var context = contextFactory.CreateDbContext())
            using (var transaction = context.Database.BeginTransaction())
            {
                try
                {
                    var newMatch = new Match
                    {
                        StartedAt = DateTime.UtcNow,
                        EndedAt = DateTime.UtcNow
                    };
                    context.Match.Add(newMatch);

                    var allRegisteredPlayerIds = matchResultData.RedTeamPlayerIds
                        .Concat(matchResultData.BlueTeamPlayerIds).Distinct().ToList();

                    var playersFromDb = await context.Player
                       .Where(p => allRegisteredPlayerIds.Contains(p.Id))
                       .ToListAsync();

                    if (playersFromDb.Count != allRegisteredPlayerIds.Count)
                    {
                        var foundIds = new HashSet<int>(playersFromDb.Select(p => p.Id));
                        var missing = allRegisteredPlayerIds.Where(id => !foundIds.Contains(id)).ToList();
                        throw new InvalidOperationException($"Some player IDs were not found: " +
                            $"{string.Join(", ", missing)}");
                    }

                    var playerById = playersFromDb.ToDictionary(p => p.Id);

                    var redTeamEntity = new Team
                    {
                        Match = newMatch,
                        TotalPoints = matchResultData.RedScore
                    };

                    var blueTeamEntity = new Team
                    {
                        Match = newMatch,
                        TotalPoints = matchResultData.BlueScore
                    };

                    foreach (var playerId in matchResultData.RedTeamPlayerIds)
                    {
                        if (!playerById.TryGetValue(playerId, out Player player))
                        {
                            throw new InvalidOperationException($"Player with ID {playerId} not found.");
                        }
                        redTeamEntity.Player.Add(player);
                    }
                    foreach (var playerId in matchResultData.BlueTeamPlayerIds)
                    {
                        if (!playerById.TryGetValue(playerId, out Player player))
                        {
                            throw new InvalidOperationException($"Player with ID {playerId} not found.");
                        }
                        blueTeamEntity.Player.Add(player);
                    }

                    context.Team.AddRange(new[] { redTeamEntity, blueTeamEntity });

                    await context.SaveChangesAsync();

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }
}
