namespace LibraryApp.Forms;

// [13] Every screen implements Reload(), so the main form can refresh whichever tab is showing.
public interface IReloadable { void Reload(); }

// [13] UI helpers: one place for the grid style, button builders and message boxes.
// Small helpers so every screen looks the same.
static class Ui
{
    public static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window
    };

    public static FlowLayoutPanel Bar(DockStyle dock, params Control[] items)
    {
        var p = new FlowLayoutPanel { Dock = dock, AutoSize = true, Padding = new Padding(6), WrapContents = true };
        p.Controls.AddRange(items);
        return p;
    }

    public static Label Lbl(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 8, 2, 0) };

    public static Button Btn(string text, EventHandler click)
    {
        var b = new Button { Text = text, AutoSize = true };
        b.Click += click;
        return b;
    }

    public static void Info(string msg) => MessageBox.Show(msg, "Library", MessageBoxButtons.OK, MessageBoxIcon.Information);
    public static void Error(string msg) => MessageBox.Show(msg, "Library", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
