using System.Data;
using Microsoft.Data.Sqlite;

namespace LibraryApp.Data;

// [7] Database access: every query in the app goes through this class.
// The only place that opens SQLite. The .db file lives on the librarian PC only.
public static class Database
{
    static string _cs = "";

    // [8] Schema: five tables created on first run. IF NOT EXISTS makes it safe to run on every start.
    const string Schema = @"
CREATE TABLE IF NOT EXISTS Members (
    member_id TEXT PRIMARY KEY, name TEXT NOT NULL,
    phone TEXT NOT NULL DEFAULT '', status TEXT NOT NULL DEFAULT 'Active');
CREATE TABLE IF NOT EXISTS Books (
    book_id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT NOT NULL, author TEXT NOT NULL DEFAULT '',
    copies_total INTEGER NOT NULL, copies_available INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS Loans (
    loan_id INTEGER PRIMARY KEY AUTOINCREMENT,
    member_id TEXT NOT NULL REFERENCES Members(member_id),
    book_id INTEGER NOT NULL REFERENCES Books(book_id),
    date_out TEXT NOT NULL, date_due TEXT NOT NULL, date_returned TEXT);
CREATE TABLE IF NOT EXISTS GateEvents (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp TEXT NOT NULL, member_id TEXT NOT NULL, direction TEXT NOT NULL, gate_id TEXT NOT NULL,
    UNIQUE (timestamp, member_id, direction, gate_id));
CREATE TABLE IF NOT EXISTS ImportState (
    file_name TEXT PRIMARY KEY, rows_imported INTEGER NOT NULL);
CREATE INDEX IF NOT EXISTS ix_events_member ON GateEvents(member_id, timestamp);";

    public static void Initialize(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _cs = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        Exec(Schema);
    }

    // [7] A new short-lived connection per call. Foreign keys are switched on each time
    // because SQLite turns them off by default.
    public static SqliteConnection Open()
    {
        var c = new SqliteConnection(_cs);
        c.Open();
        using var pragma = c.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return c;
    }

    // [7] Every value is passed as a named parameter (@name), never joined into the SQL text.
    // This is what prevents SQL injection.
    public static SqliteCommand Cmd(SqliteConnection c, SqliteTransaction? tx, string sql, params (string, object?)[] p)
    {
        var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in p) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    public static int Exec(string sql, params (string, object?)[] p)
    {
        using var c = Open(); using var cmd = Cmd(c, null, sql, p);
        return cmd.ExecuteNonQuery();
    }

    public static object? Scalar(string sql, params (string, object?)[] p)
    {
        using var c = Open(); using var cmd = Cmd(c, null, sql, p);
        return cmd.ExecuteScalar();
    }

    // [7] Query returns a table that a grid can display directly.
    // Returns a DataTable for grids. Columns are typed as object to avoid SQLite type surprises.
    public static DataTable Query(string sql, params (string, object?)[] p)
    {
        using var c = Open(); using var cmd = Cmd(c, null, sql, p);
        using var r = cmd.ExecuteReader();
        var t = new DataTable();
        for (int i = 0; i < r.FieldCount; i++) t.Columns.Add(r.GetName(i), typeof(object));
        while (r.Read())
        {
            var row = new object[r.FieldCount];
            r.GetValues(row);
            t.Rows.Add(row);
        }
        return t;
    }
}
