#!/usr/bin/env python3
"""
Library Gate Tracker
--------------------
Runs on a gate PC. Records who goes IN and OUT of the library to a local CSV.

Numbered comments like "# [3]" point to the matching section in
gate_tracker_explainer.md. Comments without a number explain the line(s)
beneath them.

Usage examples:
    python gate_tracker.py --gate-id GATE1 --direction in      # entrance gate
    python gate_tracker.py --gate-id GATE2 --direction out     # exit gate
    python gate_tracker.py --gate-id GATE1 --direction both    # asks IN/OUT each scan
"""

# Standard library only, so nothing needs to be installed on the gate PC.
import argparse   # reads command-line options
import csv        # reads and writes the log file safely
import os         # file and folder handling
import re         # pattern matching for ID validation
import sys        # lets us exit with an error code
from datetime import datetime  # timestamps

# [3] CSV format: these are the column names, in order.
FIELDS = ["timestamp", "member_id", "direction", "gate_id"]

# [8] ID validation: letters, digits, dash and underscore, 1 to 20 characters.
MEMBER_ID_PATTERN = re.compile(r"^[A-Za-z0-9_-]{1,20}$")

# [9] Duplicate-scan guard: ignore the same event if it repeats within this time.
DUPLICATE_WINDOW_SECONDS = 5


def parse_args():
    # [4] Command-line options: each gate PC is configured when it is started,
    # so the same script can serve every gate.
    p = argparse.ArgumentParser(description="Library gate tracker")

    # Unique name for this gate. It also decides the log file name.
    p.add_argument("--gate-id", default="GATE1", help="Unique name for this gate PC")

    # [5] The app never guesses IN or OUT. The direction is fixed by the gate's
    # role ("in" or "out") or asked every time ("both").
    p.add_argument(
        "--direction",
        choices=["in", "out", "both"],
        default="both",
        help="in = entrance only, out = exit only, both = ask each time",
    )

    # Folder where the CSV lives. Point this at the folder you share read-only.
    p.add_argument("--log-dir", default="logs", help="Folder for the CSV log")
    return p.parse_args()


def log_path(log_dir, gate_id):
    # [2] One file per gate: GATE1 -> logs/gate1_log.csv
    return os.path.join(log_dir, f"{gate_id.lower()}_log.csv")


def ensure_log(path):
    # [6] Creating the log: make the folder if missing, and write the header row
    # only when the file is new or empty. Existing data is never overwritten.
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    if not os.path.exists(path) or os.path.getsize(path) == 0:
        with open(path, "w", newline="", encoding="utf-8") as f:
            csv.writer(f).writerow(FIELDS)


def append_event(path, member_id, direction, gate_id):
    # [7] Writing an event: append one row, then force it onto the disk.
    # ISO format (2026-11-18T13:17:12) sorts correctly and is easy to import.
    now = datetime.now().isoformat(timespec="seconds")

    # Mode "a" means append, so earlier rows are never touched.
    with open(path, "a", newline="", encoding="utf-8") as f:
        csv.writer(f).writerow([now, member_id, direction, gate_id])

        # flush() pushes Python's buffer to the OS, and fsync() pushes the OS
        # buffer to the disk. Without these, a power cut could lose recent scans.
        f.flush()
        os.fsync(f.fileno())
    return now


