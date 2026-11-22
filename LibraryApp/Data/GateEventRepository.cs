using LibraryApp.Models;
using Microsoft.Data.Sqlite;

namespace LibraryApp.Data;

public static class GateEventRepository
{
    // [10] The second line of defence against duplicate imports (the first is ImportState).
    // INSERT OR IGNORE + the UNIQUE constraint is a second guard against duplicate imports.
    public static int Insert(SqliteConnection c, SqliteTransaction tx, GateEvent e)
    {
        using var cmd = Database.Cmd(c, tx,
            "INSERT OR IGNORE INTO GateEvents(timestamp,member_id,direction,gate_id) VALUES(@t,@m,@d,@g)",
            ("@t", e.Timestamp), ("@m", e.MemberId), ("@d", e.Direction), ("@g", e.GateId));
        return cmd.ExecuteNonQuery();
    }

    // [10] ImportState remembers how many data rows of each gate file are already imported.
    public static int GetImported(SqliteConnection c, SqliteTransaction tx, string file)
    {
        using var cmd = Database.Cmd(c, tx, "SELECT rows_imported FROM ImportState WHERE file_name=@f", ("@f", file));
        var v = cmd.ExecuteScalar();
        return v == null ? 0 : Convert.ToInt32(v);
    }

    public static void SetImported(SqliteConnection c, SqliteTransaction tx, string file, int rows)
    {
        using var cmd = Database.Cmd(c, tx,
            "INSERT INTO ImportState(file_name,rows_imported) VALUES(@f,@n) " +
            "ON CONFLICT(file_name) DO UPDATE SET rows_imported=@n", ("@f", file), ("@n", rows));
        cmd.ExecuteNonQuery();
    }
}
