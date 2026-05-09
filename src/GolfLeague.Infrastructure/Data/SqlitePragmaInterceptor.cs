using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GolfLeague.Infrastructure.Data;

/// <summary>
/// Sets PRAGMAs on every newly opened SQLite connection. journal_mode=DELETE
/// makes journal handling explicit and avoids any chance of WAL/SHM sidecar
/// files being out of sync after the main file is replaced from blob storage.
/// foreign_keys=ON enforces referential integrity at the SQLite level.
/// </summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragmas(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ApplyPragmas(connection);
        return base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void ApplyPragmas(DbConnection connection)
    {
        // foreign_keys is per-connection and must be set every open. We don't
        // touch journal_mode — it defaults to DELETE on SQLite and changing it
        // mid-transaction can fail; the default is what we want.
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
    }
}
