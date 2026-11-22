using LibraryApp.Data;

namespace LibraryApp.Forms;

// [17] Books screen: same pattern as members, with a copies counter.
public class BooksView : UserControl, IReloadable
{
    readonly TextBox _search = new() { PlaceholderText = "Search by title or author", Width = 260 };
    readonly DataGridView _grid = Ui.Grid();
    readonly TextBox _title = new() { Width = 200 }, _author = new() { Width = 150 };
    readonly NumericUpDown _copies = new() { Minimum = 1, Maximum = 1000, Value = 1, Width = 60 };
    // [17] Remembers which book is selected so Update knows which row to change.
    int? _selected;   // book_id of the row picked in the grid

    public BooksView()
    {
        _search.TextChanged += (_, _) => Reload();
        _grid.SelectionChanged += (_, _) => FillFromSelection();

        Controls.Add(_grid);
        Controls.Add(Ui.Bar(DockStyle.Bottom,
            Ui.Lbl("Title"), _title, Ui.Lbl("Author"), _author, Ui.Lbl("Copies"), _copies,
            Ui.Btn("Add", (_, _) => Add()), Ui.Btn("Update selected", (_, _) => UpdateSelected()), Ui.Btn("Clear", (_, _) => ClearFields())));
        Controls.Add(Ui.Bar(DockStyle.Top, _search));
    }

    public void Reload() => _grid.DataSource = BookRepository.Search(_search.Text.Trim());

    void FillFromSelection()
    {
        var row = _grid.CurrentRow;
        if (row == null || row.Cells["ID"].Value == null) return;
        _selected = Convert.ToInt32(row.Cells["ID"].Value);
        _title.Text = row.Cells["Title"].Value?.ToString();
        _author.Text = row.Cells["Author"].Value?.ToString();
        _copies.Value = Convert.ToDecimal(row.Cells["Total"].Value);
    }

    void ClearFields()
    {
        _grid.ClearSelection();
        _selected = null;
        _title.Clear(); _author.Clear(); _copies.Value = 1;
    }

    void Add()
    {
        if (_title.Text.Trim() == "") { Ui.Error("Please enter a title."); return; }
        try { BookRepository.Add(_title.Text.Trim(), _author.Text.Trim(), (int)_copies.Value); Reload(); }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }

    void UpdateSelected()
    {
        if (_selected == null) { Ui.Error("Select a book in the list first."); return; }
        if (_title.Text.Trim() == "") { Ui.Error("Please enter a title."); return; }
        try
        {
            if (!BookRepository.Update(_selected.Value, _title.Text.Trim(), _author.Text.Trim(), (int)_copies.Value))
                Ui.Error("Cannot set fewer copies than are currently out on loan.");
            Reload();
        }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }
}
