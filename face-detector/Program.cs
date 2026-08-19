namespace face_detector
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Install global exception handlers to capture unhandled exceptions (including native/runtime crashes)
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    var ex = e.ExceptionObject as Exception;
                    var msg = ex?.ToString() ?? e.ExceptionObject?.ToString() ?? "Unknown unhandled exception";
                    File.AppendAllText("crash.log", $"UNHANDLED: {DateTime.UtcNow:O}\n{msg}\n\n");
                }
                catch { }
            };

            Application.ThreadException += (s, e) =>
            {
                try
                {
                    File.AppendAllText("crash.log", $"UI THREAD: {DateTime.UtcNow:O}\n{e.Exception}\n\n");
                }
                catch { }
            };

            try
            {
                Application.Run(new Master());
                //Application.Run(new Camera());
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText("crash.log", $"FATAL: {DateTime.UtcNow:O}\n{ex}\n\n");
                }
                catch { }

                MessageBox.Show($"Fatal error: {ex.Message}\nSee crash.log for details.", "Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}