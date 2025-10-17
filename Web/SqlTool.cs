using System;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace AiWebsite.ApiService;

public sealed class SqlTool(ILogger<SqlTool> logger, SqliteConnection sqlite)
{
    [Description("Runs the provided SQL on the database. Returns the output as a structured table.")]
    public async Task<string> RunSql(string sql)
    {
        logger.LogDebug("Executing SQL: {Sql}", sql);

        if (sqlite.State is ConnectionState.Closed)
        {
            await sqlite.OpenAsync();
        }

        try
        {
            await using var command = sqlite.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync();
            if (!reader.HasRows)
            {
                return $"Rows affected {reader.RecordsAffected}";
            }
            if (!await reader.ReadAsync())
            {
                return "Could not read first row";
            }
            var sb = new StringBuilder();
            sb.AppendJoin(", ", Enumerable.Range(0, reader.FieldCount).Select(reader.GetName));
            sb.AppendLine();
            var row = Enumerable.Range(0, reader.FieldCount).Select(reader.GetString);
            do
            {
                sb.AppendJoin(", ", row);
                sb.AppendLine();
            } while (await reader.ReadAsync());
            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error when executing SQL");
            return ex.Message;
        }
    }
}
