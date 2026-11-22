# IT-Project-Year-1-local-information-systems

# Gate Tracker Explainer

This document explains `gate_tracker.py`, the Python CLI that runs on the library's gate PCs. Each section number matches a `# [n]` comment in the code, so you can jump from a line of code to its explanation and back.

## [1] Purpose and big picture

The gate PCs record who enters and leaves the library. The app does one job: **write a reliable log of IN and OUT events to a local CSV file.**

It does not decide who is inside the building, and it does not talk to the librarian's database. The librarian PC pulls the CSV files later and works that out.

```
Gate PC (this app)  ->  gate1_log.csv  --pulled by-->  Librarian PC  ->  SQLite
```

Keeping the gate app this simple is deliberate. A simple program has fewer ways to fail, and the gate is the busiest, most public machine in the library.

## [2] One file per gate

Each gate writes its own file: `gate1_log.csv`, `gate2_log.csv`, and so on. The file name comes from the gate ID (`log_path`).

**Why not one shared file?** Two PCs appending to the same file over a network share can interleave or corrupt rows, and if the share goes down the gates stop working. With one file per gate, each PC is the only writer of its own file. Nothing can collide, and the gate keeps working even if the network is down.

## [3] CSV format

Every row has four columns, defined in `FIELDS`:

| Column | Example | Meaning |
|---|---|---|
| `timestamp` | `2026-11-18T13:17:12` | When the scan happened (ISO 8601) |
| `member_id` | `M001` | Who was scanned |
| `direction` | `IN` or `OUT` | Which way they went |
| `gate_id` | `GATE1` | Which gate recorded it |

ISO timestamps sort correctly as plain text and are easy for the librarian app to import. Recording `gate_id` on every row means the rows still make sense after files from different gates are merged.

## [4] Command-line options

One script serves every gate. Each PC is configured by the options it is started with (`parse_args`):

| Option | Default | Purpose |
|---|---|---|
| `--gate-id` | `GATE1` | Unique name of this gate. Also decides the log file name |
| `--direction` | `both` | `in`, `out` or `both` (see section 5) |
| `--log-dir` | `logs` | Folder holding the CSV. Point it at the folder you share read-only |

Examples:

```
python gate_tracker.py --gate-id GATE1 --direction in
python gate_tracker.py --gate-id GATE2 --direction out --log-dir C:\library\logs
```

## [5] Why the app never guesses IN or OUT

An obvious idea is to toggle automatically: if the last event for a member was IN, the next one must be OUT. This fails with more than one gate. A member can enter at Gate 1 and leave at Gate 2, but Gate 2 cannot see Gate 1's file, so it would guess wrong.

So the direction is never guessed. It is set one of two ways:

- **Fixed gate** (`--direction in` or `out`): the gate's role decides. An entrance PC always writes IN, an exit PC always writes OUT.
- **Both** (`--direction both`): the attendant is asked "IN or OUT?" on every scan (`ask_direction`). Typing `c` cancels, and nothing is recorded.

Working out who is currently inside is left to the librarian app, which sees every gate's file at once. A member is on the premises if their latest event across all gates is IN.

## [6] Creating the log

At start-up, `ensure_log` makes sure the log file exists:

1. It creates the folder if it is missing.
2. It writes the header row **only if the file is new or empty.**

An existing file is never overwritten, so restarting the app or rebooting the PC never erases earlier data.

## [7] Writing an event

`append_event` adds one row to the end of the file. Two details matter:

- **Append mode (`"a"`).** Earlier rows are never touched.
- **`flush()` then `os.fsync()`.** `flush()` pushes Python's buffer to the operating system. `fsync()` pushes the operating system's buffer to the disk itself. Without both, the OS may hold the row in memory for a while, and a power cut would lose recent scans. With them, a power cut loses at most the scan that was in progress.

The function returns the timestamp so the screen can show exactly what was saved.

## [8] ID validation

Before anything is written, the ID is converted to uppercase and checked against `MEMBER_ID_PATTERN`: letters, digits, `-` and `_`, from 1 to 20 characters.

