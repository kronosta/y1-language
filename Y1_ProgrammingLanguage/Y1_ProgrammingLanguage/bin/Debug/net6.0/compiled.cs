using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Threading.Tasks;

public class Program
{
public static string[] strs = {"@[Program\n","    C# - public static string[] strs = {,,\n","    };\n","    |/Main\n","        DefineVariable Total new String(new char[]{})\n","        C# - Total += strs[0] + strs[1];\n","        Summation 0 30 I\n","            C# - Total += new string(new char[]{,,\n","                ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', (char)34}) +,,\n","                strs[I].Replace(new string(new char[]{(char)10}), new string(new char[]{})) +,,\n","                new string(new char[]{(char)92, 'n', (char)34, ',', ',', ','});\n","            C# - Total += (char)10;\n","            C# - return 0;\n","        EndSummation\n","        Summation 2 30 I\n","            C# - Total += strs[I];\n","            C# - return 0;\n","        EndSummation\n","        C# - Total = Total,,\n","        .Replace(,,\n","            new string(new char[]{']', '@', (char)92, 'n', (char)34, ',', ',', ','}),,,\n","            new string(new char[]{']', '@', (char)92, 'n', (char)34, ',', ','}),,\n","        ),,\n","        .Replace(,,\n","            new string(new char[]{(char)92, '|', (char)92, 'n', (char)34}),,,\n","            new string(new char[]{(char)92, (char)92, '|', (char)92, 'n', (char)34}),,\n","        );\n","        C# - Console.Write(Total);\n","    \\|\n","]@\n"};
public static void Main() {
var y1__stack_0 = new List<Tuple<Type, Dictionary<string, object>, TypeBuilder, Dictionary<string, Type>>>();
AssemblyName y1__aName_0 = new AssemblyName("y1__DynamicAssembly");
AssemblyBuilder y1__ab_0 = AssemblyBuilder.DefineDynamicAssembly(y1__aName_0, AssemblyBuilderAccess.RunAndCollect);
ModuleBuilder y1__mb_0 = y1__ab_0.DefineDynamicModule(y1__aName_0.Name);
ILGenerator y1__il_0;
dynamic y1__func_0;
dynamic y1__result_0;
dynamic Total = new String(new char[]{}) ;
Total += strs[0] + strs[1];
y1__func_0 = (Func<int, dynamic>)((I) => {
var y1__stack_1 = new List<Tuple<Type, Dictionary<string, object>, TypeBuilder, Dictionary<string, Type>>>();
AssemblyName y1__aName_1 = new AssemblyName("y1__DynamicAssembly");
AssemblyBuilder y1__ab_1 = AssemblyBuilder.DefineDynamicAssembly(y1__aName_1, AssemblyBuilderAccess.RunAndCollect);
ModuleBuilder y1__mb_1 = y1__ab_1.DefineDynamicModule(y1__aName_1.Name);
ILGenerator y1__il_1;
dynamic y1__func_1;
dynamic y1__result_1;
Total += new string(new char[]{' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', (char)34}) +strs[I].Replace(new string(new char[]{(char)10}), new string(new char[]{})) +new string(new char[]{(char)92, 'n', (char)34, ',', ',', ','});
Total += (char)10;
return 0;
});
y1__result_0 = y1__func_0(0);
for (int y1__counter_0 = 1; y1__counter_0 < 30; y1__counter_0++)
{
y1__result_0 += y1__func_0(y1__counter_0);
}
y1__func_0 = (Func<int, dynamic>)((I) => {
var y1__stack_1 = new List<Tuple<Type, Dictionary<string, object>, TypeBuilder, Dictionary<string, Type>>>();
AssemblyName y1__aName_1 = new AssemblyName("y1__DynamicAssembly");
AssemblyBuilder y1__ab_1 = AssemblyBuilder.DefineDynamicAssembly(y1__aName_1, AssemblyBuilderAccess.RunAndCollect);
ModuleBuilder y1__mb_1 = y1__ab_1.DefineDynamicModule(y1__aName_1.Name);
ILGenerator y1__il_1;
dynamic y1__func_1;
dynamic y1__result_1;
Total += strs[I];
return 0;
});
y1__result_0 = y1__func_0(2);
for (int y1__counter_0 = 3; y1__counter_0 < 30; y1__counter_0++)
{
y1__result_0 += y1__func_0(y1__counter_0);
}
Total = Total.Replace(new string(new char[]{']', '@', (char)92, 'n', (char)34, ',', ',', ','}),new string(new char[]{']', '@', (char)92, 'n', (char)34, ',', ','})).Replace(new string(new char[]{(char)92, '|', (char)92, 'n', (char)34}),new string(new char[]{(char)92, (char)92, '|', (char)92, 'n', (char)34}));
Console.Write(Total);
}
}
