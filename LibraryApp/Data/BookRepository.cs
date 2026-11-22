using System.Data;

namespace LibraryApp.Data;

public static class BookRepository
{
    public static DataTable Search(string text) => Database.Query(
        "SELECT book_id AS ID, title AS Title, author AS Author, copies_total AS Total, copies_available AS Available " +
        "FROM Books WHERE title LIKE @q OR author LIKE @q ORDER BY title", ("@q", $"%{text}%"));

    // Books that can be lent right now, for the loan screen.
    public static DataTable Available() => Database.Query(
        "SELECT book_id AS ID, title || ' - ' || author AS Title FROM Books WHERE copies_available > 0 ORDER BY title");

    public static void Add(string title, string author, int copies) => Database.Exec(
        "INSERT INTO Books(title,author,copies_total,copies_available) VALUES(@t,@a,@c,@c)",
        ("@t", title), ("@a", author), ("@c", copies));

    // [9] Update changes title, author and total copies in ONE statement, and refuses the
    // change if it would leave fewer copies than are currently on loan.
    // Changing the total adjusts the available count by the same amount.
    // Returns false if that would leave fewer copies than are currently out on loan.
    public static bool Update(int id, string title, string author, int copies) => Database.Exec(
        "UPDATE Books SET title=@t, author=@a, copies_available = copies_available + (@c - copies_total), copies_total=@c " +
        "WHERE book_id=@i AND copies_available + (@c - copies_total) >= 0",
        ("@i", id), ("@t", title), ("@a", author), ("@c", copies)) > 0;
}
