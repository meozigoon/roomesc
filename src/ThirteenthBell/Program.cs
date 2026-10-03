namespace ThirteenthBell;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) => ErrorReporter.Report(eventArgs.Exception, "UI thread", true);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                ErrorReporter.Report(exception, "Unhandled application exception", false);
            }
        };

        GameForm? game = null;
        try
        {
            Theme.Initialize();
            game = new GameForm();
            Application.Run(game);
            ErrorReporter.BeginShutdown();
            return 0;
        }
        catch (Exception exception)
        {
            ErrorReporter.Report(exception, "Application startup or runtime", true);
            return 1;
        }
        finally
        {
            ErrorReporter.BeginShutdown();
            try
            {
                game?.Dispose();
            }
            catch (InvalidOperationException exception)
            {
                ErrorReporter.Report(exception, "Disposing the game window", false);
            }

            try
            {
                Theme.Shutdown();
            }
            catch (InvalidOperationException exception)
            {
                ErrorReporter.Report(exception, "Releasing the game theme", false);
            }
        }
    }
}
