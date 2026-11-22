using System.Data;
using LibraryApp.Models;

namespace LibraryApp.Data;

// [9] Repositories: one class per table, containing only SQL (no rules, no screens).
public static class MemberRepository
{
    public static DataTable Search(string text) => Database.Query(
        "SELECT member_id AS ID, name AS Name, phone AS Phone, status AS Status FROM Members " +
        "WHERE member_id LIKE @q OR name LIKE @q ORDER BY name", ("@q", $"%{text}%"));

    public static bool Exists(string id) =>
        Convert.ToInt32(Database.Scalar("SELECT COUNT(*) FROM Members WHERE member_id=@i", ("@i", id))) > 0;

    public static Member? Get(string id)
    {
        var t = Database.Query("SELECT member_id, name, phone, status FROM Members WHERE member_id=@i", ("@i", id));
        if (t.Rows.Count == 0) return null;
        var r = t.Rows[0];
        return new Member(r[0].ToString()!, r[1].ToString()!, r[2].ToString() ?? "", r[3].ToString()!);
    }

    public static void Add(Member m) => Database.Exec(
        "INSERT INTO Members(member_id,name,phone,status) VALUES(@i,@n,@p,@s)",
        ("@i", m.MemberId), ("@n", m.Name), ("@p", m.Phone), ("@s", m.Status));

    public static void Update(Member m) => Database.Exec(
        "UPDATE Members SET name=@n, phone=@p, status=@s WHERE member_id=@i",
        ("@i", m.MemberId), ("@n", m.Name), ("@p", m.Phone), ("@s", m.Status));
}
