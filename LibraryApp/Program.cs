// Numbered comments like "// [7]" point to the matching section in LibraryApp_explainer.md.
// Comments without a number explain the code beneath them.

using LibraryApp.Data;
using LibraryApp.Forms;

namespace LibraryApp;

static class Program
{
    // [4] Startup: load settings, create the database if needed, then open the main window.
    // Any failure is shown in a message box instead of crashing silently.
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            var settings = AppSettings.Load();
            // [7] Runs once per start-up and is safe to repeat (CREATE TABLE IF NOT EXISTS).
            Database.Initialize(settings.DbPath);   // creates library.db and tables on first run
            Application.Run(new MainForm(settings));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Library app could not start", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
