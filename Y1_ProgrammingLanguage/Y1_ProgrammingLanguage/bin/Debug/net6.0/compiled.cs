using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Threading.Tasks;

public class Program
{
public static void Main() {
var y1__stack_0 = new List<Tuple<Type, Dictionary<string, object>, TypeBuilder, Dictionary<string, Type>>>();
AssemblyName y1__aName_0 = new AssemblyName("y1__DynamicAssembly");
AssemblyBuilder y1__ab_0 = AssemblyBuilder.DefineDynamicAssembly(y1__aName_0, AssemblyBuilderAccess.RunAndCollect);
ModuleBuilder y1__mb_0 = y1__ab_0.DefineDynamicModule(y1__aName_0.Name);
ILGenerator y1__il_0;
dynamic y1__func_0;
dynamic y1__result_0;
Console.WriteLine("Hello, World!");
}
}
