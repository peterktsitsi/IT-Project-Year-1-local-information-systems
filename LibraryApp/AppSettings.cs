using System.Text.Json;

namespace LibraryApp;

// Stored in %AppData%\LibraryApp\settings.json. Edit that file to change gate paths.
// [5] Settings: values the librarian can change without recompiling.
public class AppSettings
{
    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LibraryApp");

    // [5] Gate paths are UNC paths to each gate PC's read-only shared CSV.
    // One entry per gate PC: the shared log file, read-only, on VLAN 10.
    public List<string> GateLogPaths { get; set; } = new()
    {
        @"\\192.168.10.114\logs\gate1_log.csv",
        @"\\192.168.10.115\logs\gate2_log.csv"
    };
    public string DbPath { get; set; } = Path.Combine(Folder, "library.db");
    public int LoanDays { get; set; } = 14;
    public int MaxLoansPerMember { get; set; } = 3;
    public int SyncMinutes { get; set; } = 5;

    // [5] Load: read settings.json if it exists, otherwise create it with defaults.
    // A damaged file falls back to defaults but is never overwritten.
    public static AppSettings Load()
    {
        Directory.CreateDirectory(Folder);
        var file = Path.Combine(Folder, "settings.json");
        if (File.Exists(file))
        {
            try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file)) ?? new(); }
            catch { return new(); }   // unreadable file: use defaults, do not overwrite it
        }
        var s = new AppSettings();
        File.WriteAllText(file, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        return s;
    }
}
