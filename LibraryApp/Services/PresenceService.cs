using System.Data;
using LibraryApp.Data;

namespace LibraryApp.Services;

// [11] Presence is calculated from events, never stored, so it cannot drift out of date.
// A member is on the premises if their latest event across ALL gates is IN.
public static class PresenceService
{
    public static DataTable OnPremises() => Database.Query(
        "SELECT e.member_id AS ID, COALESCE(m.name, '(not registered)') AS Name, " +
        "e.timestamp AS 'Came in', e.gate_id AS Gate " +
        "FROM GateEvents e LEFT JOIN Members m ON m.member_id = e.member_id " +
        "WHERE e.direction = 'IN' AND e.id = (SELECT id FROM GateEvents WHERE member_id = e.member_id " +
        "ORDER BY timestamp DESC, id DESC LIMIT 1) ORDER BY e.timestamp");

    // [11] The most recent event decides: IN means inside; OUT or no events means not.
    public static bool IsOnPremises(string memberId) =>
        Database.Scalar("SELECT direction FROM GateEvents WHERE member_id=@m ORDER BY timestamp DESC, id DESC LIMIT 1",
            ("@m", memberId))?.ToString() == "IN";
}
