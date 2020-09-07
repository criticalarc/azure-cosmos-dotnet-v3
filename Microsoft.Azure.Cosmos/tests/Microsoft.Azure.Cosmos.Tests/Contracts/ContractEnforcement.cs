﻿namespace Microsoft.Azure.Cosmos.Tests.Contracts
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    public class ContractEnforcement
    {
        private static readonly InvariantComparer invariantComparer = new InvariantComparer();

        private static Assembly GetAssemblyLocally(string name)
        {
            Assembly.Load(name);
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            return loadedAssemblies
                .Where((candidate) => candidate.FullName.Contains(name + ","))
                .FirstOrDefault();
        }

        private sealed class MemberMetadata
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public MemberTypes Type { get; }
            public List<string> Attributes { get; }
            public string MethodInfo { get; }
            public MemberMetadata(MemberTypes type, List<string> attributes, string methodInfo)
            {
                this.Type = type;
                this.Attributes = attributes;
                this.MethodInfo = methodInfo;
            }
        }

        private sealed class TypeTree
        {
            [JsonIgnore]
            public Type Type { get; }
            public Dictionary<string, TypeTree> Subclasses { get; } = new Dictionary<string, TypeTree>();
            public Dictionary<string, MemberMetadata> Members { get; } = new Dictionary<string, MemberMetadata>();
            public Dictionary<string, TypeTree> NestedTypes { get; } = new Dictionary<string, TypeTree>();

            public TypeTree(Type type)
            {
                this.Type = type;
            }
        }

        private static IEnumerable<CustomAttributeData> RemoveDebugSpecificAttributes(IEnumerable<CustomAttributeData> attributes)
        {
            return attributes.Where(x =>
                !x.AttributeType.Name.Contains("SuppressMessageAttribute") &&
                !x.AttributeType.Name.Contains("DynamicallyInvokableAttribute") &&
                !x.AttributeType.Name.Contains("NonVersionableAttribute") &&
                !x.AttributeType.Name.Contains("ReliabilityContractAttribute") &&
                !x.AttributeType.Name.Contains("NonVersionableAttribute") &&
                !x.AttributeType.Name.Contains("DebuggerStepThroughAttribute") &&
                !x.AttributeType.Name.Contains("IsReadOnlyAttribute")
            );
        }

        private static TypeTree BuildTypeTree(TypeTree root, Type[] types)
        {
            IEnumerable<Type> subclassTypes = types.Where((type) => type.IsSubclassOf(root.Type)).OrderBy(o => o.FullName, invariantComparer);
            foreach (Type subclassType in subclassTypes)
            {
                root.Subclasses[subclassType.Name] = ContractEnforcement.BuildTypeTree(new TypeTree(subclassType), types);
            }

            IEnumerable<KeyValuePair<string, MemberInfo>> memberInfos =
                root.Type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Select(memberInfo => new KeyValuePair<string, MemberInfo>($"{memberInfo.ToString()}{string.Join("-", ContractEnforcement.RemoveDebugSpecificAttributes(memberInfo.CustomAttributes))}", memberInfo))
                    .OrderBy(o => o.Key, invariantComparer);
            foreach (KeyValuePair<string, MemberInfo> memberInfo in memberInfos)
            {
                List<string> attributes = ContractEnforcement.RemoveDebugSpecificAttributes(memberInfo.Value.CustomAttributes)
                        .Select((customAttribute) => customAttribute.AttributeType.Name)
                        .ToList();
                attributes.Sort(invariantComparer);

                string methodSignature = null;

                if (memberInfo.Value.MemberType == MemberTypes.Constructor | memberInfo.Value.MemberType == MemberTypes.Method | memberInfo.Value.MemberType == MemberTypes.Event)
                {
                    methodSignature = memberInfo.Value.ToString();
                }

                root.Members[
                        memberInfo.Key
                    ] = new MemberMetadata(
                    memberInfo.Value.MemberType,
                    attributes,
                    methodSignature);
            }

            foreach (Type nestedType in root.Type.GetNestedTypes().OrderBy(o => o.FullName))
            {
                root.NestedTypes[nestedType.Name] = ContractEnforcement.BuildTypeTree(new TypeTree(nestedType), types);
            }

            return root;
        }

        public static void ValidateContractContainBreakingChanges(
            string dllName,
            string baselinePath,
            string breakingChangesPath)
        {
            string localJson = GetCurrentContract(dllName);
            File.WriteAllText($"Contracts/{breakingChangesPath}", localJson);

            string baselineJson = GetBaselineContract(baselinePath);
            ContractEnforcement.ValidateJsonAreSame(localJson, baselineJson);
        }

        public static void ValidatePreviewContractContainBreakingChanges(
            string dllName,
            string officialBaselinePath,
            string previewBaselinePath,
            string previewBreakingChangesPath)
        {
            string currentPreviewJson = ContractEnforcement.GetCurrentContract(
              dllName);

            JObject currentJObject = JObject.Parse(currentPreviewJson);
            JObject officialBaselineJObject = JObject.Parse(File.ReadAllText("Contracts/" + officialBaselinePath));

            string currentJsonNoOfficialContract = ContractEnforcement.RemoveDuplicateContractElements(
                localContract: currentJObject,
                officialContract: officialBaselineJObject);

            Assert.IsNotNull(currentJsonNoOfficialContract);

            string baselinePreviewJson = ContractEnforcement.GetBaselineContract(previewBaselinePath);
            File.WriteAllText($"Contracts/{previewBreakingChangesPath}", currentJsonNoOfficialContract);

            ContractEnforcement.ValidateJsonAreSame(baselinePreviewJson, currentJsonNoOfficialContract);
        }

        public static string GetCurrentContract(string dllName)
        {
            TypeTree locally = new TypeTree(typeof(object));
            Assembly assembly = ContractEnforcement.GetAssemblyLocally(dllName);
            Type[] exportedTypes = assembly.GetExportedTypes();
            ContractEnforcement.BuildTypeTree(locally, exportedTypes);

            string localJson = JsonConvert.SerializeObject(locally, Formatting.Indented);
            return localJson;
        }

        public static string GetBaselineContract(string baselinePath)
        {
            string baselineFile = File.ReadAllText("Contracts/" + baselinePath);
            return NormalizeJsonString(baselineFile);
        }

        public static string RemoveDuplicateContractElements(JObject localContract, JObject officialContract)
        {
            RemoveDuplicateContractHelper(localContract, officialContract);
            string noDuplicates = localContract.ToString();
            return NormalizeJsonString(noDuplicates);
        }

        private static string NormalizeJsonString(string file)
        {
            TypeTree baseline = JsonConvert.DeserializeObject<TypeTree>(file);
            string updatedString = JsonConvert.SerializeObject(baseline, Formatting.Indented);
            return updatedString;
        }

        private static void RemoveDuplicateContractHelper(JObject previewContract, JObject officialContract)
        {
            foreach (KeyValuePair<string, JToken> token in officialContract)
            {
                JToken previewLocalToken = previewContract[token.Key];
                if (previewLocalToken != null)
                {
                    if (JToken.DeepEquals(previewLocalToken, token.Value))
                    {
                        previewContract.Remove(token.Key);
                    }
                    else if (previewLocalToken.Type == JTokenType.Object && token.Value.Type == JTokenType.Object)
                    {
                        RemoveDuplicateContractHelper(previewLocalToken as JObject, token.Value as JObject);
                    }
                }
            }
        }

        public static void ValidateJsonAreSame(string baselineJson, string currentJson)
        {
            // This prevents failures caused by it being serialized slightly different order
            string normalizedBaselineJson = NormalizeJsonString(baselineJson);
            string normalizedCurrentJson = NormalizeJsonString(currentJson);
            System.Diagnostics.Trace.TraceWarning($"String length Expected: {normalizedBaselineJson.Length};Actual:{normalizedCurrentJson.Length}");
            if (!string.Equals(normalizedCurrentJson, normalizedBaselineJson, StringComparison.InvariantCulture))
            {
                System.Diagnostics.Trace.TraceWarning($"Expected: {normalizedBaselineJson}");
                System.Diagnostics.Trace.TraceWarning($"Actual: {normalizedCurrentJson}");
                Assert.Fail($@"Public API has changed. If this is expected, then run (EnlistmentRoot)\UpdateContracts.ps1 . To see the differences run the update script and use git diff.");
            }
        }

        private class InvariantComparer : IComparer<string>
        {
            public int Compare(string a, string b) => Comparer.DefaultInvariant.Compare(a, b);
        }
    }
}