- Uppercasing means `m001` and `M001` count as the same member.
- The pattern keeps junk out of the log: commas, quotes, spaces and typos that would make later imports fail or create "ghost" members.

An invalid ID is rejected with a message and nothing is saved.

## [9] Duplicate-scan guard

Scanners and nervous fingers produce double scans. If the same member, with the same direction, was recorded less than `DUPLICATE_WINDOW_SECONDS` (5) ago, the new scan is ignored and the attendant is told.

Only the same direction is blocked. A member who scans IN and then OUT 3 seconds later is accepted, because that is a valid sequence of events.

If the stored timestamp cannot be read, the code assumes it is old and accepts the scan. Losing a real event is worse than keeping a duplicate.

## [10] Reading helpers

Two small functions read the CSV back:

- `read_recent` returns the last 10 rows for the `/recent` command.
- `last_event_for` finds the newest row for one member in this gate's file, for the duplicate guard in section 9.

Both read the whole file. That is fine for a village library, where a gate file holds thousands of rows rather than millions. If the library grows a lot, the guard could remember recent scans in memory instead.

## [11] Main loop and commands

`main` runs a loop: read a line, decide whether it is a command or a member ID, act on it, repeat.

| Input | What it does |
|---|---|
| A member ID | Validates it, picks the direction, checks for a double scan, writes the row |
| `/recent` | Shows the last 10 events at this gate |
| `/help` | Lists the commands |
| `/quit` | Exits the program |
| Empty line | Ignored |

Anything that starts with `/` is treated as a command, never as an ID. `Ctrl+C` or `Ctrl+D` also exits cleanly instead of showing a crash message.

## [12] Error handling

There are two points where the app can fail, and each is handled so the attendant always knows what happened:

- **At start-up.** If the log file cannot be created (wrong path, no permission), the app prints a clear message and stops. This is better than discovering the problem in the middle of a busy morning.
- **When writing.** If a write fails (disk full, file locked), the app prints that the event was **not saved** and keeps running, so the attendant can write it down by hand and try again.

## [13] How it fits the network design

- The log folder is shared **read-only** on VLAN 10 (Gate).
- The librarian PC in VLAN 20 starts the connection and pulls each gate's CSV. The gate never connects out to the librarian.
- The ACL on the gate VLAN only allows replies to that pull, so a compromised gate PC cannot reach the librarian's database.
- If the network is down, the gate keeps writing locally. The librarian catches up on the next sync.

## [14] Testing checklist

| Test | Expected result |
|---|---|
| Start a gate for the first time | Folder and file are created with the header row |
| Scan `m001`, choose `i` | `M001,IN` row appears in the CSV |
| Scan `m001` as IN again straight away | "Ignored" message, no new row |
| Enter `bad id!` | "Invalid ID" message, no new row |
| Choose `c` at the direction prompt | "Cancelled", no new row |
| Run with `--direction out` | No prompt, rows are written as OUT |
| `/recent` | Shows the latest rows |
| Restart the app | Old rows are still there, no second header |
| Unplug the network, scan a member | Row is still written locally |

---

# LibraryApp Explainer

This document explains the librarian application for the Raa community library: a Windows Forms program backed by SQLite. Each section number matches a `// [n]` comment in the code, so you can jump from a line of code to its explanation and back. Sections without a code comment cover the wider picture.

---

## [1] Purpose and big picture

The librarian app is the **brain of the library**. It runs on one PC, the main librarian computer, and does four jobs:

1. **Collects** the entry and exit logs written by the gate PCs.
2. **Works out** who is on the premises right now.
3. **Manages** members, books and loans.
4. **Enforces one rule:** a book is only issued to an active member who is currently inside the library.

```
 Gate PC 1 ─┐                                   ┌─ Members
            ├─ CSV logs ◄─ pulled by ─ LibraryApp ┼─ Books and loans
 Gate PC 2 ─┘  (one per gate)         (this app)  └─ Gate events
                                                      │
                                                  SQLite file
                                              (on this PC only)
```

The gate app (see the gate tracker explainer) only writes CSV files. It knows nothing about members or books. This app does all the thinking, which keeps the busy, public-facing gate PCs simple.

---

## [2] Technology choices

