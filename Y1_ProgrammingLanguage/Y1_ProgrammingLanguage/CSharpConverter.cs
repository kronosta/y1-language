using System;
using System.Collections.Generic;

namespace Kronosta.Language.Y1
{
    public class CSharpConverter
    {
        public CSharpConverter() { }

        public Compiler? Compiler;

        public List<string> assemblyRefs = new List<string>(), assemblyPaths = new List<string>(),
            standardY1Libs = new List<string>();
        public string framework = "net6.0", platform = "anycpu", sdk = "Microsoft.NET.Sdk";
        public Random rand = new Random((int)DateTime.Now.ToBinary());

        public string ConvertToCSharp(List<string> y1CodeSplit) =>
            ConvertToCSharp(y1CodeSplit, 0, true);

        public string ConvertToCSharp(
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
object y1__func_{depth};
object y1__result_{depth};
#pragma warning restore CS0168
";
                return csCode;
            };
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
                try {
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
                            csCode += "public class " + Utils.ToIdentifier(trimmed.Substring(2)) + "\n";
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
                                "\"" + Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                                "System.Reflection.TypeAttributes.Public" +
                                "), y1__arg.Item4));\n";
                            csCode += "}\n";
                        }
                        else if (trimmed.Split(' ')[0] == "DefineMethod")
                        {
                            csCode += "System.Reflection.Emit.MethodBuilder " + Utils.ToIdentifier(trimmed.Split(' ')[2]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item3.DefineMethod(" +
                                "\"" + Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                                "System.Reflection.MethodAttributes.Public, " +
                                "typeof(void), " +
                                "System.Type.EmptyTypes" +
                                ");\n";
                            csCode += "y1__il_" + depth + " = " + Utils.ToIdentifier(trimmed.Split(' ')[2]) + ".GetILGenerator();\n";
                            csCode += "{\n";
                            mode = "methodbuild";
                            modeArg = Utils.ToIdentifier(trimmed.Split(' ')[2]);
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
                                "\"" + Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                                "System.Activator.CreateInstance(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item1, null" +
                                "));\n";
                        }
                        else if (trimmed.Split(' ')[0] == "CallMethod")
                        {
                            csCode += "y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item1.GetMethod(\"" +
                                trimmed.Split(' ')[1] + "\").Invoke(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item2[\"" +
                                Utils.ToIdentifier(trimmed.Split(' ')[2]) + "\"], null);\n";
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
                            csCode += "System.Reflection.Emit.FieldBuilder " + Utils.ToIdentifier(trimmed.Split(' ')[1]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item3.DefineField(\"" +
                                Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
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
                            csCode += "System.Reflection.Emit.MethodBuilder " + Utils.ToIdentifier(trimmed.Split(' ')[2]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item3.DefineMethod(" +
                               "\"" + Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                               "System.Reflection.MethodAttributes.Public, " +
                               "typeof(void), " +
                               paramTypes +
                               ");\n";
                            csCode += "y1__il_" + depth + " = " + Utils.ToIdentifier(trimmed.Split(' ')[2]) + ".GetILGenerator();\n";
                            csCode += "{\n";
                            mode = "methodbuild";
                            modeArg = Utils.ToIdentifier(trimmed.Split(' ')[2]);
                        }
                        else if (trimmed.Split(' ')[0] == "LoadField")
                        {
                            csCode += "System.Reflection.FieldInfo " + Utils.ToIdentifier(trimmed.Split(' ')[2]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item1.GetField(\"" +
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
                                mode = "methodbuild";
                                modeArg = Utils.ToIdentifier(trimmed.Split(' ')[2]);
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
                            csCode += "System.Reflection.Emit.FieldBuilder " + Utils.ToIdentifier(trimmed.Split(' ')[1]) + " = y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item3.DefineField(\"" +
                                Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
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
                                mode = "methodbuild";
                                modeArg = Utils.ToIdentifier(trimmed.Split(' ')[3]);
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
                            csCode += "System.Reflection.Emit.FieldBuilder " + Utils.ToIdentifier(trimmed.Split(' ')[2]) + " = ((System.Reflection.Emit.TypeBuilder)(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item4[\"" +
                                trimmed.Split(' ')[1] + "\"])).DefineField(\"" +
                                Utils.ToIdentifier(trimmed.Split(' ')[2]) + "\", " +
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
                                Utils.ToIdentifier(trimmed.Split(' ')[1]) + "\", " +
                                "System.Activator.CreateInstance(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item4[\"" + trimmed.Split(' ')[2] + "\"], null" +
                                "));\n";
                        }
                        else if (trimmed.Split(' ')[0] == "CallSubclassMethod")
                        {
                            csCode += "y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item4[\"" + trimmed.Split(' ')[3] + "\"].GetMethod(\"" +
                                trimmed.Split(' ')[1] + "\").Invoke(y1__stack_" + depth + "[y1__stack_" + depth + ".Count - 1].Item2[\"" +
                                Utils.ToIdentifier(trimmed.Split(' ')[2]) + "\"], null);\n";
                        }
                        else if (trimmed.Split(' ')[0] == "Summation")
                        {
                            int start = Int32.Parse(trimmed.Split(' ')[1]);
                            int end = Int32.Parse(trimmed.Split(' ')[2]);
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
                        }
                        else if (trimmed.Split(' ')[0] == "DefineVariable")
                        {
                            csCode += trimmed.Split(' ')[2] + " " + trimmed.Split(' ')[1] + " = ";
                            for (int j = 3; j < trimmed.Split(' ').Length; j++)
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
                            List<Tuple<string, List<string>>> lines = new List<Tuple<string, List<string>>>
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
                            Console.WriteLine($"Syntax error: Line {i}. Contents: {trimmed}");
                        }
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    Console.WriteLine($"Syntax error (specifically with argument number): Line {i}. Contents: {trimmed}");
                }
            }
            if (Compiler.CompilerSettings.PrintOutCSharp)
            {
                Console.WriteLine("============================================================================");
                Console.WriteLine(csCode);
                Console.WriteLine("============================================================================");
            }
            return csCode;
        }
    }
}
