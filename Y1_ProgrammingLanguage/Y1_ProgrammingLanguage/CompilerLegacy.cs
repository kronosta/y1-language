using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Kronosta.Language.Y1
{
    public static class CompilerLegacy
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

        public static void Compile(string[] args)
        {
            //Fix old versions having "multiple entry points"
            bool logging = false;
            try
            {
                File.Delete("./compiled.cs");
            }
            catch
            {
            }
            if (args.Length >= 1)
            {
                filename = args[0];
            }
            else
            {
                filename = Console.ReadLine() ?? "";
                if (filename == "")
                    throw new Exception("Filename passed to compiler was empty.");
            }
            bool isLibrary = false;
            if (args.Contains("-lib"))
                isLibrary = true;
            if (args.Contains("-log"))
                logging = true;
            Encoding encoding = Encoding.UTF8;
            try
            {
                using (var sr = new StreamReader(filename))
                {
                    y1Code = sr.ReadToEnd();
                    encoding = sr.CurrentEncoding;
                }
            }
            catch
            {
                Console.WriteLine("File " + filename + " not found. Press any key to continue.");
                Console.ReadKey();
                Environment.Exit(1);
            }
            Encoding.UTF8.GetString(Encoding.Convert(encoding, Encoding.UTF8, encoding.GetBytes(y1Code)));
            Compiler dummyComp = new Compiler();
            Preprocessor pp = new Preprocessor(dummyComp);
            y1Code = pp.Prepreprocess(y1Code);
            string[] y1CodeSplitBlankLines = y1Code.Split('\n', '\r', '\f', '\v');
            List<string> y1CodeSplitBlankLinesRemoved = new List<string>();
            foreach (string i in y1CodeSplitBlankLines)
            {
                if (i != "")
                {
                    y1CodeSplitBlankLinesRemoved.Add(i);
                }
            }

            if (logging) pp.DoLogging = true;
            string csCode = new CSharpConverter(dummyComp).ConvertToCSharp(pp.Preprocess(y1CodeSplitBlankLinesRemoved), 0, true);
            int filenameSlashLastIndex = filename.LastIndexOf("/");
            string filenameLast = filename;
            if (filenameSlashLastIndex >= 0 && filename.Length > filenameSlashLastIndex + 1)
                filenameLast = filename.Substring(filenameSlashLastIndex + 1);
            string csprojName = filenameLast + ".csproj";
            string csName = filenameLast + ".~cs";
            if (File.Exists(csprojName)) File.Delete(csprojName);
            if (File.Exists(csName)) File.Delete(csName);

            using (var file = File.Create(csName)) { }

            using (var sw = new StreamWriter(csName))
            {
                sw.Write(csCode);
            }

            using (StreamWriter sw = new StreamWriter(csprojName))
            {
                sw.WriteLine($"<Project Sdk=\"{sdk}\">");
                sw.WriteLine("<PropertyGroup>");
                sw.WriteLine(isLibrary ? "<OutputType>Library</OutputType>" : "<OutputType>Exe</OutputType>");
                sw.WriteLine($"<TargetFramework>{framework}</TargetFramework>");
                sw.Write("<AdditionalLibPaths>C:/Program Files/dotnet/shared/Microsoft.NETCore.App/6.0.8</AdditionalLibPaths>");
                foreach (var i in assemblyPaths)
                {
                    sw.WriteLine("<AdditionalLibPaths>" + i + "</AdditionalLibPaths>");
                }
                sw.WriteLine($"<PlatformTarget>{platform}</PlatformTarget>");
                sw.WriteLine("</PropertyGroup>");
                sw.WriteLine("<ItemGroup>");
                sw.WriteLine($"<Compile Remove=\"*.cs\"/>");
                sw.WriteLine($"<Compile Include=\"{csName}\"/>");
                foreach (var i in assemblyRefs)
                {
                    sw.WriteLine("<Reference Include=\"" + i + "\" />");
                }
                sw.WriteLine("</ItemGroup>");
                sw.WriteLine("</Project>");
            };
            Process? p = Process.Start("dotnet", new string[] { "build", csprojName });
            p?.WaitForExit();
            if (Directory.Exists(Utils.ToIdentifier(filenameLast)))
            {
                Directory.Delete(Utils.ToIdentifier(filenameLast), true);
            }
            Directory.CreateDirectory(Utils.ToIdentifier(filenameLast));
            try { File.Move($"bin/Debug/{framework}/{filenameLast}.deps.json", $"{Utils.ToIdentifier(filenameLast)}/{filenameLast}.deps.json"); } catch { }
            try { File.Move($"bin/Debug/{framework}/{filenameLast}.dll", $"{Utils.ToIdentifier(filenameLast)}/{filenameLast}.dll"); } catch { }
            try { File.Move($"bin/Debug/{framework}/{filenameLast}.exe", $"{Utils.ToIdentifier(filenameLast)}/{filenameLast}.exe"); } catch { }
            try { File.Move($"bin/Debug/{framework}/{filenameLast}.pdb", $"{Utils.ToIdentifier(filenameLast)}/{filenameLast}.pdb"); } catch { }
            try { File.Move($"bin/Debug/{framework}/{filenameLast}.runtimeconfig.json", $"{Utils.ToIdentifier(filenameLast)}/{filenameLast}.runtimeconfig.json"); } catch { }

        }
    }
}