| Choice | Why |
|---|---|
| **C# on .NET (Windows Forms)** | A desktop app suits a single librarian PC. Windows Forms is quick to build and easy to explain |
| **SQLite** | A full database in one file with no server to install or maintain. Ideal for one PC |
| **Microsoft.Data.Sqlite** | The official, lightweight driver. No heavy ORM, so every SQL statement is visible and explainable |
| **UI built in code** | No `.Designer.cs` files, so the whole screen layout is readable in one place and works in any editor |
| **Records for models** | Short, immutable data classes with no boilerplate |
| **Settings in JSON** | The librarian can change gate paths without recompiling |

SQLite is used on **one PC only**. It is a file database, and opening the same file from several computers over a network share is unreliable. That is why the gates send CSV files to the librarian rather than writing into the database.

---

## [3] Project structure and layers

```
LibraryApp/
├── Program.cs          Entry point
├── AppSettings.cs      Settings loaded from settings.json
├── Models/             Plain data (Member, Book, Loan, GateEvent) and the ID rule
├── Data/               The ONLY code that talks to SQLite
├── Services/           The library's rules (import, presence, loans)
└── Forms/              Screens only, no SQL
```

The layers only depend **downwards**:

```
Forms  ->  Services  ->  Data  ->  SQLite
              all layers may use Models
```

- A form never writes SQL. It asks a service or repository.
- A repository never opens a window or applies library rules.
- Rules such as "must be on the premises" live in exactly one place (`LoanService`).

**Why this matters:** each part has one job, so a change (for example a new loan limit) touches one file, and each piece can be explained and tested on its own.

---

## [4] Startup (`Program.cs`)

On launch, `Main` does three things in order:

1. `AppSettings.Load()` reads the settings (section 5).
2. `Database.Initialize(...)` creates the database file and tables if they do not exist (sections 7 and 8).
3. `Application.Run(new MainForm(settings))` opens the main window.

All three are inside a `try/catch`. If anything fails (for example the AppData folder is not writable), the user sees a clear message box instead of a crash with no explanation.

`[STAThread]` is required by Windows Forms: it makes the UI thread a single-threaded apartment, which Windows UI components need.

---

## [5] Settings (`AppSettings.cs`)

Settings are stored in `%AppData%\LibraryApp\settings.json`, created with defaults on first run.

| Setting | Default | Meaning |
|---|---|---|
| `GateLogPaths` | Two UNC paths, e.g. `\\192.168.10.114\logs\gate1_log.csv` | One path per gate CSV |
| `DbPath` | `%AppData%\LibraryApp\library.db` | Where the database file lives |
| `LoanDays` | 14 | Days until a book is due |
| `MaxLoansPerMember` | 3 | Books a member may hold at once |
| `SyncMinutes` | 5 | How often the gate logs are re-imported |

**Why a JSON file and not constants in the code?** The gate paths are different on every installation, and the librarian should not need a developer to change them.

**Loading rules:**

- If the file exists, it is read.
- If it is damaged, the app uses defaults **but does not overwrite the file**, so a typo does not destroy the librarian's configuration.
- If it does not exist, it is created with the defaults and indented so it is easy to edit by hand.

The database stays in AppData, not inside the project folder, so rebuilding or moving the program never touches the library's data.

---

## [6] Models (`Models/Models.cs`)

Models are plain data holders:

| Record | Holds |
|---|---|
| `Member` | ID, name, phone, status (Active or Suspended) |
| `Book` | ID, title, author, copies total, copies available |
| `Loan` | Loan ID, member, book, date out, date due, date returned |
| `GateEvent` | Timestamp, member ID, direction (IN or OUT), gate ID |

They contain no database or screen code. A `record` gives short syntax and value-based equality.

**`Rules.IsValidMemberId`** is the member ID rule: letters, digits, `-` and `_`, 1 to 20 characters. It is the **same rule the gate app uses**, so an ID scanned at the gate always matches an ID registered here.

**`GateEvent.TryParse`** turns one CSV line into an event. It returns `null` instead of throwing an error when the line is bad, which makes it the importer's gatekeeper. A line is rejected if it:

