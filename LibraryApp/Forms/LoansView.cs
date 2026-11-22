using LibraryApp.Data;
using LibraryApp.Services;

namespace LibraryApp.Forms;

// [18] Loans screen: collects inputs and shows results. LoanService decides if a loan is allowed.
// Issue and return books. Issuing is blocked unless the member is on the premises.
public class LoansView : UserControl, IReloadable
{
    readonly AppSettings _settings;
    readonly TextBox _member = new() { Width = 100, CharacterCasing = CharacterCasing.Upper };
    readonly Label _who = new() { AutoSize = true, Margin = new Padding(8, 8, 2, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    readonly ComboBox _book = new() { Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly DataGridView _grid = Ui.Grid();

    public LoansView(AppSettings settings)
    {
        _settings = settings;
        _member.TextChanged += (_, _) => ShowPresence();

        Controls.Add(_grid);
        Controls.Add(Ui.Bar(DockStyle.Bottom, Ui.Btn("Return selected book", (_, _) => ReturnSelected())));
        Controls.Add(Ui.Bar(DockStyle.Top,
            Ui.Lbl("Member ID"), _member, _who, Ui.Lbl("Book"), _book, Ui.Btn("Issue", (_, _) => Issue())));
    }

    public void Reload()
    {
        _book.DisplayMember = "Title";
        _book.ValueMember = "ID";
        _book.DataSource = BookRepository.Available();
        _grid.DataSource = LoanRepository.Active();
        ShowPresence();
    }

    // [18] Live status label: green if the member is on the premises, red if not or unknown.
    // Shows the librarian straight away whether the member may borrow.
    void ShowPresence()
    {
        var id = _member.Text.Trim();
        if (id == "") { _who.Text = ""; return; }
        var m = MemberRepository.Get(id);
        if (m == null) { _who.Text = "Unknown member"; _who.ForeColor = Color.Firebrick; return; }
        bool on = PresenceService.IsOnPremises(id);
        _who.Text = $"{m.Name}: {(on ? "ON premises" : "NOT on premises")}";
        _who.ForeColor = on ? Color.SeaGreen : Color.Firebrick;
    }

    void Issue()
    {
        if (_book.SelectedValue == null) { Ui.Error("No book selected (or none available)."); return; }
        var (ok, msg) = LoanService.Issue(_member.Text, Convert.ToInt32(_book.SelectedValue), _settings);
        if (ok) Ui.Info(msg); else Ui.Error(msg);
        Reload();
    }

    void ReturnSelected()
    {
        var row = _grid.CurrentRow;
        if (row == null || row.Cells["Loan"].Value == null) { Ui.Error("Select a loan in the list first."); return; }
        var (ok, msg) = LoanService.Return(Convert.ToInt32(row.Cells["Loan"].Value));
        if (ok) Ui.Info(msg); else Ui.Error(msg);
        Reload();
    }
}
