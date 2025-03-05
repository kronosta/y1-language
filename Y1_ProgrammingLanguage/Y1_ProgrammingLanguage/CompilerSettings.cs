using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using static Kronosta.Language.Y1.CompilerSettings;

namespace Kronosta.Language.Y1
{
    public class CompilerSettings
    {
        public delegate ValueTuple<IList<string>, IDictionary<string, object>> Step(
            ValueTuple<IList<string>, IDictionary<string, object>> input,
            IList<ValueTuple<IList<string>, IDictionary<string, object>>> sourceList,
            FauxRefParameter<int> sourceListIndex
        );
        public delegate ValueTuple<IList<string>, IDictionary<string, object>> FromSource(string source);
        public delegate Compilation ToCompilation(IList<ValueTuple<IList<string>, IDictionary<string, object>>> pieces);

        public string LanguageCode = "en-US";
        public string NamespaceSeparator = "~";
        public Registry<Step> AvailableSteps = new Registry<Step>();
        public IList<ValueTuple<string,string>> StepOrder = new List<ValueTuple<string,string>>();
        public FromSource FromSourceFunc =
            x => ValueTuple.Create(new List<string> { x }, new Dictionary<string,object>());
        public ToCompilation? ToCompilationFunc;
        public string AssemblyName = "TestY1";
        public bool PrintOutCSharp = false;

        public CompilerSettings() { }
    }
}
