using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace Kronosta.Language.Y1
{
    public partial class Preprocessor
    {
        #region Helper classes
        public class PreprocessorException : Exception
        {
            public PreprocessorException()
            {
            }

            public PreprocessorException(string message)
                : base(message)
            {
            }

            public PreprocessorException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }

        public delegate void Directive(
                Preprocessor prep,
                List<string> codeSplit,
                List<string> result,
                FauxRefParameter<int> lineIndex,
                object state
            );
        #endregion

        #region Common fields
        public bool DoLogging = false;
        public IDictionary<string, object> customState = new Dictionary<string, object>();
        public Compiler Compiler;
        #endregion

        #region Pre(1)processing
        public IDictionary<string, List<string>> Macros;
        public readonly Registry<Directive> Directives;


        public Preprocessor()
        {
            Macros = new Dictionary<string, List<string>>();
            Directives = new Registry<Directive>();
            RegisterDefaultDirectives();
        }

        public void RegisterDefaultDirectives()
        {
            Directives.Register(
                "", "File",
                static (prep, y1CodeSplit, result, ii, state) =>
                {
                    int i = ii.Value;
                    string trimmed = y1CodeSplit[i].Trim();
                    string filename = trimmed.Split(' ')[1];
                    string contents;
                    if (prep.Compiler.FakeFiles.ContainsKey(filename))
                    {
                        contents = prep.Compiler.FakeFiles[filename];
                    }
                    else
                    {
                        using (StreamReader sr = new StreamReader(filename))
                        {
                            contents = sr.ReadToEnd();
                            contents = contents.Replace("\r\n", "\n");
                        }
                    }
                    foreach (var j in contents.Split(new char[] { '\n', '\r', '\f', '\v' }))
                    {
                        if (j != "")
                        {
                            result.Add(j);
                        }
                    }
                }
            );

            Directives.Register(
                "", "Define",
                static (prep, codeSplit, result, lineIndexR, state) =>
                {
                    int lineIndex = lineIndexR.Value;
                    string name = codeSplit[lineIndex].Split(' ')[1];
                    lineIndex++;
                    List<string> contents =
                        Preprocessor.QuestionBlock(codeSplit, ref lineIndex, true, false,
                            s => s.Substring(s.IndexOf(':') + 1));
                    prep.Macros.Add(name, contents);
                    lineIndexR.Value = lineIndex;
                }
            );

            Directives.Register(
                "", "Call",
                static (prep, y1CodeSplit, result, ii, state) =>
                {
                    int i = ii.Value;
                    string trimmed = y1CodeSplit[i].Trim();
                    if (prep.Macros.ContainsKey(trimmed.Split(' ')[1]))
                    {
                        List<string> contents = prep.Macros[trimmed.Split(' ')[1]];
                        for (int j = 1; j < trimmed.Split("!!").Length; j++)
                        {
                            List<string> unreplaced = contents;
                            contents = new List<string>();
                            foreach (var k in unreplaced)
                            {
                                if (k != "")
                                {
                                    contents.Add(
                                        k.Replace(
                                            "?" + j + "?",
                                            Utils.GraveUnescape(trimmed.Split("!!")[j])
                                        ).Replace("?!", "?"));
                                }
                            }
                        }
                        result.AddRange(contents);
                    }
                }
            );

            Directives.Register(
                "", "DefineShort",
                static (prep, y1CodeSplit, result, ii, state) =>
                {
                    int i = ii.Value;
                    string trimmed = y1CodeSplit[i].Trim();
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
                        Regex macroRegex = new Regex($@"\[\[\s*{name}(\s+(!!.*)+)?\s*]]");
                        Match macroMatch = macroRegex.Match(y1CodeSplit[i]);
                        if (macroMatch.Success)
                        {
                            Group paramGroup = macroMatch.Groups[2];
                            string[] paramGroupValues = paramGroup.Value.Split("!!");
                            for (int j = 1; j < paramGroupValues.Length; j++)
                            {
                                newContents = newContents.Replace($"?{j}?", Utils.GraveUnescape(paramGroupValues[j]));
                            }
                            result.Add(macroRegex.Replace(y1CodeSplit[i], newContents));
                        }
                        else
                        {
                            result.Add(y1CodeSplit[i]);
                        }
                        i++;
                    }
                    ii.Value = i;
                }
            );

            Directives.Register(
                "", "Rewrite",
                static (prep, y1CodeSplit, result, ii, state) =>
                {
                    int i = ii.Value;
                    List<string> sourceFiles = Program.sourceFiles, sourceTexts = Program.sourceTexts;
                    string assemblyName = Program.AssemblyName;
                    string trimmed = y1CodeSplit[i].Trim();
                    string outputStreamName = trimmed.Split(' ')[1];
                    i++;
                    List<string> rewriter = Preprocessor.QuestionBlock(y1CodeSplit, ref i, false);
                    i++;
                    List<string> toRewrite = Preprocessor.QuestionBlock(y1CodeSplit, ref i, false);
                    string name = $"Y1__Rewrite__{Program.rand.NextInt64()}";
                    using (var sw = new StreamWriter(name + ".y1"))
                    {
                        foreach (var j in rewriter)
                        {
                            sw.WriteLine(j);
                        }
                    }
                    Program.Main(new string[] { "/Source=" + name + ".y1", "/Name=" + name });

                    ProcessStartInfo info = new ProcessStartInfo()
                    {
                        ArgumentList =
                        {
                            $"{name}/{name}.dll"
                        }
                    };
                    info.FileName = "dotnet";
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
                    }
                    standardIn?.Dispose();
                    process?.WaitForExit();
                    List<string> rewritten =
                        new List<string>(standardOut?.ReadToEnd()?.Replace("\r\n","\n").Split(
                            new char[] { '\n', '\r', '\v', '\f' }
                        ) ?? new string[] { }
                    );
                    result.AddRange(rewritten ?? new List<string>());
                    File.Delete(name + ".y1");
                    File.Delete($"{name}/{name}.dll");
                    File.Delete($"{name}/{name}.runtimeconfig.json");
                    Directory.Delete(Utils.ToIdentifier(name), true);
                    Program.sourceFiles = sourceFiles;
                    Program.sourceTexts = sourceTexts;
                    Program.AssemblyName = assemblyName;
                    ii.Value = i;
                }
            );

            Directives.Register(
                "", "Undefine",
                static (prep, y1CodeSplit, result, ii, state) =>
                {
                    string trimmed = y1CodeSplit[ii.Value].Trim();
                    prep.Macros.Remove(trimmed.Split(' ')[1]);
                }
            );

            Directives.Register(
                "", "IfDefined",
                static (prep, y1CodeSplit, result, ii, state) =>
                {
                    int i = ii.Value;
                    string trimmed = y1CodeSplit[i].Trim();
                    i++;
                    List<string> ifContents = Preprocessor.QuestionBlock(y1CodeSplit, ref i, false);
                    i++;
                    List<string> elseContents = Preprocessor.QuestionBlock(y1CodeSplit, ref i, false);
                    if (prep.Macros.ContainsKey(trimmed.Split(' ')[1]))
                        result.AddRange(ifContents);
                    else
                        result.AddRange(elseContents);
                    ii.Value = i;
                }
            );

            Directives.Register(
                "", "Defer",
                static (prep, y1CodeSplit, result, i, state) =>
                {
                    string trimmed = y1CodeSplit[i.Value].Trim();
                    result.Add(trimmed.Substring(7));
                }
            );

            Directives.Register(
                "", "WriteToFile",
                static (prep, y1CodeSplit, result, i, state) =>
                {
                    string trimmed = y1CodeSplit[i.Value].Trim();
                    using (var sw = new StreamWriter(trimmed.Split(' ')[1]))
                    {
                        foreach (var j in prep.Macros[trimmed.Split(' ')[2]])
                        {
                            sw.WriteLine(j.Replace("?!", "?"));
                        }
                        sw.Flush();
                    }
                }
            );

            Directives.Register(
                "", "PreprocessorEnclose",
                static (prep, y1CodeSplit, result, ii, state) =>
                {
                    int i = ii.Value;
                    string trimmed = y1CodeSplit[i].Trim();
                    i++;
                    List<string> contents = Preprocessor.QuestionBlock(y1CodeSplit, ref i, false);
                    Preprocessor callee = new Preprocessor();
                    List<string> innerResult = callee.Preprocess(contents);
                    result.AddRange(innerResult);
                    ii.Value = i;
                }
            );

            Directives.Register(
                "", "CondenseLines",
                static (prep, y1CodeSplit, result, i, state) =>
                {
                    string trimmed = y1CodeSplit[i.Value].Trim();
                    string[] contents = trimmed.Split("!!");
                    for (int j = 1; j < contents.Length; j++)
                    {
                        result.Add(
                          Utils.GraveUnescape(contents[j])
                        );
                    }
                }
            );

            Directives.Register(
                "", "User_Diagnostic",
                static (prep, y1CodeSplit, result, i, state) =>
                {
                    string trimmed = y1CodeSplit[i.Value].Trim();

                    string contents = trimmed.Split(' ')[1];
                    Console.Write(Utils.GraveUnescape(contents));
                }
            );


            Directives.Register(
                "", "User_Read",
                static (prep, y1CodeSplit, result, i, state) =>
                {
                    prep.customState["input"] = (string)prep.customState["input"] + Console.Read();
                }
            );

            Directives.Register(
                "", "User_IfChar",
                static (prep, y1CodeSplit, result, ii, state) =>
                {
                    int i = ii.Value;
                    string trimmed = y1CodeSplit[i].Trim();

                    char inputChar = ((string)prep.customState["input"])[0];
                    char test = Utils.GraveUnescape(trimmed.Split(' ')[1])[0];
                    i++;
                    List<string> ifContents = Preprocessor.QuestionBlock(y1CodeSplit, ref i, false);
                    i++;
                    List<string> elseContents = Preprocessor.QuestionBlock(y1CodeSplit, ref i, false);
                    if (inputChar == test)
                        result.AddRange(ifContents);
                    else
                        result.AddRange(elseContents);
                    ii.Value = i;
                }
            );

            Directives.Register(
                "", "User_DequeueInput",
                static (prep, y1CodeSplit, result, ii, state) =>
                {
                    try
                    {
                        prep.customState["input"] = ((string)prep.customState["input"]).Substring(1);
                    }
                    catch { }
                }
            );

            Directives.Register(
                "", "RunPre2Processor",
                static (prep, y1CodeSplit, result, ii, state) =>
                {
                    int i = ii.Value;
                    string[] trimmed = y1CodeSplit[i].Trim().Split(' ');
                    bool shouldTrim = trimmed[1][0] == '1';
                    bool shouldEscape = trimmed[1][1] == '1';
                    i++;
                    List<string> block = Preprocessor.QuestionBlock(y1CodeSplit, ref i, shouldTrim, shouldEscape);
                    string toPPP = block.Aggregate((s1, s2) => s1 + "\n" + s2);
                    result.AddRange(prep.Prepreprocess(toPPP).Split('\n', '\r', '\f', '\v'));
                    ii.Value = i;
                }
            );

            Directives.Register(
                "", "DeferN",
                static (prep, y1CodeSplit, result, ii, state) =>
                {
                    string trimmed = y1CodeSplit[ii.Value].Trim();
                    string numberStr = trimmed.Substring(8);
                    numberStr = numberStr.Substring(0, numberStr.IndexOf(' '));
                    int number;
                    try { number = int.Parse(numberStr); } catch { number = 1; }
                    string toDefer = trimmed.Substring(8 + numberStr.Length + 1);
                    number--;
                    if (number == 0)
                        result.Add(toDefer);
                    else
                        result.Add($"?DeferN {number} {toDefer}");
                    //Console.WriteLine($"Deferred '{toDefer}' {number} times.");
                }
            );

            Directives.Register(
                "", "PrintPPResults",
                 static (prep, y1CodeSplit, result, ii, state) =>
                 {
                     result.ForEach(x => Console.WriteLine(x));
                 }
            );

#if Y1_NonexistentSymbol
            // Template for copy-paste
        Directives.Register(
            "", "Call",
            static (prep, y1CodeSplit, result, i, state) =>
            {
                string trimmed = y1CodeSplit[i].Trim();
            }
        );
#endif
                 }

        public static List<string> QuestionBlock(
            List<string> codeSplit,
            ref int index,
            bool trim = true,
            bool escape = true,
            Func<string, string>? each = null)
        {
            List<string> result = new List<string>();
            while (codeSplit[index].Trim() != "?")
            {
                string line = codeSplit[index];
                if (trim) line = line.Trim();
                if (escape) line = line.Replace("?!", "?");
                if (each != null) line = each(line);
                result.Add(line);
                index++;
            }
            return result;
        }

        public List<string> Preprocess(List<string> y1CodeSplit)
        {
            List<string> result = new List<string>();
        TryAgain:
            for (int i = 0; i < y1CodeSplit.Count; i++)
            {
                string trimmed = y1CodeSplit[i].Trim();
                if (trimmed.StartsWith("?"))
                {
                    int spacePos = trimmed.IndexOf(' ');
                    string qualified = spacePos < 1 ? trimmed.Substring(1) : trimmed.Substring(1, spacePos - 1);
                    ValueTuple<string,string> ID = Directives.GetIDFromLocalizedQualified(
                        Compiler.CompilerSettings.LanguageCode,
                        Compiler.CompilerSettings.NamespaceSeparator,
                        qualified
                    );
                    FauxRefParameter<int> ii = new FauxRefParameter<int>(i);
                    Directives
                         .GetEntry(ID.Item1, ID.Item2)
                        ?.Invoke(this, y1CodeSplit, result, ii, Directives.GetState(ID.Item1, ID.Item2) ?? false);
                    i = ii.Value;
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
            result = QuestionMarkBracketSubstitution(result);
            if (DoLogging)
            {
                Console.WriteLine("[[PREPROCESSOR OUTPUT]]");
                foreach (var i in result)
                    Console.WriteLine(i);
                Console.WriteLine("[[END OUTPUT]]");
            }
            return result;
        }

        public List<string> QuestionMarkBracketSubstitution(List<string> input)
        {
            List<string> result = new List<string>();
            for (int i = 0; i < input.Count; i++)
            {
                if (input[i].Trim() == "[<?>]")
                {
                    i++;
                    String line = input[i].Trim();
                    switch (line[0])
                    {
                        case 'Q':
                            result.Add("?" + line.Substring(1));
                            break;
                        case 'W':
                            try
                            {
                                int[] space = line.Substring(1).Split(',').Select(x => int.Parse(x)).Take(2).ToArray();
                                i++;
                                result.Add(
                                    new String(Enumerable.Repeat(' ', space[0]).ToArray()) +
                                    input[i].Trim() +
                                    new String(Enumerable.Repeat(' ', space[0]).ToArray()));
                            }
                            catch
                            {
                                throw new PreprocessorException("Invalid whitespace specifier: " + line);
                            }
                            break;
                        case 'G':
                            String unescaped = Utils.GraveUnescape(line.Substring(1));
                            result.Add(unescaped);
                            break;
                    }
                }
                else
                {
                    result.Add(input[i]);
                }
            }
            return result;
        }
        #endregion

        #region Prepre(2)processing
        public string Prepreprocess(string unpp)
        {
            if (unpp.StartsWith("^^$")) unpp = unpp.Substring(3).Replace("###Yen;", "¥");
            string result = "";
            Regex yenRegex = new Regex("¥.[^\\]]*]");
            MatchCollection yenMatches = yenRegex.Matches(unpp);
            string[] withoutYen = yenRegex.Split(unpp);
            for (int i = 0; i < withoutYen.Length; i++)
            {
                result += withoutYen[i];
                if (i < yenMatches.Count) result = HandleYen(yenMatches[i].Value, result, i, withoutYen, yenMatches);
            }
            return result;
        }

        // There's some extra parameters in here in case I make this extendable later
        public string HandleYen(string match, string soFar, int index, string[] withoutYen, MatchCollection yenMatches)
        {
            string result = soFar;
            string matchContents = match;
            char yenType = matchContents[1];
            matchContents = matchContents.Substring(2, matchContents.Length - 3);
            switch (yenType)
            {
                case '\\':
                    char value;
                    try { value = (char)int.Parse(matchContents); } catch { value = '0'; }
                    result += value;
                    break;
                case '$':
                    string contents = matchContents;
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
                            .Select(x => Utils.GraveUnescape(x))
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
                        string[] commandParts = command.Split(' ').Select(x => Utils.GraveUnescape(x)).ToArray();
                        ProcessStartInfo psi = new ProcessStartInfo();
                        psi.RedirectStandardOutput = true;
                        psi.FileName = Utils.GraveUnescape(commandParts[0]);
                        psi.Arguments = Utils.GraveUnescape(commandParts[1]);
                        Process process = new Process();
                        process.StartInfo = psi;
                        process.Start();
                        StreamReader reader = process.StandardOutput;
                        string output = reader.ReadToEnd();
                        process.WaitForExit();
                        result += output;
                    }
                    break;
                case 'P':
                    if (matchContents[0] == '1')
                    {
                        matchContents = matchContents.Substring(1);
                        List<string> lines = matchContents.Split('!')
                            .Select(s => s.Replace("\r\n", "\n"))
                            .Select(s => Utils.GraveUnescape(s))
                            .ToList();
                        string ppresult = this.Preprocess(lines)
                            .Aggregate((s1, s2) => s1 + "\n" + s2);
                        result += ppresult;
                    }
                    else if (matchContents[0] == '2')
                    {
                        matchContents = Utils.GraveUnescape(matchContents.Substring(1));
                        result += this.Prepreprocess(matchContents);
                    }
                    break;

            }
            return result;
        }
        #endregion
    }
}
