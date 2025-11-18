using MongoDB.Driver;
using StackExchange.Redis;
using Testcontainers.MsSql;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace TestHelpers
{
    /// <summary>
    /// Provides Docker test containers for integration tests using Testcontainers
    /// Implements IAsyncLifetime for proper setup/teardown with xUnit
    /// </summary>
    public class DatabaseFixture : IAsyncLifetime
    {
        // SQL Server container for Ordering service
        private MsSqlContainer? _msSqlContainer;
        
        // MongoDB container for Catalog service
        private MongoDbContainer? _mongoDbContainer;
        
        // PostgreSQL container for Discount service
        private PostgreSqlContainer? _postgreSqlContainer;
        
        // Redis container for Basket service
        private RedisContainer? _redisContainer;

        public string SqlServerConnectionString { get; private set; } = string.Empty;
        public string MongoDbConnectionString { get; private set; } = string.Empty;
        public string PostgreSqlConnectionString { get; private set; } = string.Empty;
        public string RedisConnectionString { get; private set; } = string.Empty;

        /// <summary>
        /// Initializes and starts all required database containers
        /// </summary>
        public async Task InitializeAsync()
        {
            // Start SQL Server container (for Ordering service)
            _msSqlContainer = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2019-latest")
                .WithPassword("YourStrong@Passw0rd")
                .Build();
            await _msSqlContainer.StartAsync();
            SqlServerConnectionString = _msSqlContainer.GetConnectionString();

            // Start MongoDB container (for Catalog service)
            _mongoDbContainer = new MongoDbBuilder()
                .WithImage("mongo:latest")
                .Build();
            await _mongoDbContainer.StartAsync();
            MongoDbConnectionString = _mongoDbContainer.GetConnectionString();

            // Start PostgreSQL container (for Discount service)
            _postgreSqlContainer = new PostgreSqlBuilder()
                .WithImage("postgres:latest")
                .WithUsername("admin")
                .WithPassword("admin1234")
                .WithDatabase("DiscountDb")
                .Build();
            await _postgreSqlContainer.StartAsync();
            PostgreSqlConnectionString = _postgreSqlContainer.GetConnectionString();

            // Start Redis container (for Basket service)
            _redisContainer = new RedisBuilder()
                .WithImage("redis:alpine")
                .Build();
            await _redisContainer.StartAsync();
            RedisConnectionString = _redisContainer.GetConnectionString();
        }

        /// <summary>
        /// Stops and disposes all database containers
        /// </summary>
        public async Task DisposeAsync()
        {
            if (_msSqlContainer != null)
            {
                await _msSqlContainer.StopAsync();
                await _msSqlContainer.DisposeAsync();
            }

            if (_mongoDbContainer != null)
            {
                await _mongoDbContainer.StopAsync();
                await _mongoDbContainer.DisposeAsync();
            }

            if (_postgreSqlContainer != null)
            {
                await _postgreSqlContainer.StopAsync();
                await _postgreSqlContainer.DisposeAsync();
            }

            if (_redisContainer != null)
            {
                await _redisContainer.StopAsync();
                await _redisContainer.DisposeAsync();
            }
        }

        /// <summary>
        /// Clears all data from the MongoDB test database
        /// </summary>
        public async Task ResetMongoDbAsync()
        {
            var client = new MongoClient(MongoDbConnectionString);
            await client.DropDatabaseAsync("CatalogTestDb");
        }

        /// <summary>
        /// Clears all data from Redis
        /// </summary>
        public async Task ResetRedisAsync()
        {
            var options = ConfigurationOptions.Parse(RedisConnectionString);
            options.AllowAdmin = true;
            var redis = await ConnectionMultiplexer.ConnectAsync(options);
            var server = redis.GetServer(redis.GetEndPoints()[0]);
            await server.FlushAllDatabasesAsync();
            await redis.DisposeAsync();
        }
    }

    /// <summary>
    /// Specific fixture for SQL Server (Ordering service)
    /// </summary>
    public class SqlServerFixture : IAsyncLifetime
    {
        private MsSqlContainer? _container;
        public string ConnectionString { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            _container = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2019-latest")
                .WithPassword("YourStrong@Passw0rd")
                .Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }

        public async Task DisposeAsync()
        {
            if (_container != null)
            {
                await _container.StopAsync();
                await _container.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Specific fixture for MongoDB (Catalog service)
    /// </summary>
    public class MongoDbFixture : IAsyncLifetime
    {
        private MongoDbContainer? _container;
        public string ConnectionString { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            _container = new MongoDbBuilder()
                .WithImage("mongo:latest")
                .Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }

        public async Task DisposeAsync()
        {
            if (_container != null)
            {
                await _container.StopAsync();
                await _container.DisposeAsync();
            }
        }

        /// <summary>
        /// Clears all data from the test database
        /// </summary>
        public async Task ResetDatabaseAsync()
        {
            var client = new MongoClient(ConnectionString);
            await client.DropDatabaseAsync("CatalogTestDb");
        }
    }

    /// <summary>
    /// Specific fixture for PostgreSQL (Discount service)
    /// </summary>
    public class PostgreSqlFixture : IAsyncLifetime
    {
        private PostgreSqlContainer? _container;
        public string ConnectionString { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:latest")
                .WithUsername("admin")
                .WithPassword("admin1234")
                .WithDatabase("DiscountDb")
                .Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }

        public async Task DisposeAsync()
        {
            if (_container != null)
            {
                await _container.StopAsync();
                await _container.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Specific fixture for Redis (Basket service)
    /// </summary>
    public class RedisFixture : IAsyncLifetime
    {
        private RedisContainer? _container;
        public string ConnectionString { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            _container = new RedisBuilder()
                .WithImage("redis:alpine")
                .Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }

        public async Task DisposeAsync()
        {
            if (_container != null)
            {
                await _container.StopAsync();
                await _container.DisposeAsync();
            }
        }

        /// <summary>
        /// Clears all data from Redis
        /// </summary>
        public async Task ResetDatabaseAsync()
        {
            var options = ConfigurationOptions.Parse(ConnectionString);
            options.AllowAdmin = true;
            var redis = await ConnectionMultiplexer.ConnectAsync(options);
            var server = redis.GetServer(redis.GetEndPoints()[0]);
            await server.FlushAllDatabasesAsync();
            await redis.DisposeAsync();
        }

        /// <summary>
        /// Creates an IDistributedCache instance for testing
        /// </summary>
        public Microsoft.Extensions.Caching.Distributed.IDistributedCache CreateDistributedCache()
        {
            var options = new Microsoft.Extensions.Options.OptionsWrapper<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>(
                new Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions
                {
                    Configuration = ConnectionString
                });
            return new Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache(options);
        }
    }
}

