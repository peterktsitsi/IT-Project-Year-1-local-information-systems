using LibraryApp.Services;

namespace LibraryApp.Forms;

// [15] Read-only screen: everyone currently on the premises, taken from PresenceService.
// Live list of who is inside, built from the imported gate events.
public class PresenceView : UserControl, IReloadable
{
    readonly DataGridView _grid = Ui.Grid();
    readonly Label _count = new() { Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };

    public PresenceView()
    {
        Controls.Add(_grid);
        Controls.Add(_count);
    }

    public void Reload()
    {
        var t = PresenceService.OnPremises();
        _grid.DataSource = t;
        _count.Text = $"  {t.Rows.Count} on the premises";
    }
}
