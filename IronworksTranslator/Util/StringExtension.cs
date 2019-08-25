using System.Linq;

namespace IronworksTranslator.Util
{
    public static class StringExtension
    {
        public static bool HasKorean(this string sentence)
        {
            var array = sentence.ToCharArray();
            //( Complete character || Consonant , Vowel )
            return array.Any(ch => {
                if ((0xAC00 <= ch && ch <= 0xD7A3) || (0x3131 <= ch && ch <= 0x318E))
                    return true;
                else
                    return false;
            });
        }

        public static bool HasJapanese(this string sentence)
        {
            var array = sentence.ToCharArray();
            // 0x3040 -> 0x309F === Hirigana, 0x30A0 -> 0x30FF === Katakana, 0x4E00 -> 0x9FBF === Kanji
            return array.Any(ch => {
                if ((ch >= 0x3040 && ch <= 0x309F) || (ch >= 0x30A0 && ch <= 0x30FF) || (ch >= 0x4E00 && ch <= 0x9FBF))
                    return true;
                else
                    return false;
            });
        }

        public static bool HasEnglish(this string sentence)
        {
            var array = sentence.ToCharArray();
            return array.Any(ch => {
                if ((0x61 <= ch && ch <= 0x7A) || (0x41 <= ch && ch <= 0x5A))
                    return true;
                else
                    return false;
            });
        }
    }
}
