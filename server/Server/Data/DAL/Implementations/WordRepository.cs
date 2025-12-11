using Data.DAL.Interfaces;
using Data.Model;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class WordRepository : IWordRepository
    {
        private readonly IDbContextFactory contextFactory;
        public WordRepository(IDbContextFactory ContextFactory)
        {
            this.contextFactory = ContextFactory;
        }
        public async Task<List<PasswordWord>> GetRandomWordsAsync(int count)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                var words = await context.PasswordWord
                    .OrderBy(w => Guid.NewGuid())
                    .Take(count)
                    .ToListAsync();
                
                return words;
            }
        }
     }
}

