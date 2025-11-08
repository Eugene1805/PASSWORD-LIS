using Data.DAL.Interfaces;
using Data.Model;
using Data.Util;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class WordRepository : IWordRepository
    {
        public async Task<List<PasswordWord>> GetRandomWordsAsync(int count)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
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

