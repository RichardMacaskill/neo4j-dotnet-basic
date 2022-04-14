using System;
using Neo4j.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Basic_Example
{
    internal sealed class DriverIntroductionExample : IDisposable
    {
        private bool _disposed = false;
        private readonly IDriver _driver;

        DriverIntroductionExample() => Dispose(false);

        private DriverIntroductionExample(string uri, string user, string password)
        {
            _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        }

        private async Task CreateFriendship(string person1Name, string person2Name)
        {
            var query = @"
                 MERGE (p1:Person { name: $person1Name })
                 MERGE (p2:Person { name: $person2Name })
                 MERGE (p1)-[:KNOWS]->(p2)
                 RETURN p1, p2";

            await using var session = _driver.AsyncSession();

            try
            {
                // Write transactions allow the driver to handle retries and transient error
                var writeResults = await session.WriteTransactionAsync(async tx =>
                {
                    {
                        var result = await tx.RunAsync(query, new { person1Name, person2Name });
                        return (await result.ToListAsync());
                    }
                });

                foreach (var result in writeResults)
                {
                    var person1 = result["p1"].As<INode>().Properties["name"];
                    var person2 = result["p2"].As<INode>().Properties["name"];
                    Console.WriteLine($"Created friendship between: {person1}, {person2}");
                }
            }
            // Capture any errors along with the query and data for traceability
            catch (Neo4jException ex)
            {
                Console.WriteLine($"{query} - {ex}");
                throw;
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        private async Task FindPerson(string personName)
        {
            var query = @"
            MATCH (p:Person)
            WHERE p.name = $name
            RETURN p.name";


            var session = _driver.AsyncSession(build => build.WithImpersonatedUser("sue@cas-testlab-neo4j.co.uk"));
            try
            {
                IDictionary<string, Object> txMetadata = new Dictionary<string, object> { { "applicationId", "123" } };

                var readResults = await session.ReadTransactionAsync(async tx =>
                    {
                        var result = await tx.RunAsync(query, new { name = personName });
                        return (await result.ToListAsync());
                    }
                    , txBuilder => txBuilder.WithMetadata(txMetadata));

                foreach (var result in readResults)
                {
                    Console.WriteLine($"Found person: {result["p.name"].As<String>()}");
                }
            }

            // Capture any errors along with the query and data for traceability
            catch (Neo4jException ex)
            {
                Console.WriteLine($"{query} - {ex}");
                throw;
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _driver?.Dispose();
            }

            _disposed = true;
        }

        public static async Task Main(string[] args)
        {
            var uri = "neo4j+s://cas-testlab-neo4j.co.uk:7687";
            var user = "scratchuser";
            var password = Environment.GetEnvironmentVariable("NEO4J_PWD");

            var example = new DriverIntroductionExample(uri, user, password);
            await example.CreateFriendship("Alice", "David");
            await example.FindPerson("Alice");
        }
    }
}