using LibraryApp.Data;

namespace LibraryApp.Services;

// [12] Loans: every lending rule lives here so the screens stay thin.
// The access rule lives here: books are only lent to members who are on the premises.
public static class LoanService
{
    // [12] Checks run in order: member exists, is Active, is on the premises, is under the loan limit.
    // Only then are the stock change and the loan row saved together.
    public static (bool Ok, string Message) Issue(string memberId, int bookId, AppSettings s)
    {
        memberId = memberId.Trim().ToUpperInvariant();
        var m = MemberRepository.Get(memberId);
        if (m == null) return (false, "Unknown member ID.");
        if (m.Status != "Active") return (false, $"{m.Name} is {m.Status}.");
        if (!PresenceService.IsOnPremises(memberId))
            return (false, $"{m.Name} is not on the premises according to the gate logs. Sync, or check the gate.");

        var count = Convert.ToInt32(Database.Scalar(
            "SELECT COUNT(*) FROM Loans WHERE member_id=@m AND date_returned IS NULL", ("@m", memberId)));
        if (count >= s.MaxLoansPerMember)
            return (false, $"{m.Name} already has {count} books out (limit {s.MaxLoansPerMember}).");

        var today = DateTime.Today;
        using var c = Database.Open();
        using var tx = c.BeginTransaction();   // both changes succeed or neither does

        // [12] "copies_available > 0" inside the UPDATE makes the stock check and decrement one atomic step.
        using (var dec = Database.Cmd(c, tx,
            "UPDATE Books SET copies_available = copies_available - 1 WHERE book_id=@b AND copies_available > 0", ("@b", bookId)))
        {
            if (dec.ExecuteNonQuery() == 0) return (false, "No copies of that book are available.");
        }
        var due = today.AddDays(s.LoanDays).ToString("yyyy-MM-dd");
        using (var ins = Database.Cmd(c, tx,
            "INSERT INTO Loans(member_id,book_id,date_out,date_due) VALUES(@m,@b,@o,@d)",
            ("@m", memberId), ("@b", bookId), ("@o", today.ToString("yyyy-MM-dd")), ("@d", due)))
        {
            ins.ExecuteNonQuery();
        }
        tx.Commit();
        return (true, $"Issued to {m.Name}. Due back {due}.");
    }

    // [12] Return stamps the loan as returned and puts the copy back in stock, in one transaction.
    public static (bool Ok, string Message) Return(int loanId)
    {
        using var c = Database.Open();
        using var tx = c.BeginTransaction();

        using var find = Database.Cmd(c, tx,
            "SELECT book_id FROM Loans WHERE loan_id=@l AND date_returned IS NULL", ("@l", loanId));
        var bookId = find.ExecuteScalar();
        if (bookId == null) return (false, "That loan was not found or is already returned.");

        using (var done = Database.Cmd(c, tx, "UPDATE Loans SET date_returned=@d WHERE loan_id=@l",
                   ("@d", DateTime.Today.ToString("yyyy-MM-dd")), ("@l", loanId)))
            done.ExecuteNonQuery();
        using (var inc = Database.Cmd(c, tx,
                   "UPDATE Books SET copies_available = copies_available + 1 WHERE book_id=@b", ("@b", bookId)))
            inc.ExecuteNonQuery();

        tx.Commit();
        return (true, "Book returned.");
    }
}
