using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronworksTranslator.Util
{
    public static class ByteArrayExtension
    {
        public static List<byte[]> ExtractAutoTranslate(this byte[] rawMessage)
        {
            List<byte[]> result = new List<byte[]>();

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
                    if(i + range > rawMessage.Length) continue;

                    if(rawMessage[i + range + 2].Equals(0x03)) // ETX
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
