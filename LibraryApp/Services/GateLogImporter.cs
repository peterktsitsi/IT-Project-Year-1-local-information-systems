using LibraryApp.Data;
using LibraryApp.Models;

namespace LibraryApp.Services;

public record ImportResult(int NewRows, List<string> Errors);

// [10] Importer: the bridge between the gate CSV files and SQLite.
// Pulls each gate's CSV and imports only rows it has not seen before.
public static class GateLogImporter
{
    public static ImportResult ImportAll(IEnumerable<string> paths)
    {
        int total = 0;
        var errors = new List<string>();
        foreach (var path in paths)
        {
            // One unreachable gate must not stop the others from syncing.
            try { total += ImportFile(path); }
            catch (Exception ex) { errors.Add($"{Path.GetFileName(path)}: {ex.Message}"); }
        }
        return new ImportResult(total, errors);
    }

    // [10] Steps: read the file, skip the header and already-imported rows, insert the new rows,
    // then save progress. Everything happens in one transaction.
    static int ImportFile(string path)
    {
        var lines = ReadLinesShared(path);                       // the gate may be writing at the same moment
        var data = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();   // skip header

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        int done = GateEventRepository.GetImported(conn, tx, path);
        // [10] Fewer rows than we imported means the file was replaced, so start from the top.
        if (data.Count < done) done = 0;                         // file was replaced: start over (duplicates ignored)

        int added = 0, processed = done;
        for (int i = done; i < data.Count; i++)
        {
            var ev = GateEvent.TryParse(data[i]);
            // [10] A half-written last line is left for the next sync; a bad line in the middle is skipped for good.
            if (ev == null)
            {
                if (i == data.Count - 1) break;                  // last line may be half-written: retry next sync
                processed = i + 1;                               // bad line in the middle: skip it for good
                continue;
            }
            added += GateEventRepository.Insert(conn, tx, ev);
            processed = i + 1;
        }

        GateEventRepository.SetImported(conn, tx, path, processed);
        tx.Commit();
        return added;
    }

    // [10] FileShare.ReadWrite lets us read while the gate PC is still appending to the file.
    static List<string> ReadLinesShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);
        var lines = new List<string>();
        string? line;
        while ((line = sr.ReadLine()) != null) lines.Add(line);
        return lines;
    }
}
