using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Kronosta.Language.Y1
{
    public class Compiler
    {
        public CompilerSettings CompilerSettings { get; set; }
        public Preprocessor Preprocessor { get; set; }
        public CSharpConverter Converter { get; set; }
        public IDictionary<string, string> FakeFiles { get; set; }

        public Compiler()
        {
            CompilerSettings = new CompilerSettings()
            {
                ToCompilationFunc = this.ToCompilation
            };
            foreach (var i in GetDefaultSteps())
            {
                CompilerSettings.AvailableSteps.Register(i.Item1, i.Item2, i.Item3);
                CompilerSettings.StepOrder.Add(ValueTuple.Create(i.Item1, i.Item2));
            }
            Preprocessor = new Preprocessor(this);
            Converter = new CSharpConverter(this);
            FakeFiles = new Dictionary<string, string>();
        }

        private string Step_Prepreprocess(string source) =>
            Preprocessor.Prepreprocess(source);
        private IList<string> Step_SplitLines(string source) =>
            source.Replace("\r\n", "\n").Split('\n', '\r', '\f', '\v').ToList();
        private IList<string> Step_Preprocess(IList<string> source) =>
            Preprocessor.Preprocess(source.ToList());
        private string Step_Convert(IList<string> source) =>
            Converter.ConvertToCSharp(source.ToList());


        public Tuple<string, string, CompilerSettings.Step>[] GetDefaultSteps() =>
            new Tuple<string, string, CompilerSettings.Step>[]{
                Tuple.Create<string, string, CompilerSettings.Step>("", "Prepreprocess",
                    (x,_,_) => ValueTuple.Create(new List<string> { Step_Prepreprocess(x.Item1[0])}, x.Item2)),
                Tuple.Create<string, string, CompilerSettings.Step>("", "SplitLines",
                    (x,_,_) => ValueTuple.Create(Step_SplitLines(x.Item1[0]), x.Item2)),
                Tuple.Create<string, string, CompilerSettings.Step>("", "Preprocess",
                    (x,_,_) => ValueTuple.Create(Step_Preprocess(x.Item1), x.Item2)),
                Tuple.Create<string, string, CompilerSettings.Step>("", "Convert",
                    (x,_,_) =>
                    {
                        var t = ValueTuple.Create(new List<string> { Step_Convert(x.Item1) }, x.Item2);
                        List<MetadataReference> references =
                            Converter.assemblyRefs.Select<string, MetadataReference>(
                                path => path.StartsWith('\'')
                                ? MetadataReference.CreateFromFile(Assembly.Load(path.Substring(1)).Location)
                                : MetadataReference.CreateFromFile(path)
                            )
                            .Concat(new MetadataReference[]
                            {
                                MetadataReference.CreateFromFile(Assembly.Load("System.Private.CoreLib").Location),
                                MetadataReference.CreateFromFile(Assembly.Load("System.Console").Location),
                                MetadataReference.CreateFromFile(Assembly.Load("System.Linq").Location),
                                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location)
                            })
                            .ToList();
                        t.Item2.Add("References", references);
                        return t;
                    })
            };



        public EmitResult Compile(
            Stream stream,
            string assemblyName,
            IDictionary<string, string> files,
            IList<string> startingSources)
        {
            CompilerSettings.AssemblyName = assemblyName;
            FakeFiles = files;
            IList<ValueTuple<IList<string>, IDictionary<string, object>>> processedSources =
                startingSources
                .Select(s => CompilerSettings.FromSourceFunc(s))
                .ToList();
            for (int i = 0; i < CompilerSettings.StepOrder.Count; i++)
            {
                for (var j = new FauxRefParameter<int>(0); j.Value < processedSources.Count; j.Value++)
                {
                    var stepID = CompilerSettings.StepOrder[i];
                    processedSources[j.Value] = CompilerSettings.AvailableSteps.GetEntry(stepID.Item1, stepID.Item2)(
                        processedSources[j.Value], processedSources, j);

                }
            }
            Compilation? compilation = CompilerSettings.ToCompilationFunc?.Invoke(processedSources);
            if (compilation == null)
                throw new NullReferenceException("CompilerSettings.ToCompilationFunc did not produce a compilation or does not exist.");
            return compilation.Emit(stream);
        }

        public Compilation ToCompilation(IList<ValueTuple<IList<string>, IDictionary<string, object>>> pieces)
        {
            CSharpSyntaxTree[] trees = pieces
                .Select(x => x.Item1)
                .Where(x => x != null && x.Count == 1)
                .Select(x => x[0])
                .Select(x => (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(x))
                .ToArray();
            MetadataReference[] references = pieces
                .Select(x => x.Item2)
                .Where(x => x != null && x.ContainsKey("References"))
                .Select(x => x["References"])
                .Where(x => x is List<MetadataReference>)
                .Select(x => (List<MetadataReference>)x)
                .Aggregate((x, y) => x.Concat(y).ToList())
                .ToArray();
            CSharpCompilation compilation = CSharpCompilation.Create(CompilerSettings.AssemblyName, trees, references);
            return compilation;
        }
    }
}
