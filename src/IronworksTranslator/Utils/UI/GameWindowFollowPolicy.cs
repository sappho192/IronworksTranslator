namespace IronworksTranslator.Utils.UI
{
    internal enum GameWindowFollowAction
    {
        Keep,
        Minimize,
        Restore
    }

    internal static class GameWindowFollowPolicy
    {
        internal static GameWindowFollowAction Decide(
            bool enabled, bool gameAvailable, bool gameMinimized,
            int foregroundProcessId, int gameProcessId, int appProcessId)
        {
            if (!enabled || !gameAvailable || gameProcessId <= 0)
                return GameWindowFollowAction.Restore;
            // Include settings, input windows and modal dialogs in the app focus exception.
            if (foregroundProcessId == appProcessId ||
                (foregroundProcessId == gameProcessId && !gameMinimized))
                return GameWindowFollowAction.Restore;
            // A transient null foreground window during Alt+Tab is not a focus change.
            if (foregroundProcessId == 0 && !gameMinimized)
                return GameWindowFollowAction.Keep;
            return GameWindowFollowAction.Minimize;
        }
    }

    internal sealed class AutomaticWindowState
    {
        private WindowState? _stateBeforeMinimizing;

        internal WindowState? Update(bool visible, WindowState currentState, bool minimize)
        {
            // If the user restores the window, its new state takes precedence.
            if (currentState != WindowState.Minimized)
                _stateBeforeMinimizing = null;
            if (minimize && visible)
            {
                if (currentState == WindowState.Minimized) return null;
                _stateBeforeMinimizing = currentState;
                return WindowState.Minimized;
            }

            // Reset a hidden window's state without showing it, so a later dashboard
            // Show() does not retain an automatic minimized state.
            var previousState = _stateBeforeMinimizing;
            _stateBeforeMinimizing = null;
            return previousState;
        }
    }
}
