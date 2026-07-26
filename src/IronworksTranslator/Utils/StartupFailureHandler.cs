namespace IronworksTranslator.Utils
{
    internal enum StartupFailureStage
    {
        UserNotification,
        Cleanup,
        Shutdown,
    }

    internal static class StartupFailureHandler
    {
        internal const int ExitCode = 1;

        internal static async Task HandleAsync(
            Action notifyUser,
            Func<Task> cleanupAsync,
            Action<int> shutdown,
            Action<Exception, StartupFailureStage> reportSecondaryFailure)
        {
            try
            {
                notifyUser();
            }
            catch (Exception ex)
            {
                reportSecondaryFailure(ex, StartupFailureStage.UserNotification);
            }

            try
            {
                await cleanupAsync();
            }
            catch (Exception ex)
            {
                reportSecondaryFailure(ex, StartupFailureStage.Cleanup);
            }

            try
            {
                shutdown(ExitCode);
            }
            catch (Exception ex)
            {
                reportSecondaryFailure(ex, StartupFailureStage.Shutdown);
                throw;
            }
        }
    }
}
