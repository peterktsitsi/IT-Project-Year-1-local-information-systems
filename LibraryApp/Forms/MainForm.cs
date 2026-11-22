using LibraryApp.Services;

namespace LibraryApp.Forms;

// [14] The shell: a toolbar with a sync button, plus one tab per screen.
public class MainForm : Form
{
    readonly AppSettings _settings;
    readonly PresenceView _presence = new() { Dock = DockStyle.Fill };
    readonly MembersView _members = new() { Dock = DockStyle.Fill };
    readonly BooksView _books = new() { Dock = DockStyle.Fill };
    readonly LoansView _loans;
    readonly ToolStripLabel _status = new("Not synced yet");
    readonly System.Windows.Forms.Timer _timer = new();
    readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    bool _syncing;

    public MainForm(AppSettings settings)
    {
        _settings = settings;
        _loans = new LoansView(settings) { Dock = DockStyle.Fill };

        Text = "Library - Librarian";
        Size = new Size(1000, 650);
        StartPosition = FormStartPosition.CenterScreen;

        AddTab("On premises", _presence);
        AddTab("Members", _members);
        AddTab("Books", _books);
        AddTab("Loans", _loans);
        // [14] Switching tabs reloads that screen so it always shows fresh data.
        _tabs.SelectedIndexChanged += (_, _) => (_tabs.SelectedTab?.Controls[0] as IReloadable)?.Reload();

        var tools = new ToolStrip();
        var sync = new ToolStripButton("Sync gate logs");
        sync.Click += async (_, _) => await SyncAsync();
        tools.Items.Add(sync);
        tools.Items.Add(_status);

        Controls.Add(_tabs);
        Controls.Add(tools);

        // [14] The automatic sync interval comes from settings.json.
        _timer.Interval = Math.Max(1, settings.SyncMinutes) * 60_000;   // automatic re-sync
        _timer.Tick += async (_, _) => await SyncAsync();
        _timer.Start();
        Shown += async (_, _) => await SyncAsync();
    }

    void AddTab(string title, Control content)
    {
        var page = new TabPage(title);
        page.Controls.Add(content);
        _tabs.TabPages.Add(page);
    }

    // [14] SyncAsync: import on a background thread, block overlapping syncs, then refresh the visible tab.
    // The import runs off the UI thread so a slow or offline gate never freezes the window.
    async Task SyncAsync()
    {
        if (_syncing) return;
        _syncing = true;
        _status.Text = "Syncing...";
        try
        {
            var r = await Task.Run(() => GateLogImporter.ImportAll(_settings.GateLogPaths));
            _status.Text = $"Last sync {DateTime.Now:HH:mm}: {r.NewRows} new event(s)" +
                (r.Errors.Count > 0 ? $" | problem: {string.Join("; ", r.Errors)}" : "");
            (_tabs.SelectedTab?.Controls[0] as IReloadable)?.Reload();
        }
        finally { _syncing = false; }
    }
}
