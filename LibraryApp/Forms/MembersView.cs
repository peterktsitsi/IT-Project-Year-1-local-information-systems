using LibraryApp.Data;
using LibraryApp.Models;

namespace LibraryApp.Forms;

// [16] Members screen: search box, grid, and an edit bar with Add / Update / Clear.
public class MembersView : UserControl, IReloadable
{
    readonly TextBox _search = new() { PlaceholderText = "Search by ID or name", Width = 260 };
    readonly DataGridView _grid = Ui.Grid();
    readonly TextBox _id = new() { Width = 90 }, _name = new() { Width = 180 }, _phone = new() { Width = 110 };
    readonly ComboBox _status = new() { Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };

    public MembersView()
    {
        _status.Items.AddRange(new object[] { "Active", "Suspended" });
        _status.SelectedIndex = 0;

        _search.TextChanged += (_, _) => Reload();
        _grid.SelectionChanged += (_, _) => FillFromSelection();

        Controls.Add(_grid);
        Controls.Add(Ui.Bar(DockStyle.Bottom,
            Ui.Lbl("ID"), _id, Ui.Lbl("Name"), _name, Ui.Lbl("Phone"), _phone, Ui.Lbl("Status"), _status,
            Ui.Btn("Add", (_, _) => Save(true)), Ui.Btn("Update", (_, _) => Save(false)), Ui.Btn("Clear", (_, _) => ClearFields())));
        Controls.Add(Ui.Bar(DockStyle.Top, _search));
    }

    public void Reload() => _grid.DataSource = MemberRepository.Search(_search.Text.Trim());

    void FillFromSelection()
    {
        var row = _grid.CurrentRow;
        if (row == null || row.Cells["ID"].Value == null) return;
        _id.Text = row.Cells["ID"].Value?.ToString();
        _name.Text = row.Cells["Name"].Value?.ToString();
        _phone.Text = row.Cells["Phone"].Value?.ToString();
        _status.SelectedItem = row.Cells["Status"].Value?.ToString();
    }

    void ClearFields()
    {
        _grid.ClearSelection();
        _id.Clear(); _name.Clear(); _phone.Clear(); _status.SelectedIndex = 0;
    }

    // [16] Save validates first (ID rule, name), then adds or updates. Duplicates are checked before inserting.
    void Save(bool add)
    {
        var id = _id.Text.Trim().ToUpperInvariant();
        if (!Rules.IsValidMemberId(id)) { Ui.Error("ID: use letters, digits, - or _ (max 20 characters)."); return; }
        if (_name.Text.Trim() == "") { Ui.Error("Please enter a name."); return; }

        var m = new Member(id, _name.Text.Trim(), _phone.Text.Trim(), _status.Text);
        try
        {
            if (add)
            {
                if (MemberRepository.Exists(id)) { Ui.Error("That member ID already exists."); return; }
                MemberRepository.Add(m);
            }
            else
            {
                if (!MemberRepository.Exists(id)) { Ui.Error("No member with that ID. Use Add."); return; }
                MemberRepository.Update(m);
            }
            Reload();
        }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }
}
