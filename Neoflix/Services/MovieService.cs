using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neo4j.Driver;
using Neoflix.Example;
using Neoflix.Exceptions;

namespace Neoflix.Services
{
    public class MovieService
    {
        private readonly IDriver _driver;

        public MovieService(IDriver driver)
        {
            _driver = driver;
        }

        public async Task<Dictionary<string, object>[]> AllAsync(string sort = "title", 
            Ordering order = Ordering.Asc, int limit = 6, int skip = 0, string userId = null)
        {
            using var session = _driver.AsyncSession();

  
            return await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(@$"
                MATCH (m:Movie)
                WHERE m.{sort} IS NOT NULL
                RETURN m {{ .* }} AS movie
                ORDER BY m.{sort} {order.ToString("G").ToUpper()}
                SKIP $skip
                LIMIT $limit", new { skip, limit });

                var records = await cursor.ToListAsync();
                var movies = records
                    .Select(x => x["movie"].As<Dictionary<string, object>>())
                    .ToArray();


                return movies;
            });
        }
       
        public async Task<Dictionary<string, object>[]> GetByGenreAsync(string name, string sort = "title",
            Ordering order = Ordering.Asc, int limit = 6, int skip = 0, string userId = null)
        {
            return await Task.FromResult(Fixtures.Popular.Skip(skip).Take(limit).ToArray());
        }
       
        public async Task<Dictionary<string, object>[]> GetForActorAsync(string id, string sort = "title",
            Ordering order = Ordering.Asc, int limit = 6, int skip = 0, string userId = null)
        {
            return await Task.FromResult(Fixtures.Roles.Skip(skip).Take(limit).ToArray());
        }
       
        public async Task<Dictionary<string, object>[]> GetForDirectorAsync(string id, string sort = "title",
            Ordering order = Ordering.Asc, int limit = 6, int skip = 0, string userId = null)
        {
            return await Task.FromResult(Fixtures.Popular.Skip(skip).Take(limit).ToArray());
        }
        
        public async Task<Dictionary<string, object>> FindByIdAsync(string id, string userId = null)
        {
            return await Task.FromResult(Fixtures.Goodfellas);
        }
        
        public async Task<Dictionary<string, object>[]> GetSimilarMoviesAsync(string id, int limit, int skip)
        {
            var random = new Random();
            var exampleData = Fixtures.Popular
                .Skip(skip)
                .Take(limit)
                .Select(popularItem =>
                    popularItem.Concat(new[]
                        {
                            new KeyValuePair<string, object>("score", Math.Round(random.NextDouble() * 100, 2))
                        })
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
                .ToArray();
            return await Task.FromResult(exampleData);
        }

        private async Task<string[]> GetUserFavoritesAsync(IAsyncTransaction transaction, string userId)
        {
            if (userId == null)
                return new string[] { };
            var query = @"
                            MATCH (u:User {userId: $userId})-[:HAS_FAVORITE]->(m)
                            RETURN m.tmdbId as id";
            var cursor = await transaction.RunAsync(query, new { userId });
            var records = await cursor.ToListAsync();

            return records.Select(x => x["id"].As<string>()).ToArray();
        }
    }
}
