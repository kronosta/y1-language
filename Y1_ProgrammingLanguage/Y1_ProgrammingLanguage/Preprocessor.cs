using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Kronosta.Language.Y1
{
    public class Preprocessor
    {
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

        public bool DoLogging { get; set; } = false;
        public List<string> Preprocess(List<string> y1CodeSplit)
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
                }
                else if (trimmed.Split(' ')[0] == "?Rewrite")
                {
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
                          Utils.GraveUnescape(contents[j])
                        );
                    }
                }
                else if (trimmed.Split(' ')[0] == "?User_Diagnostic")
                {
                    string contents = trimmed.Split(' ')[1];
                    Console.Write(Utils.GraveUnescape(contents));
                }
                else if (trimmed.Split(' ')[0] == "?User_Read")
                {
                    input += Console.Read();
                }
                else if (trimmed.Split(' ')[0] == "?User_IfChar")
                {
                    char inputChar = input[0];
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
                }
                else if (trimmed.Split(' ')[0] == "?User_DequeueInput")
                {
                    try
                    {
                        input = input.Substring(1);
                    }
                    catch { }
                }
                else if (trimmed[0] != '?')
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
    }
}
