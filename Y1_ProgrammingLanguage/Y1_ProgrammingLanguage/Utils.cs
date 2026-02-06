using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Kronosta.Language.Y1
{
    public class Utils
    {
        private static long FuncAssemblyCount = 0;

        /*
        - `` `E ``, `!`
        - `` `Q ``, `?`
        - `` `S ``, space
        - `` `N ``, newline
        - `` `T ``, tab
        - `` `R ``, carriage return
        - `` `} ``, `]`
        - `` `G ``, `` ` ``
        - `` `Uxxxx `` (where x is a hexadecimal digit) - UTF-16 unicode codepoint
        - (`` ` `` followed by a newline, carriage return, form feed, or vertical tab),
          escapes a newline. This only matters in very specific scenarios, such as
          when grave escaping in Yen interpolations or if you somehow inject line breaks
          into a line.
             
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

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8603 // Possible null reference return.
        public static Func<object?, object?> Y1ToFunc(string y1)
        {
            Compiler compiler = new Compiler();
            Stream stream = new MemoryStream();
            string aName = $"Kronosta.Y1.Special.Utils_Y1ToFunc_{FuncAssemblyCount}";
            FuncAssemblyCount++;
            EmitResult result = compiler.Compile(
                stream,
                aName,
                new Dictionary<string, string>(),
                new List<string> { y1 },
                OutputKind.DynamicallyLinkedLibrary);
            if (!result.Success)
            {
                throw new SyntaxErrorException("Error when compiling assembly for Utils.Y1ToFunc:\n[\n" +
                    string.Join("\n,,,\n",
                        result.Diagnostics.Select(
                            diag => diag.GetMessage() + ", Location: " +
                                diag.Location.MetadataModule + ":" + diag.Location.GetLineSpan()))
                                + "\n]\nSourceCode: " + result.Diagnostics.Where(
                                    x => x.Location.SourceTree?.ToString() != null
                                ).FirstOrDefault()?.Location?.SourceTree);
            }
            stream.Position = 0;
            AssemblyLoadContext alc = new AssemblyLoadContext(aName, true);
            Assembly assem = alc.LoadFromStream(stream);
            Type type = assem.GetType("Entry");

            MethodInfo method = type.GetMethod("Func", new Type[] { typeof(object) });

            return param => method.Invoke(null, new object?[] { param });
        }
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8603 // Possible null reference return.
    }
}
