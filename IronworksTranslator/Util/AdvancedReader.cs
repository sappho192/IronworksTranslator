using System.Windows;

namespace Sharlayan
{
    public static class AdvancedReader
    {
        private static string lastMessage = "";
        public static string getMessage()
        {
            //var array = MemoryHandler.Instance.GetByteArray(Scanner.Instance.Locations["ALLMESSAGES"], 100);
            //var array2 = MemoryHandler.Instance.GetByteArray(Scanner.Instance.Locations["ALLMESSAGES2"], 100);
            //var str = BitConverter.ToString(array);
            //var str2 = BitConverter.ToString(array2);
            //var raw =
            try
            {
                var message = MemoryHandler.Instance.GetString(Scanner.Instance.Locations["ALLMESSAGES2"]);
                if (message != lastMessage)
                {
                    lastMessage = message;
                    return message;
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Application.Current.Shutdown();
            }
            return "";
        }
    }
}