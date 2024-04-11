using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Text;

namespace Y1
{
    public class Compiler
    {
        public static string? filename, y1Code;
        public static List<string> assemblyRefs = new List<string>(), assemblyPaths = new List<string>(),
            standardY1Libs = new List<string>();
        public static string framework = "net6.0", platform = "anycpu", sdk = "Microsoft.NET.Sdk";
        public static Random rand = new Random((int)DateTime.Now.ToBinary());

        public static string GraveEscape(string escaped)
        {
            escaped = escaped
                .Replace("`E", "!")
                .Replace("`S", " ")
                .Replace("`N", "\n")
                .Replace("`T", "\t")
                .Replace("`R", "\r");
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

        public static string HandleYen(string match, string soFar)
        {
            string result = soFar;
            string matchContents = match;
            if (matchContents[1] == '\\')
            {
                char value = (char)int.Parse(matchContents.Substring(2, matchContents.Length - 3));
                result += value;
            }
            else if (matchContents[1] == '$')
            {
                string contents = matchContents.Substring(2, matchContents.Length - 3);
                Tuple<string, string>[] conditionedCommands =
                    contents.Split(";")
                    .Select(x => x.Trim())
                    .Select(x => new Tuple<string, string>(x.Split(':')[0], x.Split(':')[1]))
                    .ToArray();
                string command = "";
                int i = 0;
                while (true)
                {
                    if (i >= conditionedCommands.Length) break;
                    string[] lifoPieces = conditionedCommands[i]
                        .Item1
                        .Trim()
                        .Split(' ')
                        .Select(x => GraveEscape(x))
                        .ToArray();
                    Stack<string> stack = new Stack<string>();
                    foreach (string piece in lifoPieces)
                    {
                        if (piece == "@FileExists")
                        {
                            stack.Push(File.Exists(stack.Pop()) ? "true" : "false");
                        }
                        else if (piece == "@OSMatch")
                        {
                            OperatingSystem os = Environment.OSVersion;
                            string osString = os.Platform.ToString() + "-" + os.Version.ToString();
                            stack.Push(Regex.IsMatch(osString, stack.Pop()) ? "true" : "false");
                        }
                        else if (piece == "@OSVersionMatch")
                        {
                            Version version = Environment.OSVersion.Version;
                            string matcher = stack.Pop();
                            Version matcherVersion = new Version(matcher.Substring(1));
                            if (matcher[0] == '=')
                            {
                                stack.Push(version == matcherVersion ? "true" : "false");
                            }
                            else if (matcher[0] == '>')
                            {
                                stack.Push(version > matcherVersion ? "true" : "false");
                            }
                            else if (matcher[0] == '<')
                            {
                                stack.Push(version < matcherVersion ? "true" : "false");
                            }
                            if (matcher[0] == '{')
                            {
                                stack.Push(version <= matcherVersion ? "true" : "false");
                            }
                            if (matcher[0] == '}')
                            {
                                stack.Push(version >= matcherVersion ? "true" : "false");
                            }
                        }
                        else if (piece == "@And")
                        {
                            string a = stack.Pop();
                            string b = stack.Pop();
                            stack.Push((a == "true" && b == "true") ? "true" : "false");
                        }
                        else if (piece == "@Or")
                        {
                            string a = stack.Pop();
                            string b = stack.Pop();
                            stack.Push((a == "true" || b == "true") ? "true" : "false");
                        }
                        else if (piece == "@Not")
                        {
                            string a = stack.Pop();
                            stack.Push(a == "true" ? "false" : "true");
                        }
                        else
                        {
                            stack.Push(piece);
                        }
                    }
                    string boolean = stack.Pop();
                    if (boolean == "true")
                    {
                        command = conditionedCommands[i].Item2.Trim();
                        break;
                    }
                    i++;
                }
                if (command != "")
                {
                    string[] commandParts = command.Split(' ').Select(x => GraveEscape(x)).ToArray();
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.RedirectStandardOutput = true;
                    psi.FileName = GraveEscape(commandParts[0]);
                    psi.Arguments = GraveEscape(commandParts[1]);
                    Process process = new Process();
                    process.StartInfo = psi;
                    process.Start();
                    StreamReader reader = process.StandardOutput;
                    string output = reader.ReadToEnd();
                    process.WaitForExit();
                    result += output;
                }
            }
            return result;
        }

        public static string Prepreprocess(string unpp)
        {
            string result = "";
            Regex yenRegex = new Regex("¥.[^\\]]*]");
            MatchCollection yenMatches = yenRegex.Matches(unpp);
            foreach (Match yenMatch in yenMatches) Console.WriteLine("PPP Match: " + yenMatch.Value);
            string[] withoutYen = yenRegex.Split(unpp);
            for (int i = 0; i < withoutYen.Length; i++)
            {
                result += withoutYen[i];
                if (i < yenMatches.Count) result = HandleYen(yenMatches[i].Value, result);
            }
            return result;
        }

        public static List<string> Preprocess(List<string> y1CodeSplit)
        {
            string input = "";
            List<string> filesToDelete = new List<string>();
            List<string> result = new List<string>();
            var macros = new Dictionary<string, List<string>>();
        TryAgain:
            for (int i = 0; i < y1CodeSplit.Count; i++)
            {
                string trimmed = y1CodeSplit[i].Trim();
                if (trimmed.Split(' ')[0] == "?File")
                {
                    string contents;
                    using (StreamReader sr = new StreamReader(trimmed.Split(' ')[1]))
                    {
                        contents = sr.ReadToEnd();
                    }
                    foreach (var j in contents.Split(new char[] { '\n', '\r', '\f', '\v' }))
                    {
                        if (j != "")
                        {
                            result.Add(j);
                        }
                    }
                }
                else if (trimmed.Split(' ')[0] == "?Define")
                {
                    List<string> contents = new List<string>();
                    string name = trimmed.Split(' ')[1];
                    i++;
                    while (y1CodeSplit[i].Trim() != "?")
                    {
                        contents.Add(
                          y1CodeSplit[i].Substring(y1CodeSplit[i].IndexOf(":") + 1).Trim()
                        );
                        i++;
                    }
                    macros.Add(name, contents);
                }
                else if (trimmed.Split(' ')[0] == "?Call")
                {
                    if (macros.ContainsKey(trimmed.Split(' ')[1]))
                    {
                        List<string> contents = macros[trimmed.Split(' ')[1]];
                        for (int j = 1; j < trimmed.Split("!!").Length; j++)
                        {
                            List<string> unreplaced = contents;
                            contents = new List<string>();
                            foreach (var k in unreplaced)
                            {
                                if (k != "")
                                {
                                    contents.Add(k.Replace("?" + j + "?", trimmed.Split("!!")[j]).Replace("?!", "?"));
                                }
                            }
                        }
                        result.AddRange(contents);
                    }
                    else
                    {
                        result.Add(trimmed);
                    }
                }
                else if (trimmed.Split(' ')[0] == "?DefineShort")
                {
                    string name = trimmed.Split(' ')[1];
                    string contents = "";
                    for (int j = 2; j < trimmed.Split(' ').Length; j++)
                    {
                        contents += trimmed.Split(' ')[j] + " ";
                    }
                    i++;
                    while (i < y1CodeSplit.Count)
                    {
                        string newContents = contents;
                        Regex macroRegex = new Regex($"\\[\\[{name} (\\!\\!.*)*\\]\\]");
                        Match macroMatch = macroRegex.Match(y1CodeSplit[i]);
                        Group paramGroup = macroMatch.Groups[1];
                        string[] paramGroupValues = paramGroup.Value.Split("!!");
                        for (int j = 1; j < paramGroupValues.Length; j++)
                        {
                            newContents = newContents.Replace($"?{j}?", paramGroupValues[j]);
                        }
                        result.Add(macroRegex.Replace(y1CodeSplit[i], newContents));
                        i++;
                    }
                }
                else if (trimmed.Split(' ')[0] == "?Rewrite")
                {
                    string? originalFilename = filename;
                    string outputStreamName = trimmed.Split(' ')[1];
                    List<string> rewriter = new List<string>();
                    List<string> toRewrite = new List<string>();
                    i++;
                    while (y1CodeSplit[i].Trim() != "?")
                    {
                        rewriter.Add(y1CodeSplit[i].Trim().Replace("?!", "?"));
                        i++;
                    }
                    i++;
                    while (y1CodeSplit[i].Trim() != "?")
                    {
                        toRewrite.Add(y1CodeSplit[i].Trim().Replace("?!", "?"));
                        i++;
                    }
                    string name = $"__Rewrite__{rand.NextInt64()}.y1";
                    using (var sw = new StreamWriter(name))
                    {
                        foreach (var j in rewriter)
                        {
                            sw.WriteLine(j);
                        }
                    }
                    Main(new string[] { name });

                    ProcessStartInfo info = new ProcessStartInfo();
                    info.FileName = ToIdentifier(name) + "/" + name + ".exe";
                    info.RedirectStandardInput = true;
                    info.RedirectStandardOutput = true;
                    info.CreateNoWindow = true;
                    Process? process = Process.Start(info);
                    StreamWriter? standardIn = process?.StandardInput;
                    StreamReader? standardOut = null;
                    if (outputStreamName == "Out")
                        standardOut = process?.StandardOutput;
                    else if (outputStreamName == "Err")
                        standardOut = process?.StandardError;
                    foreach (var j in toRewrite)
                    {
                        standardIn?.WriteLine(j);
                        standardIn?.Flush();
                        standardIn?.Dispose();
                    }
                    process?.WaitForExit();
                    List<string> rewritten = 
                        new List<string>(standardOut?.ReadToEnd()?.Split(
                            new char[] { '\n', '\r', '\v', '\f' }
                        ) ?? new string[]{}
                    );
                    result.AddRange(rewritten ?? new List<string>());
                    Directory.Delete(ToIdentifier(name), true);
                    File.Delete(name);
                    File.Delete($"{name}.csproj");
                    File.Delete($"{name}.~cs");
                    filename = originalFilename;
                }
                else if (trimmed.Split(' ')[0] == "?Undefine")
                {
                    macros.Remove(trimmed.Split(' ')[1]);
                }
                else if (trimmed.Split(' ')[0] == "?IfDefined")
                {
                    List<string> ifContents = new List<string>();
                    List<string> elseContents = new List<string>();
                    i++;
                    while (y1CodeSplit[i].Trim() != "?")
                    {
                        ifContents.Add(y1CodeSplit[i].Replace("?!", "?"));
                        i++;
                    }
                    i++;
                    while (y1CodeSplit[i].Trim() != "?")
                    {
                        elseContents.Add(y1CodeSplit[i].Replace("?!", "?"));
                        i++;
                    }
                    if (macros.ContainsKey(trimmed.Split(' ')[1]))
                        result.AddRange(ifContents);
                    else
                        result.AddRange(elseContents);
                }
                else if (trimmed.Split(' ')[0] == "?Defer")
                {
                    result.Add(trimmed.Substring(7));
                }
                else if (trimmed.Split(' ')[0] == "?WriteToFile")
                {
                    if (!File.Exists(trimmed.Split(' ')[1]))
                        filesToDelete.Add(trimmed.Split(' ')[1]);
                    using (var sw = new StreamWriter(trimmed.Split(' ')[1]))
                    {
                        foreach (var j in macros[trimmed.Split(' ')[2]])
                        {
                            sw.WriteLine(j.Replace("?!", "?"));
                        }
                        sw.Flush();
                    }
                }
                else if (trimmed.Split(' ')[0] == "?PreprocessorEnclose")
                {
                    List<string> contents = new List<string>();
                    i++;
                    while (y1CodeSplit[i].Trim() != "?")
                    {
                        contents.Add(y1CodeSplit[i].Replace("?!", "?"));
                        i++;
                    }
                    List<string> innerResult = Preprocess(contents);
                    result.AddRange(innerResult);
                }
                else if (trimmed.Split(' ')[0] == "?CondenseLines")
                {
                    string[] contents = trimmed.Split("!!");
                    for (int j = 1; j < contents.Length; j++)
                    {
                        result.Add(
                          GraveEscape(contents[j])
                        );
                    }
                }
                else if (trimmed.Split(' ')[0] == "?User_Diagnostic")
                {
                    string contents = trimmed.Split(' ')[1];
                    Console.Write(GraveEscape(contents));
                }
                else if (trimmed.Split(' ')[0] == "?User_Read")
                {
                    input += Console.Read();
                }
                else if (trimmed.Split(' ')[0] == "?User_IfChar")
                {
                    char inputChar = input[0];
                    char test = GraveEscape(trimmed.Split(' ')[1])[0];
                    List<string> ifContents = new List<string>();
                    List<string> elseContents = new List<string>();
                    i++;
                    while (y1CodeSplit[i].Trim() != "?")
                    {
                        ifContents.Add(
                          y1CodeSplit[i].Replace("?!", "?")
                        );
                        i++;
                    }
                    i++;
                    while (y1CodeSplit[i].Trim() != "?")
                    {
                        elseContents.Add(
                          y1CodeSplit[i].Replace("?!", "?")
                        );
                        i++;
                    }
                    if (inputChar == test)
                        result.AddRange(ifContents);
                    else
                        result.AddRange(elseContents);
                }
                else if (trimmed.Split(' ')[0] == "?User_DequeueInput")
                {
                    try
                    {
                        input = input.Substring(1);
                    }
                    catch { }
                }
                else
                {
                    result.Add(trimmed);
                }
            }
            bool noQuestionMarks = true;
            foreach (var i in result)
            {
                // Console.WriteLine(i);
                if (i.Trim().StartsWith("?"))
                    noQuestionMarks = false;
            }
            if (!noQuestionMarks)
            {
                y1CodeSplit = result;
                result = new List<string>();
                goto TryAgain;
            }
            return result;
        }
        public static string ConvertToCSharp(
            List<string> y1CodeSplit, 
            int depth, 
            bool outer = false,
            string mode = "run")
        {
            Func<int, string, string> startScope = (depth, prevCode) =>
            {
                string csCode = prevCode;
                csCode += @$"#pragma warning disable CS0168
var y1__stack_{depth} = new System.Collections.Generic.List<
    System.Tuple<
        System.Type, 
        System.Collections.Generic.Dictionary<string, object>, 
        System.Reflection.Emit.TypeBuilder, 
        System.Collections.Generic.Dictionary<string, System.Type>
    >
>();
System.Reflection.AssemblyName y1__aName_{depth} = new System.Reflection.AssemblyName(""y1__DynamicAssembly"");
System.Reflection.Emit.AssemblyBuilder y1__ab_{depth} = 
    System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(y1__aName_{depth}, 
    System.Reflection.Emit.AssemblyBuilderAccess.RunAndCollect
);
System.Reflection.Emit.ModuleBuilder y1__mb_{depth} = y1__ab_{depth}.DefineDynamicModule(y1__aName_{depth}.Name);
System.Reflection.Emit.ILGenerator y1__il_{depth};
dynamic y1__func_{depth};
dynamic y1__result_{depth};
#pragma warning restore CS0168
";
                return csCode;
            };
            string csCode = "";
            string modeArg = "";
            int startingPos = 0;
            if (outer)
            {
                if (y1CodeSplit[startingPos].Trim() == "<ATTRIBUTES>")
                {
                    startingPos++;
                    while (y1CodeSplit[startingPos].Trim() != "<END_ATTRIBUTES>")
                    {
                        if (y1CodeSplit[startingPos].Trim().StartsWith("Framework:"))
                        {
                            framework = y1CodeSplit[startingPos].Trim().Substring(10).Trim();
                        }
                        else if (y1CodeSplit[startingPos].Trim().StartsWith("Platform:"))
                        {
                            platform = y1CodeSplit[startingPos].Trim().Substring(9).Trim();
                        }
                        else if (y1CodeSplit[startingPos].Trim().StartsWith("SDK:"))
                        {
                            sdk = y1CodeSplit[startingPos].Trim().Substring(4).Trim();
                        }
                        startingPos++;
                    }
                    startingPos++;
                }
                if (y1CodeSplit[startingPos].Trim() == "<REFS>")
                {
                    startingPos++;
                    while (y1CodeSplit[startingPos].Trim() != "<END_REFS>")
                    {
                        if (y1CodeSplit[startingPos].Trim().StartsWith("'"))
                        {
                            assemblyPaths.Add(y1CodeSplit[startingPos].Trim().Substring(1));
                        }
                        else
                        {
                            assemblyRefs.Add(y1CodeSplit[startingPos].Trim());
                        }
                        startingPos++;
                    }
                    startingPos++;
                }
                if (y1CodeSplit[startingPos].Trim() == "<IMPORTS>")
                {
                    startingPos++;
                    while (y1CodeSplit[startingPos].Trim() != "<END_IMPORTS>")
                    {
                        csCode += "using " + y1CodeSplit[startingPos].Trim() + ";\n";
                        startingPos++;
                    }
                    startingPos++;
                }
                if (y1CodeSplit[startingPos].Trim() == "<STANDARD_Y1_LIBS>")
                {
                    startingPos++;
                    while (y1CodeSplit[startingPos].Trim() != "<END_STANDARD_Y1_LIBS>")
                    {
                        standardY1Libs.Add(y1CodeSplit[startingPos].Trim());
                        startingPos++;
                    }
                    startingPos++;
                }
                csCode += "\n";
                if (standardY1Libs.Contains("KeyListener"))
                    csCode += @"
namespace Y1
{
  public class KeyListener
  {
    public class KeyPressedEventArgs
    {
      public System.ConsoleKeyInfo key;

      public KeyPressedEventArgs(System.ConsoleKeyInfo key)
      {
        this.key = key;
      }
    }

    public static event System.EventHandler<KeyPressedEventArgs> KeyPressed;
    public static bool ShouldContinue = true;
    public static void ListenForKeys()
    {
      while (ShouldContinue)
      {
        KeyPressed?.Invoke(null, new KeyPressedEventArgs(System.Console.ReadKey(false)));
      }
      ShouldContinue = true;
    }
  }
}
";
            }
            
            for (int i = startingPos; i < y1CodeSplit.Count; i++)
            {
                string trimmed = y1CodeSplit[i].Trim();
                if (trimmed == "")
                    continue;
                //for single-line comments
                if (trimmed.StartsWith("'$.") || trimmed.StartsWith("'$:"))
                    continue;
                //for multi-line comments
                if (mode == "comment" || mode == "mcomment")
                {
                    if (trimmed.EndsWith("#]"))
                    {
                        if (mode == "comment")
                        {
                            mode = "run";
                        }
                        else if (mode == "mcomment")
                        {
                            mode = "methodbuild";
                        }
                    }
                    continue;
                }
                while (trimmed.EndsWith(",,"))
                {
                    trimmed = trimmed.Substring(0, trimmed.Length - 2);
                    i++;
                    while (y1CodeSplit[i].Trim() == "")
                    {
                        i++;
                    }
                    trimmed += y1CodeSplit[i].Trim();
                }
                if (mode == "run")
                {
                    if (trimmed.StartsWith("|/"))
                    {
                        csCode += "public static void " + trimmed.Substring(2) + "() {\n";
                        csCode = startScope(depth, csCode);
                    }
                    else if (trimmed == "\\|" || trimmed == "]@" || trimmed == "EndNamespace")
                    {
                        csCode += "}\n";
                    }
                    else if (trimmed.StartsWith("@["))
                    {
                        csCode += "public class " + ToIdentifier(trimmed.Substring(2)) + "\n";
                        csCode += "{\n";
                    }
                    //multi-line comment starter
                    else if (trimmed.StartsWith("[#"))
                    {
                        mode = "comment";
                    }
                    else if (trimmed == "PushNew")
                    {
                        csCode += "y1__stack_" + depth + @".Add(new System.Tuple<
        System.Type, 
        System.Collections.Generic.Dictionary<string, object>, 
        System.Reflection.Emit.TypeBuilder, 
        System.Collections.Generic.Dictionary<string, System.Type>
    >(" +
                            "null, null, null, null" +
                            "));\n";
                    }
                    else if (trimmed.Split(' ')[0] == "DefineType")
                    {
                        csCode += "{\n";
                        csCode += "var y1__arg = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1];\n";
                        csCode += "y1__stack_" + depth + ".RemoveAt(y1__stack_" + depth + ".Count - 1);\n";
                        csCode += "y1__stack_" + depth + @".Add(new System.Tuple<
        System.Type, 
        System.Collections.Generic.Dictionary<string, object>, 
        System.Reflection.Emit.TypeBuilder, 
        System.Collections.Generic.Dictionary<string, System.Type>
    >(y1__arg.Item1, y1__arg.Item2, y1__mb_" + depth + ".DefineType(" +
                            "\"" + ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                            "System.Reflection.TypeAttributes.Public" +
                            "), y1__arg.Item4));\n";
                        csCode += "}\n";
                    }
                    else if (trimmed.Split(' ')[0] == "DefineMethod")
                    {
                        csCode += "System.Reflection.Emit.MethodBuilder " + ToIdentifier(trimmed.Split(' ')[2]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item3.DefineMethod(" +
                            "\"" + ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                            "System.Reflection.MethodAttributes.Public, " +
                            "typeof(void), " +
                            "System.Type.EmptyTypes" +
                            ");\n";
                        csCode += "y1__il_" + depth + " = " + ToIdentifier(trimmed.Split(' ')[2]) + ".GetILGenerator();\n";
                        csCode += "{\n";
                        mode = "methodbuild";
                        modeArg = ToIdentifier(trimmed.Split(' ')[2]);
                    }
                    else if (trimmed == "FinishType")
                    {
                        csCode += "{\n";
                        csCode += "var y1__arg = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1];\n";
                        csCode += "y1__stack_" + depth + ".RemoveAt(y1__stack_" + depth + ".Count - 1);\n";
                        csCode += "y1__stack_" + depth + ".Add(" +
                            @"new System.Tuple<
        System.Type, 
        System.Collections.Generic.Dictionary<string, object>, 
        System.Reflection.Emit.TypeBuilder, 
        System.Collections.Generic.Dictionary<string, System.Type>
    >(" +
                            "y1__arg.Item3.CreateType(), " +
                            "new System.Collections.Generic.Dictionary<string, object>(), " +
                            "y1__arg.Item3, " +
                            "new System.Collections.Generic.Dictionary<string, System.Type>()" +
                            "));\n";
                        csCode += "}\n";
                    }
                    else if (trimmed.Split(' ')[0] == "CreateObject")
                    {
                        csCode += "y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item2.Add(" +
                            "\"" + ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                            "System.Activator.CreateInstance(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item1, null" +
                            "));\n";
                    }
                    else if (trimmed.Split(' ')[0] == "CallMethod")
                    {
                        csCode += "y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item1.GetMethod(\"" +
                            trimmed.Split(' ')[1] + "\").Invoke(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item2[\"" +
                            ToIdentifier(trimmed.Split(' ')[2]) + "\"], null);\n";
                    }
                    else if (trimmed.Split(' ')[0] == "Roll")
                    {
                        csCode += "y1__stack_" + depth + ".Add(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - " + trimmed.Split(' ')[1] + "]);\n";
                        csCode += "y1__stack_" + depth + ".RemoveAt(y1__stack_" + depth + ".Count - (" + trimmed.Split(' ')[1] + " + 1));\n";
                    }
                    else if (trimmed.Split(' ')[0] == "ReverseRoll")
                    {
                        csCode += "y1__stack_" + depth + ".Insert(y1__stack_" + depth + ".Count - " + trimmed.Split(' ')[1] + ", y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1]);\n";
                        csCode += "y1__stack_" + depth + ".RemoveAt(y1__stack_" + depth + ".Count - 1);\n";
                    }
                    else if (trimmed == "Drop")
                    {
                        csCode += "y1__stack_" + depth + ".RemoveAt(y1__stack_" + depth + ".Count - 1);\n";
                    }
                    else if (trimmed.Split(' ')[0] == "LoadType")
                    {
                        csCode += "y1__stack_" + depth + @".Add(new System.Tuple<
        System.Type, 
        System.Collections.Generic.Dictionary<string, object>, 
        System.Reflection.Emit.TypeBuilder, 
        System.Collections.Generic.Dictionary<string, System.Type>
    >(typeof(" + trimmed.Split(' ')[1] + "), new System.Collections.Generic.Dictionary<string,object>(), " +
                            "null, new System.Collections.Generic.Dictionary<string, Type>()));\n";
                    }
                    else if (trimmed == "ObjParams")
                    {
                        string name = "";
                        string parameters = "";
                        i++;
                        name = y1CodeSplit[i].Trim();
                        i++;
                        parameters = y1CodeSplit[i].Trim();
                        while (parameters.EndsWith(",,"))
                        {
                            parameters = parameters.Substring(0, parameters.Length - 2);
                            i++;
                            while (y1CodeSplit[i].Trim() == "")
                            {
                                i++;
                            }
                            parameters += y1CodeSplit[i].Trim();
                        }
                        csCode += "y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item2.Add(" +
                           "\"" + name + "\", " +
                           "System.Activator.CreateInstance(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item1, " + parameters +
                           "));\n";
                    }
                    else if (trimmed == "MethodParams")
                    {
                        string methodName;
                        string name;
                        string parameters;
                        string types;
                        i++;
                        methodName = y1CodeSplit[i].Trim();
                        i++;
                        types = y1CodeSplit[i].Trim();
                        i++;
                        name = y1CodeSplit[i].Trim();
                        i++;
                        parameters = y1CodeSplit[i].Trim();
                        while (parameters.EndsWith(",,"))
                        {
                            parameters = parameters.Substring(0, parameters.Length - 2);
                            i++;
                            while (y1CodeSplit[i].Trim() == "")
                            {
                                i++;
                            }
                            parameters += y1CodeSplit[i].Trim();
                        }
                        csCode += "y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item1.GetMethod(\"" +
                            methodName + "\", " + types + ").Invoke(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item2[\"" +
                            name + "\"], " + parameters + ");\n";

                    }
                    else if (trimmed.Split(' ')[0] == "DefineField")
                    {
                        csCode += "System.Reflection.Emit.FieldBuilder " + ToIdentifier(trimmed.Split(' ')[1]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item3.DefineField(\"" +
                            ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                            trimmed.Split(' ')[2] + ", System.Reflection.FieldAttributes.Public);\n";
                    }
                    else if (trimmed.Split(' ')[0] == "DefineParamMethod")
                    {
                        i++;
                        string paramTypes = y1CodeSplit[i].Trim();
                        while (paramTypes.EndsWith(",,"))
                        {
                            paramTypes = paramTypes.Substring(0, paramTypes.Length - 2);
                            i++;
                            while (y1CodeSplit[i].Trim() == "")
                            {
                                i++;
                            }
                            paramTypes += y1CodeSplit[i].Trim();
                        }
                        csCode += "System.Reflection.Emit.MethodBuilder " + ToIdentifier(trimmed.Split(' ')[2]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item3.DefineMethod(" +
                           "\"" + ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                           "System.Reflection.MethodAttributes.Public, " +
                           "typeof(void), " +
                           paramTypes +
                           ");\n";
                        csCode += "y1__il_" + depth + " = " + ToIdentifier(trimmed.Split(' ')[2]) + ".GetILGenerator();\n";
                        csCode += "{\n";
                        mode = "methodbuild";
                        modeArg = ToIdentifier(trimmed.Split(' ')[2]);
                    }
                    else if (trimmed.Split(' ')[0] == "LoadField")
                    {
                        csCode += "System.Reflection.FieldInfo " + ToIdentifier(trimmed.Split(' ')[2]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item1.GetField(\"" +
                            trimmed.Split(' ')[1] + "\");\n";
                    }
                    else if (trimmed.Split(' ')[0] == "DefineComplexMethod")
                    {
                        i++;
                        string types = y1CodeSplit[i].Trim();
                        while (types.EndsWith(",,"))
                        {
                            types = types.Substring(0, types.Length - 2);
                            i++;
                            while (y1CodeSplit[i].Trim() == "")
                            {
                                i++;
                            }
                            types += y1CodeSplit[i].Trim();
                        }
                        i++;
                        string returnType = y1CodeSplit[i].Trim();
                        i++;
                        string methodAttributes = "0";
                        if (y1CodeSplit[i].Contains("public"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Public";
                        }
                        if (y1CodeSplit[i].Contains("private"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Private";
                        }
                        if (y1CodeSplit[i].Contains("static"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Static";
                        }
                        if (y1CodeSplit[i].Contains("abstract"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Abstract";
                        }
                        if (y1CodeSplit[i].Contains("protected"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Family";
                        }
                        if (y1CodeSplit[i].Contains("virtual"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Virtual";
                        }
                        if (y1CodeSplit[i].Contains("final"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Final";
                        }
                        csCode += "System.Reflection.Emit.MethodBuilder " + ToIdentifier(trimmed.Split(' ')[2]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item3.DefineMethod(" +
                           "\"" + ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                           methodAttributes + ", " +
                           returnType + ", " +
                           types +
                           ");\n";
                        if (!y1CodeSplit[i].Contains("abstract"))
                        {
                            csCode += "y1__il_" + depth + " = " + ToIdentifier(trimmed.Split(' ')[2]) + ".GetILGenerator();\n";
                            csCode += "{\n";
                            mode = "methodbuild";
                            modeArg = ToIdentifier(trimmed.Split(' ')[2]);
                        }
                    }
                    else if (trimmed.Split(' ')[0] == "DefineComplexField")
                    {
                        i++;
                        string fieldAttributes = "0";
                        if (y1CodeSplit[i].Contains("public"))
                        {
                            fieldAttributes += " | System.Reflection.FieldAttributes.Public";
                        }
                        if (y1CodeSplit[i].Contains("private"))
                        {
                            fieldAttributes += " | System.Reflection.FieldAttributes.Private";
                        }
                        if (y1CodeSplit[i].Contains("static"))
                        {
                            fieldAttributes += " | System.Reflection.FieldAttributes.Static";
                        }
                        csCode += "System.Reflection.Emit.FieldBuilder " + ToIdentifier(trimmed.Split(' ')[1]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item3.DefineField(\"" +
                            ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                            trimmed.Split(' ')[2] + ", " + fieldAttributes + ");\n";
                    }
                    else if (trimmed.Split(' ')[0] == "Subclass")
                    {
                        csCode += "y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item4.Add(\"" +
                             trimmed.Split(' ')[2] + "\", y1__mb_" + depth + ".DefineType(\"" + trimmed.Split(' ')[1] +
                             "\", System.Reflection.TypeAttributes.Public, y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item1));\n";
                    }
                    else if (trimmed.Split(' ')[0] == "DefineSubclassMethod")
                    {
                        i++;
                        string types = y1CodeSplit[i].Trim();
                        while (types.EndsWith(",,"))
                        {
                            types = types.Substring(0, types.Length - 2);
                            i++;
                            while (y1CodeSplit[i].Trim() == "")
                            {
                                i++;
                            }
                            types += y1CodeSplit[i].Trim();
                        }
                        i++;
                        string returnType = y1CodeSplit[i].Trim();
                        i++;
                        string methodAttributes = "0";
                        if (y1CodeSplit[i].Contains("public"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Public";
                        }
                        if (y1CodeSplit[i].Contains("private"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Private";
                        }
                        if (y1CodeSplit[i].Contains("static"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Static";
                        }
                        if (y1CodeSplit[i].Contains("abstract"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Abstract";
                        }
                        if (y1CodeSplit[i].Contains("protected"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Family";
                        }
                        if (y1CodeSplit[i].Contains("virtual"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Virtual";
                        }
                        if (y1CodeSplit[i].Contains("final"))
                        {
                            methodAttributes += " | System.Reflection.MethodAttributes.Final";
                        }
                        csCode += "System.Reflection.Emit.MethodBuilder " + ToIdentifier(trimmed.Split(' ')[3]) + " = ((System.Reflection.Emit.TypeBuilder)(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1]" +
                           ".Item4[\"" + trimmed.Split(' ')[1] + "\"])).DefineMethod(" +
                           "\"" + ToIdentifier(trimmed.Split(' ')[2]) + "\", " +
                           methodAttributes + ", " +
                           returnType + ", " +
                           types +
                           ");\n";
                        if (!y1CodeSplit[i].Contains("abstract"))
                        {
                            csCode += "y1__il_" + depth + " = " + ToIdentifier(trimmed.Split(' ')[3]) + ".GetILGenerator();\n";
                            csCode += "{\n";
                            mode = "methodbuild";
                            modeArg = ToIdentifier(trimmed.Split(' ')[3]);
                        }
                    }
                    else if (trimmed.Split(' ')[0] == "DefineSubclassField")
                    {
                        i++;
                        string fieldAttributes = "0";
                        if (y1CodeSplit[i].Contains("public"))
                        {
                            fieldAttributes += " | System.Reflection.FieldAttributes.Public";
                        }
                        if (y1CodeSplit[i].Contains("private"))
                        {
                            fieldAttributes += " | System.Reflection.FieldAttributes.Private";
                        }
                        if (y1CodeSplit[i].Contains("static"))
                        {
                            fieldAttributes += " | System.Reflection.FieldAttributes.Static";
                        }
                        csCode += "System.Reflection.Emit.FieldBuilder " + ToIdentifier(trimmed.Split(' ')[2]) + " = ((System.Reflection.Emit.TypeBuilder)(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item4[\"" +
                            trimmed.Split(' ')[1] + "\"])).DefineField(\"" +
                            ToIdentifier(trimmed.Split(' ')[2]) + "\", " +
                            trimmed.Split(' ')[3] + ", " + fieldAttributes + ");\n";
                    }
                    else if (trimmed.Split(' ')[0] == "FinishSubclass")
                    {
                        csCode += "y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item4[\"" + trimmed.Split(' ')[1] +
                            "\"] = ((System.Reflection.Emit.TypeBuilder)(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item4[\"" + trimmed.Split(' ')[1] +
                            "\"])).CreateType();\n";
                    }
                    else if (trimmed.Split(' ')[0] == "CreateSubclassObject")
                    {
                        csCode += "y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item2.Add(\"" +
                            ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                            "System.Activator.CreateInstance(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item4[\"" + trimmed.Split(' ')[2] + "\"], null" +
                            "));\n";
                    }
                    else if (trimmed.Split(' ')[0] == "CallSubclassMethod")
                    {
                        csCode += "y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item4[\"" + trimmed.Split(' ')[3] + "\"].GetMethod(\"" +
                            trimmed.Split(' ')[1] + "\").Invoke(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item2[\"" +
                            ToIdentifier(trimmed.Split(' ')[2]) + "\"], null);\n";
                    }
                    else if (trimmed.Split(' ')[0] == "Summation")
                    {
                        int start = Int32.Parse(trimmed.Split(' ')[1]);
                        int end = Int32.Parse(trimmed.Split(' ')[2]);
                        string paramName = trimmed.Split(' ')[3];
                        List<string> funcLines = new List<string>();
                        i++;
                        int summationDepth = 1;
                        while (summationDepth > 0)
                        {
                            funcLines.Add(y1CodeSplit[i].Trim());
                            if (y1CodeSplit[i].Trim().StartsWith("Summation")) summationDepth++;
                            if (y1CodeSplit[i].Trim() == "EndSummation") summationDepth--;
                            i++;
                        }
                        funcLines.RemoveAt(funcLines.Count - 1);
                        string seqCode = ConvertToCSharp(funcLines, depth + 1);
                        csCode += "y1__func_" + depth + " = (System.Func<int, dynamic>)((" + paramName + ") => {\n";
                        csCode = startScope(depth + 1, csCode);
                        csCode += seqCode;
                        csCode += "});\n";
                        csCode += "y1__result_" + depth + " = y1__func_" + depth + "(" + start + ");\n";
                        csCode += "for (int y1__counter_" + depth + " = " + (start + 1) + "; y1__counter_" + depth + " < " + end + "; y1__counter_" + depth + "++)\n";
                        csCode += "{\n";
                        csCode += "y1__result_" + depth + " += y1__func_" + depth + "(y1__counter_" + depth + ");\n";
                        csCode += "}\n";
                        i--;
                    }
                    else if (trimmed.Split(' ')[0] == "DefineVariable")
                    {
                        csCode += "dynamic " + trimmed.Split(' ')[1] + " = ";
                        for (int j = 2; j < trimmed.Split(' ').Length; j++)
                        {
                            csCode += trimmed.Split(' ')[j] + " ";
                        }
                        csCode += ";\n";
                    }
                    else if (trimmed.Split(' ')[0] == "Condition")
                    {
                        csCode += "if (" + trimmed.Substring(trimmed.IndexOf(' ') + 1) + ")\n";
                        csCode += "{\n";
                        List<string> lines = new List<string>();
                        int condDepth = 1;
                        i++;
                        while (condDepth > 0)
                        {
                            lines.Add(y1CodeSplit[i]);
                            if (y1CodeSplit[i].Trim().Split(' ')[0] == "Condition") condDepth++;
                            if (y1CodeSplit[i].Trim() == "EndCondition") condDepth--;
                            i++;
                        }
                        i--;
                        lines.RemoveAt(lines.Count - 1);
                        csCode += ConvertToCSharp(lines, depth);
                        csCode += "}\n";
                    }
                    else if (trimmed.Split(' ')[0] == "While")
                    {
                        csCode += "while (" + trimmed.Substring(trimmed.IndexOf(' ') + 1) + ")\n";
                        csCode += "{\n";
                        List<string> lines = new List<string>();
                        int condDepth = 1;
                        i++;
                        while (condDepth > 0)
                        {
                            lines.Add(y1CodeSplit[i]);
                            if (y1CodeSplit[i].Trim().Split(' ')[0] == "While") condDepth++;
                            if (y1CodeSplit[i].Trim() == "EndWhile") condDepth--;
                            i++;
                        }
                        i--;
                        lines.RemoveAt(lines.Count - 1);
                        csCode += ConvertToCSharp(lines, depth);
                        csCode += "}\n";
                    }
                    else if (trimmed == "DoMulti")
                    {
                        csCode += "System.Threading.Tasks.Task.WaitAll(new System.Threading.Tasks.Task[] {\n";
                        List<string> lines = new List<string>();
                        int multiDepth = 1;
                        i++;
                        while (multiDepth > 0)
                        {
                            lines.Add(y1CodeSplit[i]);
                            if (y1CodeSplit[i].Trim() == "DoMulti") multiDepth++;
                            if (y1CodeSplit[i].Trim() == "EndMulti") multiDepth--;
                            if (multiDepth == 1 && y1CodeSplit[i].Trim() == "AndMulti")
                            {
                                lines.RemoveAt(lines.Count - 1);
                                csCode += "System.Threading.Tasks.Task.Run(((System.Action)(() => {\n";
                                csCode = startScope(depth + 1, csCode);
                                csCode += ConvertToCSharp(lines, depth + 1);
                                csCode += "}))),\n";
                                lines = new List<string>();
                            }
                            i++;
                        }
                        i--;
                        lines.RemoveAt(lines.Count - 1);
                        csCode += "System.Threading.Tasks.Task.Run(((Action)(() => {\n";
                        csCode = startScope(depth + 1, csCode);
                        csCode += ConvertToCSharp(lines, depth + 1);
                        csCode += "}))),\n";
                        csCode += "});\n";
                    }
                    else if (trimmed.Split(' ')[0] == "DefineComplexType")
                    {
                        string name = trimmed.Split(' ')[1];
                        i++;
                        string superclass = y1CodeSplit[i].Trim();
                        i++;
                        string interfaces = y1CodeSplit[i].Trim();
                        i++;
                        string typeAttributes = "0";
                        if (y1CodeSplit[i].Contains("public"))
                        {
                            typeAttributes += " | System.Reflection.TypeAttributes.Public";
                        }
                        if (y1CodeSplit[i].Contains("abstract"))
                        {
                            typeAttributes += " | System.Reflection.TypeAttributes.Abstract";
                        }
                        if (y1CodeSplit[i].Contains("sealed"))
                        {
                            typeAttributes += " | System.Reflection.TypeAttributes.Sealed";
                        }
                        if (y1CodeSplit[i].Contains("interface"))
                        {
                            typeAttributes += " | System.Reflection.TypeAttributes.Interface";
                        }
                        csCode += "{\n";
                        csCode += "var y1__arg = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1];\n";
                        csCode += "y1__stack_" + depth + ".RemoveAt(y1__stack_" + depth + ".Count - 1);\n";
                        csCode += "y1__stack_" + depth + @".Add(new System.Tuple<
        System.Type, 
        System.Collections.Generic.Dictionary<string, object>, 
        System.Reflection.Emit.TypeBuilder, 
        System.Collections.Generic.Dictionary<string, System.Type>
    >(y1__arg.Item1, y1__arg.Item2, y1__mb_" + depth + ".DefineType(" +
                            "\"" + name + "\", " +
                            typeAttributes + ", " + superclass + ", " + interfaces + "), y1__arg.Item4));\n";
                        csCode += "}\n";
                    }
                    else if (trimmed == "ListenForKeys" && standardY1Libs.Contains("KeyListener"))
                    {
                        int lfkDepth = 1;
                        List<Tuple<string,List<string>>> lines = new List<Tuple<string, List<string>>>
                        {
                            { new Tuple<string, List<string>>("", new List<string>()) }
                        };
                        i++;
                        while (lfkDepth > 0)
                        {
                            lines[lines.Count - 1].Item2.Add(y1CodeSplit[i].Trim());
                            if (y1CodeSplit[i].Trim() == "ListenForKeys") lfkDepth++;
                            if (y1CodeSplit[i].Trim() == "EndListenForKeys") lfkDepth--;
                            if (lfkDepth == 1 && y1CodeSplit[i].Trim().StartsWith("KeyCase"))
                            {
                                lines[lines.Count - 1].Item2.RemoveAt(lines[lines.Count - 1].Item2.Count - 1);
                                lines.Add(new Tuple<string, List<string>>(y1CodeSplit[i].Trim().Substring(8), new List<string>()));
                            }
                            i++;
                        }
                        lines[lines.Count - 1].Item2.RemoveAt(lines[lines.Count - 1].Item2.Count - 1);
                        i--;
                        csCode += "{\n";
                        csCode += "System.EventHandler<Y1.KeyListener.KeyPressedEventArgs> y1__keyListenerHandler_" + depth + 
                            " = (y1__keyListenerHandlerSender_" + depth + ", y1__keyListenerHandlerArgs_" + depth + 
                            ") => {\n";
                        csCode = startScope(depth + 1, csCode);
                        csCode += ConvertToCSharp(lines[0].Item2, depth + 1);
                        for (int j = 1; j < lines.Count; j++)
                        {
                            csCode += "if (y1__keyListenerHandlerArgs_" + depth + ".key == (" + lines[j].Item1 + "))\n";
                            csCode += "{\n";
                            csCode += ConvertToCSharp(lines[j].Item2, depth + 1);
                            csCode += "}\n";
                        }
                        csCode += "};\n";
                        csCode += "Y1.KeyListener.KeyPressed += y1__keyListenerHandler_" + depth + ";\n";
                        csCode += "Y1.KeyListener.ListenForKeys();";
                        csCode += "Y1.KeyListener.KeyPressed -= y1__keyListenerHandler_" + depth + ";\n";
                        csCode += "}\n";
                    }
                    else if (trimmed.Split(' ')[0] == "Namespace")
                    {
                        csCode += "namespace " + trimmed.Split(' ')[1] + "{\n";
                    }
                    else if (trimmed.StartsWith("C# - "))
                    {
                        csCode += trimmed.Substring(5);
                        csCode += "\n";
                    }
                    else
                    {
                        Console.WriteLine($"Syntax error: Line {i}. Contents: {trimmed}");
                    }
                }
                else if (mode == "methodbuild")
                {
                    if (trimmed == "\\/")
                    {
                        csCode += "}\n";
                        mode = "run";
                    }
                    else if (trimmed.StartsWith("[#"))
                    {
                        mode = "mcomment";
                    }
                    else if (trimmed.StartsWith("\\"))
                    {
                        csCode += "y1__il_" + depth + ".Emit(System.Reflection.Emit.OpCodes." + trimmed.Substring(1) + ");\n";
                    }
                    else if (trimmed.StartsWith("->"))
                    {
                        csCode += "System.Reflection.Emit.Label " + trimmed.Substring(2) + " = y1__il_" + depth + ".DefineLabel();\n";
                    }
                    else if (trimmed.StartsWith("-->"))
                    {
                        csCode += "y1__il_" + depth + ".MarkLabel(" + trimmed.Substring(3) + ");\n";
                    }
                    else if (trimmed.StartsWith("_!"))
                    {
                        if (trimmed[2] == '!')
                        {
                            csCode += "y1__il_" + depth + ".DeclareLocal(typeof(" + trimmed.Substring(3) + "));\n";
                        }
                        else
                        {
                            csCode += "y1__il_" + depth + ".DeclareLocal(" + trimmed.Substring(2) + ");\n";
                        }
                    }
                    else if (trimmed.StartsWith("<TRY>"))
                    {
                        csCode += "System.Reflection.Emit.Label " + trimmed.Substring(5).Trim() + " = y1__il_" + depth + ".BeginExceptionBlock();\n";
                    }
                    else if (trimmed.StartsWith("<CATCH>"))
                    {
                        csCode += "y1__il_" + depth + ".BeginCatchBlock(typeof(" + trimmed.Substring(7).Trim() + "));\n";
                    }
                    else if (trimmed == "<FINALLY>")
                    {
                        csCode += "y1__il_" + depth + ".BeginFinallyBlock();\n";
                    }
                    else if (trimmed == "<FAULT>")
                    {
                        csCode += "y1__il_" + depth + ".BeginFaultBlock();\n";
                    }
                    else if (trimmed == "<FILTER>")
                    {
                        csCode += "y1__il_" + depth + ".BeginExceptFilterBlock();\n";
                    }
                    else if (trimmed == "<END>")
                    {
                        csCode += "y1__il_" + depth + ".EndExceptionBlock();\n";
                    }
                    else if (trimmed.StartsWith("C# - "))
                    {
                        csCode += trimmed.Substring(5);
                        csCode += "\n";
                    }
                    else if (trimmed.Split(' ')[0] == "Condition")
                    {
                        csCode += "if (" + trimmed.Substring(trimmed.IndexOf(' ') + 1) + ")\n";
                        csCode += "{\n";
                        List<string> lines = new List<string>();
                        int condDepth = 1;
                        i++;
                        while (condDepth > 0)
                        {
                            lines.Add(y1CodeSplit[i]);
                            if (y1CodeSplit[i].Trim().Split(' ')[0] == "Condition") condDepth++;
                            if (y1CodeSplit[i].Trim() == "EndCondition") condDepth--;
                            i++;
                        }
                        i--;
                        lines.RemoveAt(lines.Count - 1);
                        csCode += ConvertToCSharp(lines, depth, false, "methodbuild");
                        csCode += "}\n";
                    }
                    else if (trimmed.Split(' ')[0] == "While")
                    {
                        csCode += "while (" + trimmed.Substring(trimmed.IndexOf(' ') + 1) + ")\n";
                        csCode += "{\n";
                        List<string> lines = new List<string>();
                        int condDepth = 1;
                        i++;
                        while (condDepth > 0)
                        {
                            lines.Add(y1CodeSplit[i]);
                            if (y1CodeSplit[i].Trim().Split(' ')[0] == "While") condDepth++;
                            if (y1CodeSplit[i].Trim() == "EndWhile") condDepth--;
                            i++;
                        }
                        i--;
                        lines.RemoveAt(lines.Count - 1);
                        csCode += ConvertToCSharp(lines, depth, false, "methodbuild");
                        csCode += "}\n";
                    }
                    else
                    {
                        Console.WriteLine("Syntax error: Line " + i);
                    }
                }
            }
            return csCode;
        }

        public static void Main(string[] args)
        {
            //Fix old versions having "multiple entry points"
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
            {
                isLibrary = true;
            }
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
            if (y1Code.StartsWith("^^$")) y1Code = y1Code.Substring(3).Replace("###Yen;", "¥");
            y1Code = Prepreprocess(y1Code);
            string[] y1CodeSplitBlankLines = y1Code.Split('\n', '\r', '\f', '\v');
            List<string> y1CodeSplitBlankLinesRemoved = new List<string>();
            foreach (string i in y1CodeSplitBlankLines)
            {
                if (i != "")
                {
                    y1CodeSplitBlankLinesRemoved.Add(i);
                }
            }
            string csCode = ConvertToCSharp(Preprocess(y1CodeSplitBlankLinesRemoved), 0, true);
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
            Process? p = Process.Start("dotnet", new string[]{"build", csprojName});
            p?.WaitForExit();
            if (Directory.Exists(ToIdentifier(filenameLast)))
            {
                Directory.Delete(ToIdentifier(filenameLast), true);
            }
            Directory.CreateDirectory(ToIdentifier(filenameLast));
            try { File.Move($"bin/Debug/{framework}/{filenameLast}.deps.json",          $"{ToIdentifier(filenameLast)}/{filenameLast}.deps.json"); } catch {}
            try { File.Move($"bin/Debug/{framework}/{filenameLast}.dll",                $"{ToIdentifier(filenameLast)}/{filenameLast}.dll"); } catch {}
            try { File.Move($"bin/Debug/{framework}/{filenameLast}.exe",                $"{ToIdentifier(filenameLast)}/{filenameLast}.exe"); } catch {}
            try { File.Move($"bin/Debug/{framework}/{filenameLast}.pdb",                $"{ToIdentifier(filenameLast)}/{filenameLast}.pdb"); } catch {}
            try { File.Move($"bin/Debug/{framework}/{filenameLast}.runtimeconfig.json", $"{ToIdentifier(filenameLast)}/{filenameLast}.runtimeconfig.json"); } catch {}
        }
    }
}
