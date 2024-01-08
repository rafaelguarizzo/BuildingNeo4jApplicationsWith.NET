using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neo4j.Driver;
using Neoflix.Exceptions;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Neoflix.Services
{
    public class AuthService
    {
        private readonly IDriver _driver;

        public AuthService(IDriver driver)
        {
            _driver = driver;
        }
        public async Task<Dictionary<string, object>> RegisterAsync(string email, string plainPassword, string name)
        {
            try
            {
                var rounds = Config.UnpackPasswordConfig();
                var encrypted = BCryptNet.HashPassword(plainPassword, rounds);

                await using var session = _driver.AsyncSession();

                var user = await session.ExecuteWriteAsync(async tx =>
                {
                    var query = @"
                    CREATE (u:User {
                        userId: randomUuid(),
                        email: $email,
                        password: $encrypted,
                        name: $name
                    })
                    RETURN u { .userId, .name, .email } as u";
                    var cursor = await tx.RunAsync(query, new { email, encrypted, name });

                    var record = await cursor.SingleAsync();
                    return record["u"].As<Dictionary<string, object>>();
                });

                var safeProperties = SafeProperties(user);
                safeProperties.Add("token", JwtHelper.CreateToken(GetUserClaims(safeProperties)));

                return safeProperties;
            }
            catch (ClientException exception)
            when (exception.Code == "Neo.ClientError.Schema.ConstraintValidationFailed")
            {
                throw new ValidationException(exception.Message, email);
            }

        }
        public async Task<Dictionary<string, object>> AuthenticateAsync(string email, string plainPassword)
        {
            await using var session = _driver.AsyncSession();
            var user = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync("MATCH (u: User {email: $email}) RETURN u", new { email });

                if (!await cursor.FetchAsync())
                    return null;


                var record = cursor.Current;
                var userProperties = record["u"].As<INode>().Properties;
                return userProperties.ToDictionary(x => x.Key, x => x.Value);
            });

            if (user == null)
                return null;

            if (!BCryptNet.Verify(plainPassword, user["password"].As<string>()))
                return null;

            var safeProperties = SafeProperties(user);
            safeProperties.Add("token", JwtHelper.CreateToken(GetUserClaims(safeProperties)));
            return safeProperties;
        }
        private static Dictionary<string, object> SafeProperties(Dictionary<string, object> user)
        {
            return user
                .Where(x => x.Key != "password")
                .ToDictionary(x => x.Key, x => x.Value);
        }

        private Dictionary<string, object> GetUserClaims(Dictionary<string, object> user)
        {
            return new Dictionary<string, object>
            {
                ["sub"] = user["userId"],
                ["userId"] = user["userId"],
                ["name"] = user["name"]
            };
        }

        public Dictionary<string, object> ConvertClaimsToRecord(Dictionary<string, object> claims)
        {
            return claims
                .Append(new KeyValuePair<string, object>("userId", claims["sub"]))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}
