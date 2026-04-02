using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class SqliteTickRepository : ITickRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteTickRepository> _logger;

    public SqliteTickRepository(
        IConfiguration configuration,
        ILogger<SqliteTickRepository> logger)
    {
     _connectionString = configuration.GetConnectionString("Default") ?? "Data Source=ticks.db";
     _logger = logger;
     CreateTableIfNotExists();   
    }

    private void CreateTableIfNotExists()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        connection.Execute("""
            CREATE TABLE IF NOT EXISTS normalized_ticks (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                source              TEXT NOT NULL,
                symbol              TEXT NOT NULL,
                price               TEXT NOT NULL,
                volume              TEXT NOT NULL,
                exchange_timestamp  TEXT NOT NULL,
                received_at         TEXT NOT NULL,
                dedup_key           TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_ticks_symbol_ts
                ON normalized_ticks(symbol, exchange_timestamp);
        """);

        _logger.LogInformation("Database initialized at {ConnectionString}", _connectionString);
    }

    public async Task SaveBatchAsync(IReadOnlyList<NormalizedTick> ticks, CancellationToken cancellationToken)
    {
        if (ticks.Count == 0)
            return;

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string sql = """
                INSERT INTO normalized_ticks
                    (source, symbol, price, volume, exchange_timestamp, received_at, dedup_key)
                VALUES
                    (@Source, @Symbol, @Price, @Volume, @ExchangeTimestamp, @ReceivedAt, @DeduplicationKey)
                """;

            foreach (var tick in ticks)
            {
                await connection.ExecuteAsync(sql, new
                {
                    Source = tick.Source.ToString(),
                    tick.Symbol,
                    Price = tick.Price.ToString(),
                    Volume = tick.Volume.ToString(),
                    ExchangeTimestamp = tick.ExchangeTimestamp.ToString("0"),
                    ReceivedAt = tick.ReceivedAt.ToString("0"),
                    tick.DeduplicationKey
                });    
            }

            await transaction.CommitAsync(cancellationToken);

            _logger.LogDebug("SAved batch to {Count} ticks", ticks.Count);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}