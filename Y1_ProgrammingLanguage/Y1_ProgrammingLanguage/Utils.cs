using System;
using System.Text;

namespace Kronosta.Language.Y1
{
    internal class Utils
    {
        /*
        Escapes:
        - "`E", '!'
        - "`Q", '?'
        - "`S", space
        - "`N", newline
        - "`T", tab
        - "`R", carriage return
        - "`}", ']'
        - "`G", '`'
        - "`Uxxxx" (where x is a hexadecimal digit) - UTF-16 unicode codepoint
        - ('`' followed by a newline, carriage return, form feed, or vertical tab),
          escapes a newline. This only matters in very specific scenarios  
             
         */
        public static string GraveUnescape(string escaped)
        {
            escaped = escaped
                .Replace("`E", "!")
                .Replace("`Q", "?")
                .Replace("`S", " ")
                .Replace("`N", "\n")
                .Replace("`T", "\t")
                .Replace("`R", "\r")
                .Replace("`}", "]")
                .Replace("`\n", "")
                .Replace("`\r", "")
                .Replace("`\f", "")
                .Replace("`\v", "");
            string total = "";
            for (int i = 0; i < escaped.Length; i++)
            {
                if (escaped[i] == '`')
                {
                    i++;
                    if (escaped[i] == 'U')
                    {
                        string hexCode = "0x";
                        i++;
                        hexCode += escaped[i];
                        i++;
                        hexCode += escaped[i];
                        i++;
                        hexCode += escaped[i];
                        i++;
                        hexCode += escaped[i];
                        total += "" + (char)(Convert.ToInt32(hexCode, 16));
                    }
                    else if (escaped[i] == 'G')
                    {
                        total += "`";
                    }
                }
                else
                {
                    total += escaped[i];
                }
            }
            return total;
        }

        public static string GraveEscape(string original, string banned, bool banNonAscii)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in original)
            {
                if (c == '\n') sb.Append("`N");
                else if (c == '\t') sb.Append("`T");
                else if (c == '\r') sb.Append("`R");
                else if (c == '`') sb.Append("`G");
                else if (c < 32 || (c >= 127 && c < 160) || (c >= 160 && banNonAscii) || banned.Contains(c))
                {
                    sb.Append("`U");
                    string hex = Convert.ToString((int)c, 16);
                    while (hex.Length < 4)
                        hex = "0" + hex;
                    sb.Append(hex);
                }
            }
            return sb.ToString();
        }

        public static string ToIdentifier(string unprocessed)
        {
            if (unprocessed.Contains('.'))
            {
                unprocessed = unprocessed.Substring(0, unprocessed.LastIndexOf('.'));
            }
            string result = "";
            for (int i = 0; i < unprocessed.Length; i++)
            {
                if ("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_".Contains(unprocessed[i]))
                {
                    result += unprocessed[i];
                }
                else if (i > 0 && "0123456789".Contains(unprocessed[i]))
                {
                    result += unprocessed[i];
                }
            }
            return result;
        }
    }
}