- does not have exactly four fields,
- has a timestamp that cannot be read,
- has an invalid member ID, or
- has a direction other than IN or OUT.

Accepted lines are normalised: the timestamp is rewritten in one fixed format and IDs are uppercased, so the database always holds consistent text.

---

## [7] Database access (`Data/Database.cs`)

This class is the **only place that opens SQLite.** Everything else calls its helpers.

| Method | Purpose |
|---|---|
| `Initialize(path)` | Creates the folder, stores the connection string, runs the schema |
| `Open()` | Opens a new connection and switches foreign keys on |
| `Cmd(...)` | Builds a command with parameters |
| `Exec(...)` | Runs INSERT, UPDATE or DELETE and returns the rows affected |
| `Scalar(...)` | Returns a single value (such as a COUNT) |
| `Query(...)` | Returns a table of rows that a grid can display |

**Key design decisions**

- **A new short-lived connection for every call.** SQLite connections are cheap, and this avoids a long-lived connection being shared between the UI thread and the background sync thread.
- **`PRAGMA foreign_keys = ON` on every connection.** SQLite ignores foreign keys by default and the setting does not persist. Without it, a loan could point to a member who does not exist.
- **Parameters, never string joining.** Every value goes in as `@name`:

  ```csharp
  Database.Exec("UPDATE Members SET name=@n WHERE member_id=@i", ("@n", name), ("@i", id));
  ```

  Because the values are never pasted into the SQL text, a member named `Robert'); DROP TABLE Members;--` is stored as a name and does nothing. This is how SQL injection is prevented.
- **`Query` builds the table by hand,** with every column typed as `object`. SQLite is loosely typed, and letting a `DataTable` guess types from the first row can throw errors on empty or mixed columns. Using `object` avoids that.

---

## [8] Database schema

Five tables are created on first run, using `CREATE TABLE IF NOT EXISTS` so the step is safe to repeat on every launch.

| Table | Columns | Notes |
|---|---|---|
| **Members** | `member_id` (PK), `name`, `phone`, `status` | Status is Active or Suspended |
| **Books** | `book_id` (PK, auto), `title`, `author`, `copies_total`, `copies_available` | Two counts so stock is never recalculated |
| **Loans** | `loan_id` (PK, auto), `member_id`, `book_id`, `date_out`, `date_due`, `date_returned` | `date_returned` is empty while the book is out |
| **GateEvents** | `id` (PK, auto), `timestamp`, `member_id`, `direction`, `gate_id` | UNIQUE on all four data columns |
| **ImportState** | `file_name` (PK), `rows_imported` | Remembers import progress per gate file |

```
Members 1 ──── * Loans * ──── 1 Books

GateEvents  (member_id matches a Member but is NOT a foreign key)
ImportState (bookkeeping for the importer)
```

**Why these choices**

- **Dates are stored as text in ISO format** (`2026-11-18` or `2026-11-18T13:17:12`). SQLite has no date type, and ISO text sorts in the correct order, so `ORDER BY timestamp` just works.
- **`GateEvents.member_id` is not a foreign key.** Someone can scan at the gate before being registered. The event must still be recorded, and the presence screen shows them as "(not registered)".
- **The UNIQUE constraint on GateEvents** stops the same event being stored twice, even if an import is somehow repeated.
- **An index on `(member_id, timestamp)`** keeps the "latest event for this member" lookup fast as the log grows.
- **`copies_available` is stored, not calculated.** It is updated inside the same transaction as the loan, so the two numbers cannot disagree.

---

## [9] Repositories (`Data/*Repository.cs`)

One repository per table. They contain **only SQL**, with no rules and no screens.

| Repository | What it offers |
|---|---|
| `MemberRepository` | Search, Exists, Get, Add, Update |
| `BookRepository` | Search, Available (for the loan drop-down), Add, Update |
| `LoanRepository` | Active (all books currently out) |
| `GateEventRepository` | Insert an event, read and write import progress |

**Three statements worth understanding**

