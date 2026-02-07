using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Kronosta.Language.Y1
{
    public class CSharpConverter
    {
        public class CSharpConversionException : Exception {
            public CSharpConversionException()
            {
            }

            public CSharpConversionException(string message)
                : base(message)
            {
            }

            public CSharpConversionException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }

        #region Fields

        public delegate string Command(
            CSharpConverter converter,
            FauxRefParameter<int> i,
            FauxRefParameter<int> depth,
            FauxRefParameter<string> mode,
            FauxRefParameter<string> modeArg,
            string csCode,
            string trimmed,
            List<string> y1CodeSplit,
            object? state
        );

        public readonly Registry<Command> CommandsRun, CommandsMethodbuild;
        public IDictionary<string, object> CustomState = new Dictionary<string, object>();

        public Func<int, string, string> startScope = (depth, prevCode) =>
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
object y1__func_{depth};
object y1__result_{depth};
#pragma warning restore CS0168
";
                return csCode;
            };

        public CSharpConverter(Compiler compiler) {
            Compiler = compiler;
            CommandsRun = new Registry<Command>();
            CommandsMethodbuild = new Registry<Command>();
            RegisterDefaultCommandsRunMode(CommandsRun);
            RegisterDefaultCommandsMethodbuildMode(CommandsMethodbuild);
        }

        public Compiler Compiler;

        public List<string> assemblyRefs = new List<string>(), assemblyPaths = new List<string>(),
            standardY1Libs = new List<string>();
        public string framework = "net6.0", platform = "anycpu", sdk = "Microsoft.NET.Sdk";
        public Random rand = new Random((int)DateTime.Now.ToBinary());

        #endregion

        #region Default Commands

        public void RegisterDefaultCommandsCommon(Registry<Command> Commands) {
             Commands.Register(
                "", "DefineVariable",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += trimmed.Split(' ')[2] + " " + trimmed.Split(' ')[1] + " = ";
                    for (int j = 3; j < trimmed.Split(' ').Length; j++)
                    {
                        csCode += trimmed.Split(' ')[j] + " ";
                    }
                    csCode += ";\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "SetVariable",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += trimmed.Split(' ')[1] + " = ";
                    for (int j = 2; j < trimmed.Split(' ').Length; j++)
                    {
                        csCode += trimmed.Split(' ')[j] + " ";
                    }
                    csCode += ";\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "Condition",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    int i = ii.Value;
                    int depth = depth_.Value;
                    csCode += "if (" + trimmed.Substring(trimmed.IndexOf(' ') + 1) + ")\n";
                    csCode += "{\n";
                    List<string> lines = new List<string>();
                    int condDepth = 1;
                    i++;
                    while (condDepth > 0)
                    {
                        lines.Add(y1CodeSplit[i]);
                        if (Commands.GetIDFromLocalizedQualified(
                            Compiler.CompilerSettings.LanguageCode, Compiler.CompilerSettings.NamespaceSeparator,
                            y1CodeSplit[i].Trim().Split(' ')[0]) == new ValueTuple<string, string>("", "Condition")) condDepth++;
                        if (Commands.GetIDFromLocalizedQualified(
                            Compiler.CompilerSettings.LanguageCode, Compiler.CompilerSettings.NamespaceSeparator,
                            y1CodeSplit[i].Trim().Split(' ')[0]) == new ValueTuple<string, string>("", "EndCondition")) condDepth--;
                        i++;
                    }
                    i--;
                    lines.RemoveAt(lines.Count - 1);
                    csCode += ConvertToCSharp(lines, depth);
                    csCode += "}\n";
                    ii.Value = i;
                    return csCode;
                }
            );
            Commands.Register(
                "", "While",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    int i = ii.Value;
                    int depth = depth_.Value;
                    csCode += "while (" + trimmed.Substring(trimmed.IndexOf(' ') + 1) + ")\n";
                    csCode += "{\n";
                    List<string> lines = new List<string>();
                    int condDepth = 1;
                    i++;
                    while (condDepth > 0)
                    {
                        lines.Add(y1CodeSplit[i]);
                        if (Commands.GetIDFromLocalizedQualified(
                            Compiler.CompilerSettings.LanguageCode, Compiler.CompilerSettings.NamespaceSeparator,
                            y1CodeSplit[i].Trim().Split(' ')[0]) == new ValueTuple<string, string>("", "While")) condDepth++;
                        if (Commands.GetIDFromLocalizedQualified(
                            Compiler.CompilerSettings.LanguageCode, Compiler.CompilerSettings.NamespaceSeparator,
                            y1CodeSplit[i].Trim().Split(' ')[0]) == new ValueTuple<string, string>("", "EndWhile")) condDepth--;
                        i++;
                    }
                    i--;
                    lines.RemoveAt(lines.Count - 1);
                    csCode += ConvertToCSharp(lines, depth);
                    csCode += "}\n";
                    ii.Value = i;
                    return csCode;
                }
            );
        }

        public void RegisterDefaultCommandsMethodbuildMode(Registry<Command> Commands) {
            RegisterDefaultCommandsCommon(Commands);
            Commands.Register(
                "", "EscapeMethodBuildMode",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) =>
                {
                    int i = ii.Value;
                    int depth = depth_.Value;
                    i++;
                    int blockDepth = 1;
                    List<string> lines = new List<string>();
                    while (blockDepth > 0)
                    {
                        lines.Add(y1CodeSplit[i]);
                        if (y1CodeSplit[i].Trim() == "EscapeMethodBuildMode") blockDepth++;
                        if (y1CodeSplit[i].Trim() == "EndEscapeMethodBuildMode") blockDepth--;
                        i++;
                    }
                    i--;
                    lines.RemoveAt(lines.Count - 1);
                    csCode += ConvertToCSharp(lines, depth, false, "run");
                    ii.Value = i;
                    return csCode;
                }
            );
        }

        public void RegisterDefaultCommandsRunMode(Registry<Command> Commands) {
            RegisterDefaultCommandsCommon(Commands);
            Commands.Register(
                "", "PushNew",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    return csCode + "y1__stack_" + depth.Value + ".Add(new System.Tuple<System.Type, " +
                        "System.Collections.Generic.Dictionary<string, object>, " +
                        "System.Reflection.Emit.TypeBuilder, " +
                        "System.Collections.Generic.Dictionary<string, System.Type>>(null, null, null, null));\n";
                }
            );
            Commands.Register(
                "", "DefineType",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "{\n";
                    csCode += "var y1__arg = y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1];\n";
                    csCode += "y1__stack_" + depth.Value + ".RemoveAt(y1__stack_" + depth.Value + ".Count - 1);\n";
                    csCode += "y1__stack_" + depth.Value + @".Add(new System.Tuple<
System.Type, 
System.Collections.Generic.Dictionary<string, object>, 
System.Reflection.Emit.TypeBuilder, 
System.Collections.Generic.Dictionary<string, System.Type>
>(y1__arg.Item1, y1__arg.Item2, y1__mb_" + depth.Value + ".DefineType(" +
                        "\"" + Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                        "System.Reflection.TypeAttributes.Public" +
                        "), y1__arg.Item4));\n";
                    csCode += "}\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "DefineMethod",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "System.Reflection.Emit.MethodBuilder " + Utils.ToIdentifier(trimmed.Split(' ')[2]) +
                        " = y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item3.DefineMethod(" +
                        "\"" + Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                        "System.Reflection.MethodAttributes.Public, " +
                        "typeof(void), " +
                        "System.Type.EmptyTypes" +
                        ");\n";
                    csCode += "y1__il_" + depth.Value + " = " + Utils.ToIdentifier(trimmed.Split(' ')[2]) + ".GetILGenerator();\n";
                    csCode += "{\n";
                    mode.Value = "methodbuild";
                    modeArg.Value = Utils.ToIdentifier(trimmed.Split(' ')[2]);
                    return csCode;
                }
            );
            Commands.Register(
                "", "FinishType",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "{\n";
                    csCode += "var y1__arg = y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1];\n";
                    csCode += "y1__stack_" + depth.Value + ".RemoveAt(y1__stack_" + depth.Value + ".Count - 1);\n";
                    csCode += "y1__stack_" + depth.Value + ".Add(" +
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
                    return csCode;
                }
            );
            Commands.Register(
                "", "CreateObject",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item2.Add(" +
                        "\"" + Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                        "System.Activator.CreateInstance(y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item1, null" +
                        "));\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "CallMethod",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item1.GetMethod(\"" +
                        trimmed.Split(' ')[1] + "\").Invoke(y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item2[\"" +
                        Utils.ToIdentifier(trimmed.Split(' ')[2]) + "\"], null);\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "Roll",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "y1__stack_" + depth.Value + ".Add(y1__stack_" + depth.Value +
                        "[y1__stack_" + depth.Value + ".Count - " + trimmed.Split(' ')[1] + "]);\n";
                    csCode += "y1__stack_" + depth.Value + ".RemoveAt(y1__stack_" + depth.Value +
                        ".Count - (" + trimmed.Split(' ')[1] + " + 1));\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "ReverseRoll",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "y1__stack_" + depth.Value + ".Insert(y1__stack_" + depth.Value +
                        ".Count - " + trimmed.Split(' ')[1] + ", y1__stack_" + depth.Value +
                        "[y1__stack_" + depth.Value + ".Count - 1]);\n";
                    csCode += "y1__stack_" + depth.Value + ".RemoveAt(y1__stack_" + depth.Value + ".Count - 1);\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "Drop",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "y1__stack_" + depth.Value + ".RemoveAt(y1__stack_" + depth.Value + ".Count - 1);\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "LoadType",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "y1__stack_" + depth.Value + @".Add(new System.Tuple<
        System.Type, 
        System.Collections.Generic.Dictionary<string, object>, 
        System.Reflection.Emit.TypeBuilder, 
        System.Collections.Generic.Dictionary<string, System.Type>
    >(typeof(" + trimmed.Split(' ')[1] + "), new System.Collections.Generic.Dictionary<string,object>(), " +
                                "null, new System.Collections.Generic.Dictionary<string, Type>()));\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "ObjParams",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    string name = "";
                    string parameters = "";
                    i.Value++;
                    name = y1CodeSplit[i.Value].Trim();
                    i.Value++;
                    parameters = y1CodeSplit[i.Value].Trim();
                    while (parameters.EndsWith(",,"))
                    {
                        parameters = parameters.Substring(0, parameters.Length - 2);
                        i.Value++;
                        while (y1CodeSplit[i.Value].Trim() == "")
                        {
                            i.Value++;
                        }
                        parameters += y1CodeSplit[i.Value].Trim();
                    }
                    csCode += "y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item2.Add(" +
                        "\"" + name + "\", " +
                        "System.Activator.CreateInstance(y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item1, " + parameters +
                        "));\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "MethodParams",
                (converter, ii, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    int i = ii.Value;
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
                    csCode += "y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item1.GetMethod(\"" +
                        methodName + "\", " + types + ").Invoke(y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item2[\"" +
                        name + "\"], " + parameters + ");\n";
                    ii.Value = i;
                    return csCode;
                }
            );
            Commands.Register(
                "", "DefineField",
                (converter, ii, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "System.Reflection.Emit.FieldBuilder " + Utils.ToIdentifier(trimmed.Split(' ')[1]) +
                        " = y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item3.DefineField(\"" +
                    Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                    trimmed.Split(' ')[2] + ", System.Reflection.FieldAttributes.Public);\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "DefineParamMethod",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    int i = ii.Value;
                    int depth = depth_.Value;
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
                    csCode += "System.Reflection.Emit.MethodBuilder " + Utils.ToIdentifier(trimmed.Split(' ')[2]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item3.DefineMethod(" +
                    "\"" + Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                    "System.Reflection.MethodAttributes.Public, " +
                    "typeof(void), " +
                    paramTypes +
                    ");\n";
                    csCode += "y1__il_" + depth + " = " + Utils.ToIdentifier(trimmed.Split(' ')[2]) + ".GetILGenerator();\n";
                    csCode += "{\n";
                    ii.Value = i;
                    mode.Value = "methodbuild";
                    modeArg.Value = Utils.ToIdentifier(trimmed.Split(' ')[2]);
                    return csCode;
                }
            );
            Commands.Register(
                "", "LoadField",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "System.Reflection.FieldInfo " + Utils.ToIdentifier(trimmed.Split(' ')[2]) + " = y1__stack_" + depth.Value +
                        "[y1__stack_" + depth.Value + ".Count - 1].Item1.GetField(\"" + trimmed.Split(' ')[1] + "\");\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "DefineComplexMethod",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    int i = ii.Value;
                    int depth = depth_.Value;
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
                    csCode += "System.Reflection.Emit.MethodBuilder " + Utils.ToIdentifier(trimmed.Split(' ')[2]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item3.DefineMethod(" +
                        "\"" + Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                        methodAttributes + ", " +
                        returnType + ", " +
                        types +
                        ");\n";
                    if (!y1CodeSplit[i].Contains("abstract"))
                    {
                        csCode += "y1__il_" + depth + " = " + Utils.ToIdentifier(trimmed.Split(' ')[2]) + ".GetILGenerator();\n";
                        csCode += "{\n";
                        mode.Value = "methodbuild";
                        modeArg.Value = Utils.ToIdentifier(trimmed.Split(' ')[2]);
                    }
                    ii.Value = i;
                    return csCode;
                }
            );
            Commands.Register(
                "", "DefineComplexField",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    int i = ii.Value;
                    int depth = depth_.Value;
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
                    csCode += "System.Reflection.Emit.FieldBuilder " + Utils.ToIdentifier(trimmed.Split(' ')[1]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item3.DefineField(\"" +
                        Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                        trimmed.Split(' ')[2] + ", " + fieldAttributes + ");\n";
                    ii.Value = i;
                    return csCode;
                }
            );
            Commands.Register(
                "", "Subclass",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item4.Add(\"" +
                        trimmed.Split(' ')[2] + "\", y1__mb_" + depth.Value + ".DefineType(\"" + trimmed.Split(' ')[1] +
                        "\", System.Reflection.TypeAttributes.Public, y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item1));\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "DefineSubclassMethod",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    int i = ii.Value;
                    int depth = depth_.Value;
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
                    csCode += "System.Reflection.Emit.MethodBuilder " + Utils.ToIdentifier(trimmed.Split(' ')[3]) + " = ((System.Reflection.Emit.TypeBuilder)(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1]" +
                        ".Item4[\"" + trimmed.Split(' ')[1] + "\"])).DefineMethod(" +
                        "\"" + Utils.ToIdentifier(trimmed.Split(' ')[2]) + "\", " +
                        methodAttributes + ", " +
                        returnType + ", " +
                        types +
                        ");\n";
                    if (!y1CodeSplit[i].Contains("abstract"))
                    {
                        csCode += "y1__il_" + depth + " = " + Utils.ToIdentifier(trimmed.Split(' ')[3]) + ".GetILGenerator();\n";
                        csCode += "{\n";
                        mode.Value = "methodbuild";
                        modeArg.Value = Utils.ToIdentifier(trimmed.Split(' ')[3]);
                    }
                    ii.Value = i;
                    return csCode;
                }
            );
            Commands.Register(
                "", "DefineSubclassField",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    int i = ii.Value;
                    int depth = depth_.Value;
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
                    csCode += "System.Reflection.Emit.FieldBuilder " + Utils.ToIdentifier(trimmed.Split(' ')[2]) + " = ((System.Reflection.Emit.TypeBuilder)(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item4[\"" +
                        trimmed.Split(' ')[1] + "\"])).DefineField(\"" +
                        Utils.ToIdentifier(trimmed.Split(' ')[2]) + "\", " +
                        trimmed.Split(' ')[3] + ", " + fieldAttributes + ");\n";
                    ii.Value = i;
                    return csCode;
                }
            );
            Commands.Register(
                "", "FinishSubclass",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item4[\"" + trimmed.Split(' ')[1] +
                        "\"] = ((System.Reflection.Emit.TypeBuilder)(y1__stack_" + depth.Value + "[y1__stack_" + depth.Value +
                        ".Count - 1].Item4[\"" + trimmed.Split(' ')[1] + "\"])).CreateType();\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "CreateSubclassObject",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item2.Add(\"" +
                        Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                        "System.Activator.CreateInstance(y1__stack_" + depth.Value + "[y1__stack_" + depth.Value +
                        ".Count - 1].Item4[\"" + trimmed.Split(' ')[2] + "\"], null" + "));\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "CallSubclassMethod",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    csCode += "y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item4[\"" + trimmed.Split(' ')[3] + "\"].GetMethod(\"" +
                        trimmed.Split(' ')[1] + "\").Invoke(y1__stack_" + depth.Value + "[y1__stack_" + depth.Value + ".Count - 1].Item2[\"" +
                        Utils.ToIdentifier(trimmed.Split(' ')[2]) + "\"], null);\n";
                    return csCode;
                }
            );
            Commands.Register(
                "", "Summation",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    int i = ii.Value;
                    int depth = depth_.Value;
                    int start = int.Parse(trimmed.Split(' ')[1]);
                    int end = int.Parse(trimmed.Split(' ')[2]);
                    string type = trimmed.Split(' ')[4];
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
                    csCode += "y1__func_" + depth + " = (System.Func<int, " + type + ">)((" + paramName + ") => {\n";
                    csCode = startScope(depth + 1, csCode);
                    csCode += seqCode;
                    csCode += "});\n";
                    csCode += "y1__result_" + depth + " = ((System.Func < int, " + type + ">)y1__func_" + depth + ")(" + start + ");\n";
                    csCode += "for (int y1__counter_" + depth + " = " + (start + 1) + "; y1__counter_" + depth + " < " + end + "; y1__counter_" + depth + "++)\n";
                    csCode += "{\n";
                    csCode += "y1__result_" + depth + " = ((" + type + ")y1__result_" + depth + ") + ((System.Func < int, " + type + " >)y1__func_" + depth + ")(y1__counter_" + depth + ");\n";
                    csCode += "}\n";
                    i--;
                    ii.Value = i;
                    return csCode;
                }
            );
           
            Commands.Register(
                "", "DoMulti",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    int i = ii.Value;
                    int depth = depth_.Value;
                    csCode += "System.Threading.Tasks.Task.WaitAll(new System.Threading.Tasks.Task[] {\n";
                    List<string> lines = new List<string>();
                    int multiDepth = 1;
                    i++;
                    while (multiDepth > 0)
                    {
                        lines.Add(y1CodeSplit[i]);
                        if (Commands.GetIDFromLocalizedQualified(
                            Compiler.CompilerSettings.LanguageCode, Compiler.CompilerSettings.NamespaceSeparator,
                            y1CodeSplit[i].Trim().Split(' ')[0]) == new ValueTuple<string, string>("", "DoMulti")) multiDepth++;
                        if (Commands.GetIDFromLocalizedQualified(
                            Compiler.CompilerSettings.LanguageCode, Compiler.CompilerSettings.NamespaceSeparator,
                            y1CodeSplit[i].Trim().Split(' ')[0]) == new ValueTuple<string, string>("", "EndMulti")) multiDepth--;
                        if (multiDepth == 1 && Commands.GetIDFromLocalizedQualified(
                            Compiler.CompilerSettings.LanguageCode, Compiler.CompilerSettings.NamespaceSeparator,
                            y1CodeSplit[i].Trim().Split(' ')[0]) == new ValueTuple<string, string>("", "AndMulti"))
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
                    ii.Value = i;
                    return csCode;
                }
            );
            Commands.Register(
                "", "DefineComplexType",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    int i = ii.Value;
                    int depth = depth_.Value;
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
                    ii.Value = i;
                    return csCode;
                }
            );
            Commands.Register(
                "", "ListenForKeys",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    int i = ii.Value;
                    int depth = depth_.Value;
                    int lfkDepth = 1;
                    List<Tuple<string, List<string>>> lines = new List<Tuple<string, List<string>>>{
                        { new Tuple<string, List<string>>("", new List<string>()) }
                    };
                    i++;
                    while (lfkDepth > 0)
                    {
                        lines[lines.Count - 1].Item2.Add(y1CodeSplit[i].Trim());
                        if (Commands.GetIDFromLocalizedQualified(
                            Compiler.CompilerSettings.LanguageCode, Compiler.CompilerSettings.NamespaceSeparator,
                            y1CodeSplit[i].Trim().Split(' ')[0]) == new ValueTuple<string, string>("", "ListenForKeys")) lfkDepth++;
                        if (Commands.GetIDFromLocalizedQualified(
                            Compiler.CompilerSettings.LanguageCode, Compiler.CompilerSettings.NamespaceSeparator,
                            y1CodeSplit[i].Trim().Split(' ')[0]) == new ValueTuple<string, string>("", "EndListenForKeys")) lfkDepth--;
                        if (lfkDepth == 1 && Commands.GetIDFromLocalizedQualified(
                            Compiler.CompilerSettings.LanguageCode, Compiler.CompilerSettings.NamespaceSeparator,
                            y1CodeSplit[i].Trim().Split(' ')[0]) == new ValueTuple<string, string>("", "KeyCase"))
                        {
                            lines[lines.Count - 1].Item2.RemoveAt(lines[lines.Count - 1].Item2.Count - 1);
                            lines.Add(
                                new Tuple<string, List<string>>(y1CodeSplit[i].Trim().Substring(
                                    y1CodeSplit[i].Trim().Split(' ')[0].Length + 1
                                ),
                                new List<string>())
                            );
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
                    ii.Value = i;
                    return csCode;
                }
            );
            Commands.Register(
                "", "Namespace",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => csCode + "namespace " + trimmed.Split(' ')[1] + "{\n"
            );
            Commands.Register(
                "", "Return",
                (converter, ii, depth_, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => csCode + "return " + trimmed.Substring(7) + ";\n"
            );
            Commands.Register(
                "", "DefineBakedField",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    string[] headerParams = trimmed
                                    .Substring(16)
                                    .Split("%%")
                                    .Select(s => s.Trim())
                                    .ToArray();
                    string? modifiers =
                        (headerParams
                        .Where(x => x.StartsWith("^"))
                        .FirstOrDefault() ?? "^public static").Substring(1);
                    string? name = headerParams
                        .Where(x => x.StartsWith("@"))
                        .FirstOrDefault() ?? "field";
                    string? type = (headerParams
                        .Where(x => x.StartsWith("$"))
                        .FirstOrDefault() ?? "$string").Substring(1);
                    string? value = headerParams
                        .Where(x => x.StartsWith("="))
                        .FirstOrDefault();
                    if (value != null) value = " = " + value.Substring(1);
                    if (headerParams.Contains(":public-property"))
                        return csCode + $"{modifiers} {type} {name} {{get; set;}}{value ?? ""};\n";
                    else if (headerParams.Contains(":private-set-property"))
                        return csCode + $"{modifiers} {type} {name} {{get; private set;}}{value ?? ""};\n";
                    else if (headerParams.Contains(":private-get-property"))
                        return csCode + $"{modifiers} {type} {name} {{private get; set;}}{value ?? ""};\n";
                    else if (headerParams.Contains(":read-only-property"))
                        return csCode + $"{modifiers} {type} {name} {{get; init;}}{value ?? ""};\n";
                    else
                        return csCode + $"{modifiers} {type} {name}{value ?? ""};\n";
                }
            );
            Commands.Register(
                "", "DefineBakedProperty",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) => {
                    int ii = i.Value;
                    string[] headerParams = trimmed
                                    .Substring(19)
                                    .Split("%%")
                                    .Select(s => s.Trim())
                                    .ToArray();
                    string? modifiers =
                        (headerParams
                        .Where(x => x.StartsWith("^"))
                        .FirstOrDefault() ?? "^public static").Substring(1);
                    string? name = headerParams
                        .Where(x => x.StartsWith("@"))
                        .FirstOrDefault() ?? "Field";
                    string? type = (headerParams
                        .Where(x => x.StartsWith("$"))
                        .FirstOrDefault() ?? "$string").Substring(1);
                    
                    bool noScopeGet = headerParams.Contains(":no-scope-get");
                    bool noScopeSet = headerParams.Contains(":no-scope-set");
                    List<string> getLines = new List<string>();
                    List<string> setLines = new List<string>();
                    ii++;
                    while (Commands.GetIDFromLocalizedQualified(
                            Compiler.CompilerSettings.LanguageCode, Compiler.CompilerSettings.NamespaceSeparator,
                            y1CodeSplit[ii].Trim().Split(' ')[0]) != new ValueTuple<string, string>("", "EndGet"))
                    {
                        getLines.Add(y1CodeSplit[ii]);
                        ii++;
                    }
                    ii++;
                    while (Commands.GetIDFromLocalizedQualified(
                        Compiler.CompilerSettings.LanguageCode, Compiler.CompilerSettings.NamespaceSeparator,
                        y1CodeSplit[ii].Trim().Split(' ')[0]) != new ValueTuple<string, string>("", "EndSet"))
                    {
                        setLines.Add(y1CodeSplit[ii]);
                        ii++;
                    }
                    csCode += $"{modifiers} {type} {name} {{\nget {{\n";
                    if (!noScopeGet)
                        csCode = startScope(0, csCode);
                    csCode += ConvertToCSharp(getLines);
                    csCode += "}\nset {\n";
                    if (!noScopeSet)
                        csCode = startScope(0, csCode);
                    csCode += ConvertToCSharp(setLines);
                    csCode += "}\n}\n";
                    i.Value = ii;
                    return csCode;
                }
            );
            Commands.Register(
                "", "PrintLine",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) =>
                {
                    return csCode + $"System.Console.WriteLine({trimmed.Substring(9)});";
                }
            );
            Commands.Register(
                "", "Print",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) =>
                {
                    return csCode + $"System.Console.Write({trimmed.Substring(5)});";
                }
            );
            Commands.Register(
                "", "ReadLine",
                (converter, i, depth, mode, modeArg, csCode, trimmed, y1CodeSplit, state) =>
                {
                    return csCode + $"{trimmed.Substring(8)} = System.Console.ReadLine();";
                }
            );
        }

        #endregion

        public string ConvertToCSharp(List<string> y1CodeSplit) =>
            ConvertToCSharp(y1CodeSplit, 0, true);

        public string ConvertToCSharp(
            List<string> y1CodeSplit,
            int depth,
            bool outer = false,
            string mode = "run")
        {
            
            string csCode = "";
            string modeArg = "";
            int startingPos = 0;
            if (outer)
            {
                while (y1CodeSplit[startingPos].Trim() == "") startingPos++;
                if (y1CodeSplit[startingPos].Trim() == "<ATTRIBUTES>")
                {
                    startingPos++;
                    while (y1CodeSplit[startingPos].Trim() != "<END_ATTRIBUTES>")
                    {
                        while (y1CodeSplit[startingPos].Trim() == "") startingPos++;
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
                while (y1CodeSplit[startingPos].Trim() == "") startingPos++;
                if (y1CodeSplit[startingPos].Trim() == "<REFS>")
                {
                    startingPos++;
                    while (y1CodeSplit[startingPos].Trim() != "<END_REFS>")
                    {
                        while (y1CodeSplit[startingPos].Trim() == "") startingPos++;
                        assemblyRefs.Add(y1CodeSplit[startingPos].Trim());
                        startingPos++;
                    }
                    startingPos++;
                }
                while (y1CodeSplit[startingPos].Trim() == "") startingPos++;
                if (y1CodeSplit[startingPos].Trim() == "<IMPORTS>")
                {
                    startingPos++;
                    while (y1CodeSplit[startingPos].Trim() != "<END_IMPORTS>")
                    {
                        while (y1CodeSplit[startingPos].Trim() == "") startingPos++;
                        csCode += "using " + y1CodeSplit[startingPos].Trim() + ";\n";
                        startingPos++;
                    }
                    startingPos++;
                }
                while (y1CodeSplit[startingPos].Trim() == "") startingPos++;
                if (y1CodeSplit[startingPos].Trim() == "<STANDARD_Y1_LIBS>")
                {
                    startingPos++;
                    while (y1CodeSplit[startingPos].Trim() != "<END_STANDARD_Y1_LIBS>")
                    {
                        while (y1CodeSplit[startingPos].Trim() == "") startingPos++;
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
                try
                {
                    if (mode == "run")
                    {
                        if (trimmed.StartsWith("|/"))
                        {
                            if (trimmed.StartsWith("|/+"))
                            {
                                string[] headerParams = trimmed
                                    .Substring(3)
                                    .Split("%%")
                                    .Select(s => s.Trim())
                                    .ToArray();
                                string? modifiersAndReturn =
                                    (headerParams
                                    .Where(x => x.StartsWith("^"))
                                    .FirstOrDefault() ?? "^public static void").Substring(1);
                                string? name = headerParams
                                    .Where(x => x.StartsWith("@"))
                                    .FirstOrDefault() ?? "Main";
                                string? genericParams = headerParams
                                    .Where(x => x.StartsWith("<"))
                                    .FirstOrDefault() ?? "";
                                string? methodParams = headerParams
                                    .Where(x => x.StartsWith("("))
                                    .FirstOrDefault() ?? "()";
                                string? genericConstraints = headerParams
                                    .Where(x => x.StartsWith("where"))
                                    .FirstOrDefault() ?? "";
                                csCode += $"{modifiersAndReturn} {name} {genericParams} {methodParams} {genericConstraints} {{\n";
                                if (!headerParams.Contains(":no-scope")) csCode = startScope(depth, csCode);
                            }
                            else
                            {
                                csCode += "public static void " + trimmed.Substring(2) + "() {\n";
                                csCode = startScope(depth, csCode);
                            }
                        }
                        else if (trimmed == "\\|" || trimmed == "]@" || trimmed == "EndNamespace")
                        {
                            csCode += "}\n";
                        }
                        else if (trimmed.StartsWith("@["))
                        {
                            csCode += "public class " + Utils.ToIdentifier(trimmed.Substring(2)) + "\n";
                            csCode += "{\n";
                        }
                        //multi-line comment starter
                        else if (trimmed.StartsWith("[#"))
                        {
                            mode = "comment";
                        }
                        else if (trimmed.StartsWith("C# - "))
                        {
                            csCode += trimmed.Substring(5);
                            csCode += "\n";
                        }
                        else
                        {
                            string commandName = trimmed.Split(' ')[0];
                            ValueTuple<string, string> registryID = 
                                CommandsRun.GetIDFromLocalizedQualified(
                                    Compiler.CompilerSettings.LanguageCode,
                                    Compiler.CompilerSettings.NamespaceSeparator,
                                    commandName);
                            Command? command = CommandsRun.GetEntry(registryID.Item1, registryID.Item2);
                            FauxRefParameter<int> ii = new FauxRefParameter<int>(i);
                            FauxRefParameter<int> depth_ = new FauxRefParameter<int>(depth);
                            FauxRefParameter<string> mode_ = new FauxRefParameter<string>(mode);
                            FauxRefParameter<string> modeArg_ = new FauxRefParameter<string>(modeArg);
                            string? outputCode = command?.Invoke(
                                this, ii, depth_, mode_, modeArg_, csCode, trimmed, y1CodeSplit, CommandsRun.GetState(registryID.Item1, registryID.Item2)
                            );
                            i = ii.Value;
                            depth = depth_.Value;
                            mode = mode_.Value;
                            modeArg = modeArg_.Value;
                            if (outputCode == null)
                                throw new CSharpConversionException($"Syntax error: Line {i}. Contents: {trimmed}");
                            else
                                csCode = outputCode;
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
                        else
                        {
                            string commandName = trimmed.Split(' ')[0];
                            ValueTuple<string, string> registryID = 
                                CommandsMethodbuild.GetIDFromLocalizedQualified(
                                    Compiler.CompilerSettings.LanguageCode,
                                    Compiler.CompilerSettings.NamespaceSeparator,
                                    commandName);
                            Command? command = CommandsMethodbuild.GetEntry(registryID.Item1, registryID.Item2);
                            FauxRefParameter<int> ii = new FauxRefParameter<int>(i);
                            FauxRefParameter<int> depth_ = new FauxRefParameter<int>(depth);
                            FauxRefParameter<string> mode_ = new FauxRefParameter<string>(mode);
                            FauxRefParameter<string> modeArg_ = new FauxRefParameter<string>(modeArg);
                            string? outputCode = command?.Invoke(
                                this, ii, depth_, mode_, modeArg_, csCode, trimmed, y1CodeSplit, CommandsMethodbuild.GetState(registryID.Item1, registryID.Item2)
                            );
                            if (outputCode == null)
                                throw new CSharpConversionException($"Syntax error: Line {i}. Contents: {trimmed}");
                            else
                                csCode = outputCode;
                        }
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    throw new CSharpConversionException($"Syntax error (specifically with argument number): Line {i}. Contents: {trimmed}");
                }
            }
            if (Compiler?.CompilerSettings?.PrintOutCSharp ?? false)
            {
                Console.WriteLine("============================================================================");
                Console.WriteLine(csCode);
                Console.WriteLine("============================================================================");
            }
            return csCode;
        }
    }
}
