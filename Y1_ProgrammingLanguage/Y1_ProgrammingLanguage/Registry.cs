using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Kronosta.Language.Y1
{
    public delegate string LocalizedStringProvider(string langCode);

    public class Registry<T>
    {

        internal IDictionary<ValueTuple<string, string>, ValueTuple<T, object?>> Entries;
        internal IDictionary<ValueTuple<string, string>, LocalizedStringProvider?> EntryLocalizers;

        public Registry() {
            Entries =
                new Dictionary<ValueTuple<string, string>, ValueTuple<T, object?>>();
            EntryLocalizers =
                new Dictionary<ValueTuple<string, string>, LocalizedStringProvider?>();
        }

        public T GetEntry(string @namespace, string entry)
        {
            try
            {
                return Entries[ValueTuple.Create(@namespace, entry)].Item1;
            }
            catch
            {
                return default(T);
            }
        }

        public object? GetState(string @namespace, string entry)
        {
            try
            {
                return Entries[ValueTuple.Create(@namespace, entry)].Item2;
            }
            catch
            {
                return null;
            }
        }

        public ValueTuple<string, string> GetIDFromLocalized(string langCode, string localizedNamespace, string localizedEntry)
        {
            string? @namespace = Registry.NamespaceLocalizers
                .Where(pair => pair.Value(langCode) == localizedNamespace)
                .Select(pair => pair.Key)
                .DefaultIfEmpty(null)
                .First();
            string? entry = this.EntryLocalizers
                .Where(pair => pair.Value(langCode) == localizedNamespace && pair.Key.Item1 == @namespace)
                .Select(pair => pair.Key.Item2)
                .DefaultIfEmpty(null)
                .First();
            if (@namespace == null) @namespace = localizedNamespace;
            if (entry == null) entry = localizedEntry;
            return ValueTuple.Create(@namespace, entry);
        }

        public ValueTuple<string, string> GetIDFromLocalizedQualified(string langCode, string separator, string composed)
        {
            string[] split = composed.Split(separator);
            if (split.Length == 1)
                return GetIDFromLocalized(langCode, "", split[0]);
            else if (split.Length == 2)
                return GetIDFromLocalized(langCode, split[0], split[1]);
            else
                throw new Registry.RegistryException($"Too many components in identifier '{composed}' with separator '{separator}'.");
        }

        public void Register(
            string @namespace,
            string entry,
            T payload,
            object? defaultState = null,
            LocalizedStringProvider? localizer = null)
        {
            var tuple = ValueTuple.Create(@namespace, entry);
            Entries.Add(tuple, ValueTuple.Create(payload, defaultState));
            if (localizer != null) EntryLocalizers.Add(tuple, localizer);
        }
    }

    public class ImmutableRegistry<T>
    {
        internal ImmutableDictionary<Tuple<string, string>, Tuple<T, ImmutableDictionary<string, string>?>> Entries;
        internal ImmutableDictionary<Tuple<string, string>, LocalizedStringProvider?> EntryLocalizers;

        public ImmutableRegistry()
        {
            this.Entries =
                ImmutableDictionary.Create<Tuple<string, string>, Tuple<T, ImmutableDictionary<string, string>?>>();
            this.EntryLocalizers =
                ImmutableDictionary.Create<Tuple<string, string>, LocalizedStringProvider?>();
        }

        public ImmutableRegistry(
            ImmutableDictionary<Tuple<string, string>, Tuple<T, ImmutableDictionary<string, string>?>> Entries,
            ImmutableDictionary<Tuple<string, string>, LocalizedStringProvider?> EntryLocalizers)
        { 
            this.Entries = Entries;
            this.EntryLocalizers = EntryLocalizers;
        }

        public T GetEntry(string @namespace, string entry)
        {
            try
            {
                return Entries[Tuple.Create(@namespace, entry)].Item1;
            }
            catch
            {
                return default(T);
            }
        }

        public object? GetState(string @namespace, string entry)
        {
            try
            {
                return Entries[Tuple.Create(@namespace, entry)].Item2;
            }
            catch
            {
                return null;
            }
        }

        public ValueTuple<string, string> GetIDFromLocalized(string langCode, string localizedNamespace, string localizedEntry)
        {
            string? @namespace = Registry.NamespaceLocalizers
                .Where(pair => pair.Value(langCode) == localizedNamespace)
                .Select(pair => pair.Key)
                .DefaultIfEmpty(null)
                .First();
            string? entry = this.EntryLocalizers
                .Where(pair => pair.Value(langCode) == localizedNamespace && pair.Key.Item1 == @namespace)
                .Select(pair => pair.Key.Item2)
                .DefaultIfEmpty(null)
                .First();
            if (@namespace == null) @namespace = localizedNamespace;
            if (entry == null) entry = localizedEntry;
            return ValueTuple.Create(@namespace, entry);
        }

        public ValueTuple<string, string> GetIDFromLocalizedQualified(string langCode, string separator, string composed)
        {
            string[] split = composed.Split(separator);
            if (split.Length == 1)
                return GetIDFromLocalized(langCode, "", split[0]);
            else if (split.Length == 2)
                return GetIDFromLocalized(langCode, split[0], split[1]);
            else
                throw new Registry.RegistryException($"Too many components in identifier '{composed}' with separator '{separator}'.");
        }
        public ImmutableRegistry<T> Register(
            string @namespace,
            string entry,
            T payload,
            ImmutableDictionary<string,string>? defaultState = null,
            LocalizedStringProvider? localizer = null)
        {
            var tuple = Tuple.Create(@namespace, entry);
            return new ImmutableRegistry<T>(
                Entries.Add(tuple, Tuple.Create(payload, defaultState)),
                localizer != null ? EntryLocalizers.Add(tuple, localizer) : EntryLocalizers
            );
        }

    }

    public static class Registry
    {
        public class RegistryException : Exception
        {
            public RegistryException()
            {
            }

            public RegistryException(string message)
                : base(message)
            {
            }

            public RegistryException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }

        internal static readonly Dictionary<string, LocalizedStringProvider?> NamespaceLocalizers =
            new Dictionary<string, LocalizedStringProvider?>();

        public static void RegisterNamespace(string @namespace, LocalizedStringProvider localizer)
        {
            NamespaceLocalizers.Add(@namespace, localizer);
        }
    }
}
