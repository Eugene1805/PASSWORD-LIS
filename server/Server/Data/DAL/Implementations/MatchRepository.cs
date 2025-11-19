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
        public MatchRepository(IDbContextFactory contextFactory)
        {
            this.contextFactory = contextFactory;
        }
        public async Task SaveMatchResultAsync(
            int redScore,
            int blueScore,
            IEnumerable<int> redTeamPlayerIds,
            IEnumerable<int> blueTeamPlayerIds)
        {
            if (redTeamPlayerIds == null) 
            {
                throw new ArgumentNullException(nameof(redTeamPlayerIds));
            }
            if (blueTeamPlayerIds == null)
            {
                throw new ArgumentNullException(nameof(blueTeamPlayerIds));
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

                    var allRegisteredPlayerIds = redTeamPlayerIds.Concat(blueTeamPlayerIds).Distinct().ToList();

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
                        TotalPoints = redScore
                    };

                    var blueTeamEntity = new Team
                    {
                        Match = newMatch,
                        TotalPoints = blueScore
                    };

                    foreach (var playerId in redTeamPlayerIds)
                    {
                        if (!playerById.TryGetValue(playerId, out Player player))
                        {
                            throw new InvalidOperationException($"Player with ID {playerId} not found.");
                        }
                        redTeamEntity.Player.Add(player);
                    }
                    foreach (var playerId in blueTeamPlayerIds)
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
