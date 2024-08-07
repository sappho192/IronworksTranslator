using Sharlayan.Core;
using Sharlayan.Utilities;
using System.Text;

namespace IronworksTranslator.Helpers.Extensions
{
    public static class ByteArrayExtension
    {
        public static ChatLogItem DecodeAutoTranslate(this byte[] rawMessage)
        {
            //List<byte[]> result = new List<byte[]>();
            List<byte> rawResult = [];
            var test = Encoding.UTF8.GetBytes("[定型文]");
            /*
             * \u0002 \002E \u0004 \u0002 \u00F0 \u00CF \u0003
             * \u0002 \002E \u0003 \u0002 \u00CA \u0003
             * \u0002 \002E \u0005 \u0004 \u00F2 \u0001 \u0095 \u0003
             */
            for (int i = 0; i < rawMessage.Length; i++)
            {
                if (rawMessage[i].Equals(0x02)) // STX
                {
                    if (i + 1 == rawMessage.Length)
                    {// Bound check
                        rawResult.Add(rawMessage[i]);
                        continue;
                    }
                    if (!rawMessage[i + 1].Equals(0x2E))
                    {// it should be char '.'(=0x2E)
                        rawResult.Add(rawMessage[i]);
                        continue;
                    }
                    byte range = rawMessage[i + 2];
                    if (i + range > rawMessage.Length)
                    {
                        rawResult.Add(rawMessage[i]);
                        continue;
                    }

                    if (rawMessage[i + range + 2].Equals(0x03)) // ETX
                    {// Found AutoTranslate block
                        byte[] autoTranslate = new byte[range - 1];
                        Array.Copy(rawMessage, i + 3, autoTranslate, 0, range - 1);
                        //result.Add(autoTranslate);
                        rawResult.AddRange(test);
                        i = i + range + 2;
                    }
                    else
                    {
                        rawResult.Add(rawMessage[i]);
                    }
                }
                else
                {
                    rawResult.Add(rawMessage[i]);
                }
            }

            return ChatEntry.Process(rawResult.ToArray());
        }

        public static List<byte[]> ExtractAutoTranslate(this byte[] rawMessage)
        {
            List<byte[]> result = [];

            /*
             * \u0002 \002E \u0004 \u0002 \u00F0 \u00CF \u0003
             * \u0002 \002E \u0003 \u0002 \u00CA \u0003
             * \u0002 \002E \u0005 \u0004 \u00F2 \u0001 \u0095 \u0003
             */
            for (int i = 0; i < rawMessage.Length; i++)
            {
                if (rawMessage[i].Equals(0x02)) // STX
                {
                    if (i + 1 == rawMessage.Length) break; // Bound check
                    if (!rawMessage[i + 1].Equals(0x2E)) continue; // it should be char '.'(=0x2E)
                    byte range = rawMessage[i + 2];
                    if (i + range > rawMessage.Length) continue;

                    if (rawMessage[i + range + 2].Equals(0x03)) // ETX
                    {// Found AutoTranslate block
                        byte[] autoTranslate = new byte[range - 1];
                        Array.Copy(rawMessage, i + 3, autoTranslate, 0, range - 1);
                        result.Add(autoTranslate);
                        i = i + range + 2;
                    }
                }
            }

            return result;
        }
    }
}
