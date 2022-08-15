using IronworksTranslator.Core;
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
                var handler = IronworksContext.CurrentMemoryHandler;
                var message = handler.GetString(handler.Scanner.Locations["ALLMESSAGES"], 0, 1024);
                if (message != lastMessage)
                {
                    lastMessage = message;
                    return message;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Application.Current.Shutdown();
            }
            return "";
        }
    }
}