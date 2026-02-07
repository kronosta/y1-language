using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Xml;

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
        public IList<ValueTuple<string, string>> StepOrder = new List<ValueTuple<string, string>>();
        public FromSource FromSourceFunc =
            x => ValueTuple.Create(new List<string> { x }, new Dictionary<string, object>());
        public ToCompilation? ToCompilationFunc;
        public string AssemblyName = "TestY1";
        public bool PrintOutCSharp = false;
        public Compiler Compiler { get; internal set; }

        public CompilerSettings() { }

        public void ModifyFromY1Code(string y1Code) {
            Utils.Y1ToFunc(y1Code)(this);
        }

        public static CompilerSettings ReadFromXML(string xml)
        {
            CompilerSettings settings = new CompilerSettings();
            XmlDocument doc = new XmlDocument();
            doc.Load(xml);
            var xmlSettings = doc["CompilerSettings"];
            settings.LanguageCode = xmlSettings?.Attributes?["LanguageCode"]?.Value ?? "en-US";
            settings.NamespaceSeparator = xmlSettings?.Attributes?["NamespaceSeparator"]?.Value ?? "~";
            settings.AssemblyName = xmlSettings?.Attributes?["AssemblyName"]?.Value ?? "TestY1";
            settings.PrintOutCSharp = (xmlSettings?.Attributes?["PrintOutCSharp"]?.Value ?? "false") == "true";
            string? toCompilationFuncStr = xmlSettings?["ToCompilationFunc"]?.InnerText;
            Func<object, object>? toCompilationBaseFunc = toCompilationFuncStr == null ? null : Utils.Y1ToFunc(toCompilationFuncStr);
            settings.ToCompilationFunc = toCompilationBaseFunc == null ? null : pieces => (Compilation)toCompilationBaseFunc(pieces);
            string? fromSourceFuncStr = xmlSettings?["FromSourceFunc"]?.InnerText;
            Func<object, object>? fromSourceBaseFunc = fromSourceFuncStr == null ? null : Utils.Y1ToFunc(fromSourceFuncStr);
            settings.FromSourceFunc = fromSourceBaseFunc == null ? settings.FromSourceFunc : source =>
                (ValueTuple<IList<string>, IDictionary<string, object>>)fromSourceBaseFunc(source);
            XmlNodeList? availableSteps = xmlSettings?["AvailableSteps"]?.GetElementsByTagName("AvailableStep");
            for (int i = 0; i < (availableSteps?.Count ?? 0); i++)
            {
                string? @namespace = availableSteps?[i]?.Attributes?["Namespace"]?.Value;
                string? entry = availableSteps?[i]?.Attributes?["Entry"]?.Value;
                string? stepStr = availableSteps?[i]?.Attributes?["Step"]?.Value;
                string? defaultStateFuncStr = availableSteps?[i]?.Attributes?["DefaultStateFunc"]?.Value;
                string? localizerStr = availableSteps?[i]?.Attributes?["Localizer"]?.Value;
                if (@namespace != null && entry != null && stepStr != null)
                {
                    Func<object?, object?> stepBaseFunc = Utils.Y1ToFunc(stepStr);
                    Func<object?, object?>? defaultStateBaseFunc = defaultStateFuncStr == null ? null : Utils.Y1ToFunc(defaultStateFuncStr);
                    Func<object?, object?>? localizerBaseFunc = localizerStr == null ? null : Utils.Y1ToFunc(localizerStr);

                    Step step = (input, sourceList, sourceListIndex) =>
                        (ValueTuple<IList<string>, IDictionary<string, object>>)stepBaseFunc((input, sourceList, sourceListIndex));
                    object? defaultState = defaultStateBaseFunc == null ? null : defaultStateBaseFunc(null);
                    LocalizedStringProvider? localizer = localizerBaseFunc == null ? null : (langCode) => (string)localizerBaseFunc(langCode);
                    settings.AvailableSteps.Register(@namespace, entry, step, defaultState, localizer);
                }
            }
            XmlNodeList? stepOrder = xmlSettings?["StepOrder"]?.GetElementsByTagName("Step");
            settings.StepOrder = new List<ValueTuple<string,string>>();
            for (int i = 0; i < (stepOrder?.Count ?? 0); i++)
            {
                string? @namespace = stepOrder?[i]?.Attributes?["Namespace"]?.Value;
                string? entry = stepOrder?[i]?.Attributes?["Entry"]?.Value;
                if (@namespace != null && entry != null)
                    settings.StepOrder.Add(ValueTuple.Create(@namespace, entry));
            }
            return settings;
        }
    }
}
