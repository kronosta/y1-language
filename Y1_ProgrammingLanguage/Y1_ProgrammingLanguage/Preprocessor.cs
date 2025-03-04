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

        public class IntRef
        {
            public int Value;

            public IntRef(int value) { this.Value = value; }
        }

        public delegate void Directive(
                Preprocessor prep,
                List<string> codeSplit,
                List<string> result,
                IntRef lineIndex,
                object state
            );
        #endregion

        #region Pre(1)processing
        public IDictionary<string, List<string>> Macros;
        public CompilerSettings CompilerSettings;
        public readonly Registry<Directive> Directives;
        public bool DoLogging = false;
        public IDictionary<string, object> customState = new Dictionary<string, object>();

        public Preprocessor()
        {
            Macros = new Dictionary<string, List<string>>();
            CompilerSettings = new CompilerSettings();
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
                    string contents;
                    using (StreamReader sr = new StreamReader(trimmed.Split(' ')[1]))
                    {
                        contents = sr.ReadToEnd();
                        contents = contents.Replace("\r\n", "\n");
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
                    List<string> contents = new List<string>();
                    string name = codeSplit[lineIndex].Split(' ')[1];
                    lineIndex++;
                    while (codeSplit[lineIndex].Trim() != "?")
                    {
                        contents.Add(
                          codeSplit[lineIndex].Substring(codeSplit[lineIndex].IndexOf(":") + 1).Trim()
                        );
                        lineIndex++;
                    }
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
                    string trimmed = y1CodeSplit[i].Trim();
                    string? originalFilename = Program.filename;
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
                    string name = $"__Rewrite__{Program.rand.NextInt64()}.y1";
                    using (var sw = new StreamWriter(name))
                    {
                        foreach (var j in rewriter)
                        {
                            sw.WriteLine(j);
                        }
                    }
                    Program.Main(new string[] { name });

                    ProcessStartInfo info = new ProcessStartInfo();
                    info.FileName = Utils.ToIdentifier(name) + "/" + name + ".exe";
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
                        ) ?? new string[] { }
                    );
                    result.AddRange(rewritten ?? new List<string>());
                    Directory.Delete(Utils.ToIdentifier(name), true);
                    try { File.Delete(name); } catch { }
                    try { File.Delete($"{name}.csproj"); } catch { }
                    try { File.Delete($"{name}.~cs"); } catch { }
                    Program.filename = originalFilename;
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
                    List<string> contents = new List<string>();
                    i++;
                    while (y1CodeSplit[i].Trim() != "?")
                    {
                        contents.Add(y1CodeSplit[i].Replace("?!", "?"));
                        i++;
                    }
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
                    ii.Value = i;
                }
            );

            Directives.Register(
                "", "User_DequeueInput",
                static (prep, y1CodeSplit, result, i, state) =>
                {
                    try
                    {
                        prep.customState["input"] = ((string)prep.customState["input"]).Substring(1);
                    }
                    catch { }
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
                        CompilerSettings.LanguageCode,
                        CompilerSettings.NamespaceSeparator,
                        qualified
                    );
                    IntRef ii = new IntRef(i);
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
                if (i < yenMatches.Count) result = HandleYen(yenMatches[i].Value, result);
            }
            return result;
        }

        public string HandleYen(string match, string soFar)
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
            }
            return result;
        }
        #endregion
    }
}