def read_recent(path, limit=10):
    # [10] Reading helpers: used by the /recent command.
    with open(path, newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    # Keep only the last `limit` rows.
    return rows[-limit:]


def last_event_for(path, member_id):
    # [10] Reading helpers: find the newest row for one member in THIS gate's
    # file only. It is used by the duplicate-scan guard.
    last = None
    with open(path, newline="", encoding="utf-8") as f:
        for row in csv.DictReader(f):
            if row["member_id"] == member_id:
                last = row  # later rows overwrite earlier ones, so the last wins
    return last


def ask_direction():
    # [5] "both" mode: the attendant tells the app IN or OUT for each scan.
    # Returns None if cancelled (c, Ctrl+C or Ctrl+D).
    while True:
        try:
            choice = input("  IN or OUT? [i/o, c=cancel]: ").strip().lower()
        except (EOFError, KeyboardInterrupt):
            print()
            return None
        if choice in ("i", "in"):
            return "IN"
        if choice in ("o", "out"):
            return "OUT"
        if choice in ("c", "cancel"):
            return None
        print("  Please type i, o or c.")


def print_help():
    # [11] Commands available to the attendant.
    print(
        "\nCommands:\n"
        "  <member id>   record an event for that member\n"
        "  /recent       show the last 10 events at this gate\n"
        "  /help         show this help\n"
        "  /quit         exit the program\n"
    )


def main():
    args = parse_args()
    path = log_path(args.log_dir, args.gate_id)

    # Store the gate name in uppercase so the CSV is consistent.
    gate_id = args.gate_id.upper()

    # [12] Error handling: if the log cannot be created (bad path, no
    # permission), stop immediately with a clear message instead of failing
    # later in the middle of a busy morning.
    try:
        ensure_log(path)
    except OSError as e:
        print(f"Cannot open log file {path}: {e}")
        sys.exit(1)

    print(f"Library Gate Tracker | gate: {gate_id} | mode: {args.direction.upper()}")
    print(f"Logging to: {os.path.abspath(path)}")
    print_help()

    # [11] Main loop: read one line, decide if it is a command or a member ID,
    # record it, repeat until the attendant quits.
    while True:
        try:
            raw = input("Member ID> ").strip()
        except (EOFError, KeyboardInterrupt):
            # Ctrl+C / Ctrl+D ends the program cleanly instead of crashing.
            print("\nGoodbye.")
            break

        # Empty line (just pressing Enter): ask again.
        if not raw:
            continue

        # Anything starting with "/" is a command, not a member ID.
        if raw.startswith("/"):
            cmd = raw.lower()
            if cmd == "/quit":
                print("Goodbye.")
                break
            elif cmd == "/help":
                print_help()
            elif cmd == "/recent":
                for r in read_recent(path):
                    print(f"  {r['timestamp']}  {r['member_id']:<12} {r['direction']}")
            else:
                print("  Unknown command. Type /help.")
            continue

        # [8] ID validation: uppercase first so "m001" and "M001" are one member,
        # then reject anything that does not match the allowed pattern.
        member_id = raw.upper()
        if not MEMBER_ID_PATTERN.match(member_id):
            print("  Invalid ID. Use letters, digits, - or _ (max 20 characters).")
            continue

        # [5] Decide the direction: fixed by the gate role, or ask the attendant.
        direction = args.direction.upper() if args.direction != "both" else ask_direction()
        if direction is None:
            print("  Cancelled. Nothing recorded.")
            continue

        # [9] Duplicate-scan guard: if this member's last event here has the
        # same direction and happened a moment ago, treat it as a double scan.
        last = last_event_for(path, member_id)
        if last and last["direction"] == direction:
            try:
                seconds = (datetime.now() - datetime.fromisoformat(last["timestamp"])).total_seconds()
            except ValueError:
                # Unreadable timestamp: assume it is old so the scan is accepted.
                seconds = DUPLICATE_WINDOW_SECONDS + 1
            if seconds < DUPLICATE_WINDOW_SECONDS:
                print(f"  Ignored: {member_id} was just recorded as {direction}.")
                continue

        # [12] Error handling: if the write fails (disk full, file locked),
        # tell the attendant clearly that the event was NOT saved.
        try:
            ts = append_event(path, member_id, direction, gate_id)
        except OSError as e:
            print(f"  ERROR: could not write to the log ({e}). Event NOT saved.")
            continue

        print(f"  OK  {member_id} {direction} at {ts}")


# Run main() only when the file is executed directly, not when imported.
if __name__ == "__main__":
    main()