1. **Searching** uses `LIKE @q` with the text wrapped in `%...%`, so typing part of an ID or name finds a match.
2. **`BookRepository.Update`** changes the title, author and total copies in **one** statement:

   ```sql
   UPDATE Books SET title=@t, author=@a,
          copies_available = copies_available + (@c - copies_total),
          copies_total = @c
   WHERE book_id=@i AND copies_available + (@c - copies_total) >= 0
   ```

   If the library adds 2 copies, `copies_available` rises by 2. If it removes copies, the `WHERE` clause refuses the change when it would leave fewer copies than are currently on loan. The method returns `false` in that case and the screen explains why.
3. **`LoanRepository.Active`** joins Loans, Members and Books so the grid shows names and titles, not raw IDs. A loan is active when `date_returned IS NULL`.

---

## [10] Gate log importer (`Services/GateLogImporter.cs`)

This is the bridge between the gate CSV files and SQLite.

**What it does for each gate file, step by step**

1. Read the file in a way that allows the gate to keep writing (`FileShare.ReadWrite`).
2. Skip the header row and any blank lines.
3. Look up how many rows were imported last time (`ImportState`).
4. Parse and insert only the rows after that point.
5. Save the new row count.
6. Do steps 3 to 5 in **one transaction**: either everything is saved or nothing is.

**Example**

| Sync | File has | ImportState says | Imports | New ImportState |
|---|---|---|---|---|
| 1 | 4 rows | 0 | rows 1 to 4 | 4 |
| 2 | 7 rows | 4 | rows 5 to 7 | 7 |
| 3 | 7 rows | 7 | nothing | 7 |

**Edge cases handled**

| Situation | What happens |
|---|---|
| A gate PC is offline or the path is wrong | That file is skipped and the problem is shown in the status bar. The other gates still sync |
| The last line is half-written (the gate was mid-write) | It is left for the next sync instead of being lost |
| A bad line in the middle of the file | It is skipped permanently so it cannot block the rest |
| The file shrinks (it was replaced or cleared) | The import restarts from the top, and the UNIQUE constraint ignores duplicates |
| The same row arrives twice | `INSERT OR IGNORE` plus the UNIQUE constraint discards it |

Importing is **pull, not push**: this PC reaches into each gate's shared folder. The gates never connect to the librarian PC, which fits the network design.

---

## [11] Presence service (`Services/PresenceService.cs`)

"Who is in the library?" is **calculated from events, never stored.** A stored "is inside" flag could drift out of date if an event were missed. Calculated from the log, it is always consistent with what the gates recorded.

**The rule:** a member is on the premises if their **latest event across all gates** is IN.

```sql
SELECT e.member_id, COALESCE(m.name, '(not registered)'), e.timestamp, e.gate_id
FROM GateEvents e LEFT JOIN Members m ON m.member_id = e.member_id
WHERE e.direction = 'IN'
  AND e.id = (SELECT id FROM GateEvents WHERE member_id = e.member_id
              ORDER BY timestamp DESC, id DESC LIMIT 1)
```

Reading it from the inside out:

- The sub-query finds each member's single most recent event. `id DESC` breaks ties when two events share a timestamp.
- The outer query keeps only members whose latest event is IN.
- `LEFT JOIN` keeps people who are not registered yet, and `COALESCE` shows "(not registered)" instead of a blank.

This is why the gate app never had to guess IN or OUT. A member can enter at Gate 1 and leave at Gate 2, and this query still gets it right because it sees both gates' events together.

`IsOnPremises(memberId)` answers the same question for one member. If they have no events at all, they are treated as **not** on the premises.

---

## [12] Loan service (`Services/LoanService.cs`)

All lending rules live here, so the screens stay thin.

**`Issue` checks run in a fixed order. The first failure stops the loan and returns a clear message:**

1. The member ID exists.
2. The member's status is Active.
3. The member is on the premises (section 11).
4. The member is under the loan limit.
5. A copy of the book is available.

Only then are the loan and stock change saved, **in one transaction**:

- `UPDATE Books SET copies_available = copies_available - 1 WHERE book_id=@b AND copies_available > 0`
- `INSERT INTO Loans ...`

The `copies_available > 0` condition inside the UPDATE makes "check stock" and "reduce stock" a single atomic step. If two requests raced for the last copy, only one could succeed. If either statement fails, the transaction is rolled back, so there is never a loan without a stock change or the reverse.

