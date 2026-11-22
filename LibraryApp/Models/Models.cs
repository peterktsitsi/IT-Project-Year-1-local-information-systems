using System.Globalization;
using System.Text.RegularExpressions;

namespace LibraryApp.Models;

// [6] Models: plain data holders with no database or screen code.
public record Member(string MemberId, string Name, string Phone, string Status);
public record Book(int BookId, string Title, string Author, int CopiesTotal, int CopiesAvailable);
public record Loan(int LoanId, string MemberId, int BookId, string DateOut, string DateDue, string? DateReturned);

// [6] The ID rule is identical to the gate app, so scanned IDs always match registered members.
// Same ID rule as the gate app, so IDs scanned at the gate match registered members.
public static class Rules
{
    static readonly Regex Id = new(@"^[A-Za-z0-9_-]{1,20}$");
    public static bool IsValidMemberId(string s) => Id.IsMatch(s);
}

public record GateEvent(string Timestamp, string MemberId, string Direction, string GateId)
{
    // [6] TryParse is the importer's gatekeeper: a malformed row returns null instead of throwing.
    // Parses one CSV line: timestamp,member_id,direction,gate_id. Returns null if malformed.
    public static GateEvent? TryParse(string line)
    {
        var p = line.Split(',');
        if (p.Length != 4) return null;
        if (!DateTime.TryParse(p[0].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var ts)) return null;
        var id = p[1].Trim().ToUpperInvariant();
        var dir = p[2].Trim().ToUpperInvariant();
        if (!Rules.IsValidMemberId(id) || (dir != "IN" && dir != "OUT")) return null;
        return new GateEvent(ts.ToString("yyyy-MM-ddTHH:mm:ss"), id, dir, p[3].Trim().ToUpperInvariant());
    }
}
