using Data.DAL.Interfaces;
using Data.Model;
using Data.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class WordRepository : IWordRepository
    {
        private static readonly Random rand = new Random();
        public Task<List<PasswordWord>> GetRandomWordsAsync(int count)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                var words = context.PasswordWord
                                   .OrderBy(x => rand.Next()).Take(count).ToList();
                return Task.FromResult(words);
            }
        }
    }
}
