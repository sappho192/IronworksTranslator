namespace IronworksTranslator.Utils
{
    public static class WindowChecker
    {
        public static bool IsMinimized(double top, double left)
        {
            if (top < -1 * SystemParameters.VirtualScreenHeight ||
                    left < -1 * SystemParameters.VirtualScreenWidth)
            {// To skip the case of minimized window
                return true;
            }
            return false;
        }

        public static bool IsMaximized(double top, double left)
        {
            if (top > SystemParameters.VirtualScreenHeight ||
                left > SystemParameters.VirtualScreenWidth)
            { // To skip the case of maximized window
                return true;
            }
            return false;
        }
    }
}
