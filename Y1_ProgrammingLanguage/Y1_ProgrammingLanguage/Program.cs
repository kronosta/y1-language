using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Emit;

namespace Kronosta.Language.Y1
{
    public class Program
    {
        [ThreadStatic]
        public static string? filename;
        [ThreadStatic]
        public static string? y1Code;
        [ThreadStatic]
        public static List<string> assemblyRefs = new List<string>();
        [ThreadStatic]
        public static List<string> assemblyPaths = new List<string>();
        [ThreadStatic]
        public static List<string> standardY1Libs = new List<string>();
        [ThreadStatic]
        public static string framework = "net6.0";
        [ThreadStatic]
        public static string platform = "anycpu";
        [ThreadStatic]
        public static string sdk = "Microsoft.NET.Sdk";
        [ThreadStatic]
        public static Random rand = new Random((int)DateTime.Now.ToBinary());

        [ThreadStatic]
        internal static List<string> sourceFiles;
        [ThreadStatic]
        internal static List<string> sourceTexts;
        [ThreadStatic]
        internal static string AssemblyName;

        public static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "@@FILE")
            {
                using (StreamReader sw = new StreamReader(args[1]))
                {
                    args = sw.ReadToEnd().Replace("\r\n", "\n").Split(new char[] { '\n', '\r' });
                }
            }
            var argdict = args.Where(x => x.StartsWith("/"))
                .Select(x => x.Substring(1).Split('='))
                .Select(x => x.Length == 1 ? new string[] { x[0], "" } : new string[] { x[0], x[1] })
                .Aggregate(
                    new Dictionary<string, List<string>>(),
                    (x, y) =>
                    {
                        if (x.ContainsKey(y[0])) x[y[0]].Add(y[1]);
                        else x[y[0]] = new List<string> { y[1] };
                        return x;
                    }
                );
            if (argdict.ContainsKey("Source"))
            {
                sourceFiles = new List<string>();
                foreach (string s in argdict["Source"])
                    sourceFiles.Add(s);
            }
            else
            {
                throw new ArgumentException("One or more /Source=[file] options are required.");
            }
            if (argdict.ContainsKey("Name") && argdict["Name"]?.Count == 1)
            {
                AssemblyName = argdict["Name"][0];
            }
            else
            {
                AssemblyName = "TestY1";
            }
            string tfm = "net8.0", frameworkName = "Microsoft.NETCore.App", frameworkVersion = "8.0.0";
            if (argdict.ContainsKey("Version") && argdict["Version"]?.Count == 1)
            {
                string[] versionPieces = argdict["Version"][0].Split(";");
                if (versionPieces.Length == 3)
                {
                    tfm = versionPieces[0];
                    frameworkName = versionPieces[1];
                    frameworkVersion = versionPieces[2];
                }
            }
            sourceTexts = sourceFiles.Select(x =>
            {
                using (StreamReader sr = new StreamReader(x))
                {
                    return sr.ReadToEnd();
                }
            }).ToList();
            Compiler compiler = new Compiler();
            compiler.CompilerSettings.PrintOutCSharp = true;
            string newFolder = Path.Combine(Directory.GetCurrentDirectory(), AssemblyName);
            Directory.CreateDirectory(newFolder);
            using (FileStream fs = File.Create(Path.Combine(newFolder, AssemblyName + ".dll")))
            {
                EmitResult result = compiler.Compile(fs, AssemblyName, new Dictionary<string, string>(), sourceTexts);
                Console.WriteLine(result.Diagnostics.Aggregate("", (x, y) => x + "\n" + y.ToString()));
            }
            using (StreamWriter sw = new StreamWriter(Path.Combine(newFolder, AssemblyName + ".runtimeconfig.json")))
            {
                sw.WriteLine(@"{
  ""runtimeOptions"": {
    ""tfm"": ""net6.0"",
    ""framework"": {
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""6.0.0""
    }
  }
}");
            }
        }
    }
}