**`Return`** finds the open loan, stamps `date_returned`, and adds one back to `copies_available`, again inside one transaction. Returning a loan that is already returned is refused with a message.

Both methods return `(bool Ok, string Message)`. The screen only shows the message, so it never needs to know why a loan was refused.

---

## [13] UI helpers (`Forms/Ui.cs`)

- **`IReloadable`** is a one-method interface (`Reload()`). Every screen implements it, so the main form can refresh whichever tab is showing without knowing which screen it is.
- **`Ui`** holds small helpers so every screen looks and behaves the same: a standard read-only grid, a flow-layout bar for fields and buttons, label and button builders, and two message-box shortcuts (`Info` and `Error`).

Keeping these in one place means a change to the look (for example the grid style) is made once.

---

## [14] Main form (`Forms/MainForm.cs`)

The shell of the app: a toolbar on top and four tabs below.

| Tab | Screen |
|---|---|
| On premises | `PresenceView` |
| Members | `MembersView` |
| Books | `BooksView` |
| Loans | `LoansView` |

**Syncing**

- A **"Sync gate logs"** button runs the importer on demand.
- A **timer** runs it automatically every `SyncMinutes`, and once when the window first appears.
- `SyncAsync` runs the import with `Task.Run`, on a background thread. A slow or offline gate then **never freezes the window.**
- A `_syncing` flag stops two syncs overlapping if the timer fires while one is still running.
- When a sync ends, the status bar shows the time, the number of new events and any problems, and the visible tab is refreshed.

**Refreshing:** when the librarian switches tabs, that screen's `Reload()` runs, so it always shows current data.

---

## [15] On premises screen (`Forms/PresenceView.cs`)

A read-only grid of everyone currently inside, with their ID, name, the time they came in and the gate they used, plus a count at the top ("3 on the premises"). It calls `PresenceService.OnPremises()` and binds the result to the grid. It has no buttons: the data comes entirely from the gate logs.

---

## [16] Members screen (`Forms/MembersView.cs`)

- A **search box** filters the grid as you type.
- Clicking a row **fills the edit fields** below the grid.
- **Add**, **Update** and **Clear** buttons manage members.

`Save` validates before touching the database:

1. The ID must match the ID rule.
2. The name must not be empty.
3. **Add** refuses an ID that already exists.
4. **Update** refuses an ID that does not exist.

The status drop-down (Active or Suspended) lets the librarian block a member from borrowing without deleting their history.

---

## [17] Books screen (`Forms/BooksView.cs`)

The same pattern as members: search, grid, and an edit bar with title, author and number of copies.

- The screen remembers the selected book's ID in `_selected`, so **Update** knows which row to change.
- Changing the copy count goes through `BookRepository.Update`, which keeps the available count correct and refuses to drop below the number on loan (section 9).
- The grid shows both **Total** and **Available**, so the librarian sees stock at a glance.

---

## [18] Loans screen (`Forms/LoansView.cs`)

The screen the librarian uses most.

1. Type the **member ID.** The label next to it immediately shows the name and whether they are **ON** or **NOT on** the premises, in green or red, so the librarian knows before clicking anything.
2. Pick a **book** from the list of books that have at least one copy available.
3. Click **Issue.** The screen passes the inputs to `LoanService.Issue` and shows its message.
4. To take a book back, select the loan in the grid and click **Return selected book.**

The screen collects inputs and displays results. **It never decides whether a loan is allowed.** That is `LoanService`'s job.

---

## [19] A day in the library: end-to-end example

| Time | What happens | Where the data goes |
|---|---|---|
| 09:02 | Thandi (`M001`) scans in at Gate 1 | `gate1_log.csv` gets `2026-11-18T09:02:11,M001,IN,GATE1` |
| 09:05 | The librarian's app syncs | New row imported into `GateEvents`, `ImportState` for gate 1 becomes 1 |
| 09:06 | The On premises tab refreshes | Thandi appears in the list |
| 09:10 | The librarian types `M001` on the Loans tab | Label turns green: "Thandi: ON premises" |
| 09:11 | The librarian issues a book | `copies_available` drops by 1, a `Loans` row is created, due in 14 days |
| 09:40 | Thandi leaves by Gate 2 | `gate2_log.csv` gets `...,M001,OUT,GATE2` |
| 09:45 | Next sync | Her latest event is now OUT, so she leaves the On premises list |
| 09:50 | She tries to borrow another book | Refused: not on the premises |
| Next visit | She returns the book | The librarian selects the loan and clicks Return, and stock goes back up |

