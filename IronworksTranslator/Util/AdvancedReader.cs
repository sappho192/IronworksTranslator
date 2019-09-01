namespace Sharlayan
{
    public static class AdvancedReader
    {
        public static string getMessage()
        {
            //var array = MemoryHandler.Instance.GetByteArray(Scanner.Instance.Locations["ALLMESSAGES"], 100);
            //var array2 = MemoryHandler.Instance.GetByteArray(Scanner.Instance.Locations["ALLMESSAGES2"], 100);
            //var str = BitConverter.ToString(array);
            //var str2 = BitConverter.ToString(array2);
            //var raw =
            var message = MemoryHandler.Instance.GetString(Scanner.Instance.Locations["ALLMESSAGES"]);

            return message;
        }
    }
}