using System.Data;

namespace LibraryApp.Data;

public static class LoanRepository
{
    // [9] An active loan is a row with no return date.
    public static DataTable Active() => Database.Query(
        "SELECT l.loan_id AS Loan, l.member_id AS Member, m.name AS Name, b.title AS Book, " +
        "l.date_out AS Out, l.date_due AS Due " +
        "FROM Loans l JOIN Members m ON m.member_id = l.member_id JOIN Books b ON b.book_id = l.book_id " +
        "WHERE l.date_returned IS NULL ORDER BY l.date_due");
}