---

## [20] Error handling and edge cases

| Situation | Behaviour |
|---|---|
| App cannot start (bad settings or path) | A message box explains it, no silent crash |
| A gate PC is off | Its file is skipped, the status bar names the problem, other gates sync |
| Network share unreachable | Same as above. The app keeps working with the data it already has |
| Member not registered but scanned | Event stored, shown as "(not registered)", cannot borrow |
| Duplicate member ID on Add | Refused with a message |
| Copies reduced below number on loan | Refused with a message |
| Last copy requested twice | Only one loan succeeds |
| Database error on a screen action | Caught and shown in a message box |

---

## [21] Security and privacy

- **SQL injection** is prevented by parameterised queries (section 7).
- **The database is local.** The SQLite file is only on the librarian PC. The network design lets the librarian PC reach the gates, but nothing on the gate or public VLANs can reach it.
- **The gate shares are read-only,** so a compromised gate PC cannot be used to tamper with what the librarian already imported.
- **Only what is needed is stored:** a member ID, name and phone number.
- **Personal information:** member names and phone numbers are personal data. In South Africa the Protection of Personal Information Act (POPIA) applies, so it is worth reading up on it, keeping the data minimal and backing the file up safely. This is a pointer, not legal advice.
- **Back up** `library.db` regularly to an external drive. The network design does not protect against a failed disk.

---

## [22] Known limitations and future improvements

| Limitation | Possible improvement |
|---|---|
| One librarian PC only | A small server database if more than one desk is ever needed |
| No login screen | Add a simple password or Windows account check |
| No overdue highlighting | Colour overdue rows red and add a fines or reminder report |
| Members cannot be deleted or archived | Add archive, keeping loan history |
| No book categories or ISBN | Extra columns and a barcode scan field |
| No reports | Monthly visits and most borrowed books, exported to CSV |
| Gate paths are edited in a JSON file | A settings screen inside the app |
| Importing reads the whole gate file each time | Fine at village scale. Read only the new tail if files grow very large |

---

## [23] Build and run

1. Install the **.NET SDK** and open `LibraryApp.csproj` in Visual Studio 2022 or later.
2. Make sure the project's target framework matches the SDK you have installed, for example `net8.0-windows` or `net10.0-windows`.
3. Restore packages (Visual Studio does it automatically, or run `dotnet restore`). This downloads Microsoft.Data.Sqlite.
4. Press **F5.** On first run the app creates `%AppData%\LibraryApp\library.db` and `settings.json`.
5. Edit `settings.json` so `GateLogPaths` point to your real gate files. For a local test, a path such as `C:\library\logs\gate1_log.csv` works.
6. Restart the app, or press **Sync gate logs.**

---

## [24] Testing checklist

| Test | Expected result |
|---|---|
| First launch | Database file and `settings.json` are created, window opens |
| Add a member with ID `M001` | Appears in the Members grid |
| Add the same ID again | "Already exists" message |
| Add a book with 2 copies | Total 2, Available 2 |
| Run the gate app and scan `M001` IN, then Sync | `M001` appears on the On premises tab |
| Scan `M001` OUT, then Sync | `M001` disappears from the list |
| Sync twice with no new scans | "0 new events", no duplicates |
| Issue a book to `M001` while outside | Refused: not on the premises |
| Issue a book to `M001` while inside | Succeeds, Available drops by 1, due date shown |
| Issue a 4th book to the same member | Refused: loan limit |
| Return the book | Available goes back up, loan leaves the grid |
| Reduce copies below the number on loan | Refused |
| Point a gate path at a wrong location | Status bar names the problem, the app keeps working |
| Suspend a member and try to borrow | Refused: Suspended |

