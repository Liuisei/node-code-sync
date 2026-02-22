using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

// Path: NodeCodeSync/Editor/ASTEditor/Pipeline/CodeToNodeConverter.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    public class ConvertedNode
    {
        public NodeMeta Self;
        public Dictionary<string, ConvertedNode[]> FieldChildren;
    }

    public static class CodeToNodeConverter
    {
        // ===================================================================
        // Debug / Timer
        // ===================================================================

        const string _modifier = "[CodeToNodeConverter]";
        private static readonly NCSDebug _debug = new NCSDebug(_modifier);
        private static readonly NCSTimer _timer = new NCSTimer(_modifier);

        // ===================================================================
        // Static caches
        // ===================================================================

        static readonly Dictionary<Type, PropertyInfo[]> s_propertyCache = new();
        static readonly Dictionary<string, List<(string Name, string FieldType)>> s_nodeFieldsCache = new();
        static readonly Dictionary<string, Dictionary<string, string>> s_tokenFieldsCache = new();
        static readonly string[] s_indents = BuildIndentCache(32);

        static string[] BuildIndentCache(int max)
        {
            var arr = new string[max];
            for (int i = 0; i < max; i++) arr[i] = new string(' ', i * 2);
            return arr;
        }

        static string Indent(int depth) =>
            depth < s_indents.Length ? s_indents[depth] : new string(' ', depth * 2);

        static PropertyInfo[] GetProps(Type type)
        {
            if (!s_propertyCache.TryGetValue(type, out var props))
                s_propertyCache[type] = props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            return props;
        }

        // ===================================================================
        // Entry Point
        // ===================================================================

        public static ConvertedNode CsharpToConvertedTree(string sourceCode)
        {
            _timer.Start();

            var astRoot = NodeToCodeConverter.CSharpToAST(sourceCode);
            _timer.Lap("RoslynParse");

            var cache = RoslynSchemaCache.Instance;

            _debug.Log("=== CodeToNode Start ===");
            var result = BuildConvertedNode(astRoot, cache, 0);
            _debug.Log("=== CodeToNode End ===");
            Debug.Log(_debug.PrintLog());

            _timer.Lap("BuildConvertedNode");

            Debug.Log(_timer.Stop("TOTAL"));
            return result;
        }

        // ===================================================================
        // Build
        // ===================================================================

        static ConvertedNode BuildConvertedNode(SyntaxNode syntaxNode, RoslynSchemaCache cache, int depth)
        {
            var kindName = syntaxNode.Kind().ToString();
            if (!cache.KindToNodeMetaMap.TryGetValue(kindName, out var meta))
            {
                _debug.LogWarning($"{Indent(depth)}Kind not found: {kindName}");
                return null;
            }

            meta = FillValues(meta, syntaxNode);
            var guid = Guid.NewGuid().ToString();
            meta = new NodeMeta(meta.Name, meta.Base, meta.Kinds, meta.Fields,
                meta.TypeComment, meta.FactoryComment, meta.SkipConvenienceFactories, guid);

            _debug.Log($"{Indent(depth)}{meta.Name} (Kind={kindName}, Guid={guid[..8]})");

            var fieldChildren = BuildFieldChildren(syntaxNode, cache, depth, meta);
            meta = FixChoiceIndexByFieldChildren(meta, fieldChildren);

            return new ConvertedNode { Self = meta, FieldChildren = fieldChildren };
        }

        static Dictionary<string, ConvertedNode[]> BuildFieldChildren(
            SyntaxNode syntaxNode, RoslynSchemaCache cache, int depth, NodeMeta meta)
        {
            var fieldChildren = new Dictionary<string, ConvertedNode[]>();

            if (!s_nodeFieldsCache.TryGetValue(meta.Name, out var nodeFieldList))
                s_nodeFieldsCache[meta.Name] = nodeFieldList = CollectFields<(string, string)>(meta.Fields, isNode: true);

            var validNames = new HashSet<string>(nodeFieldList.Count);
            foreach (var (name, _) in nodeFieldList) validNames.Add(name);
            if (validNames.Count == 0) return fieldChildren;

            foreach (var prop in GetProps(syntaxNode.GetType()))
            {
                if (!validNames.Contains(prop.Name)) continue;
                var propType = prop.PropertyType;

                if (typeof(SyntaxNode).IsAssignableFrom(propType))
                {
                    var child = prop.GetValue(syntaxNode) as SyntaxNode;
                    if (child == null) continue;
                    var converted = BuildConvertedNode(child, cache, depth + 1);
                    if (converted != null)
                    {
                        fieldChildren[prop.Name] = new[] { converted };
                        _debug.Log($"{Indent(depth)}  .{prop.Name} -> {converted.Self.Name}");
                    }
                    continue;
                }

                if (typeof(System.Collections.IEnumerable).IsAssignableFrom(propType) && propType != typeof(string))
                {
                    if (!propType.IsGenericType) continue;
                    var genArgs = propType.GetGenericArguments();
                    if (genArgs.Length == 0 || !typeof(SyntaxNode).IsAssignableFrom(genArgs[0])) continue;

                    var enumerable = prop.GetValue(syntaxNode) as System.Collections.IEnumerable;
                    if (enumerable == null) continue;

                    var list = new List<ConvertedNode>();
                    foreach (var item in enumerable)
                    {
                        if (item is SyntaxNode childNode)
                        {
                            var converted = BuildConvertedNode(childNode, cache, depth + 1);
                            if (converted != null) list.Add(converted);
                        }
                    }

                    if (list.Count > 0)
                    {
                        fieldChildren[prop.Name] = list.ToArray();
                        _debug.Log($"{Indent(depth)}  .{prop.Name} -> [{list.Count}] items");
                    }
                }
            }

            return fieldChildren;
        }

        // ===================================================================
        // CollectFields — 統合ヘルパー（Node系 / Token系 両対応）
        // ===================================================================

        /// <summary>
        /// FieldUnit[] を再帰走査して Node系 or Token系フィールドを収集する統合メソッド。
        /// isNode=true → List{(Name, FieldType)} を返す想定 (T = (string,string))
        /// isNode=false → Dictionary{Name, FieldType} を返す想定 (T = KeyValuePair)
        /// 実際には呼び出し側で型を使い分けるため、2つのオーバーロードとして実装。
        /// </summary>
        static List<(string Name, string FieldType)> CollectFields<T>(FieldUnit[] fields, bool isNode)
        {
            var result = new List<(string, string)>();
            if (fields == null) return result;
            foreach (var f in fields) CollectFieldsRecursive(f, result, isNode);
            return result;
        }

        static void CollectFieldsRecursive(FieldUnit unit, List<(string Name, string FieldType)> result, bool isNode)
        {
            if (unit.Type == FieldUnitType.Single && !string.IsNullOrEmpty(unit.Data.Name))
            {
                var kind = FieldTypeClassifier.Classify(unit.Data.FieldType);
                bool match = isNode ? FieldTypeClassifier.IsNodeType(kind) : FieldTypeClassifier.IsTokenType(kind);
                if (match) result.Add((unit.Data.Name, unit.Data.FieldType));
            }
            if (unit.Children != null)
                foreach (var child in unit.Children)
                    CollectFieldsRecursive(child, result, isNode);
        }

        static Dictionary<string, string> CollectTokenFieldsDict(FieldUnit[] fields)
        {
            var list = CollectFields<(string, string)>(fields, isNode: false);
            var dict = new Dictionary<string, string>(list.Count);
            foreach (var (name, type) in list) dict[name] = type;
            return dict;
        }

        // ===================================================================
        // FillValues
        // ===================================================================

        static NodeMeta FillValues(NodeMeta meta, SyntaxNode syntaxNode)
        {
            if (meta.Fields == null) return meta;

            meta = FillTokensByReflection(meta, syntaxNode);

            var childKinds = new HashSet<string>();
            var childTokenKinds = new HashSet<string>();
            foreach (var c in syntaxNode.ChildNodes()) childKinds.Add(c.Kind().ToString());
            foreach (var t in syntaxNode.ChildTokens()) childTokenKinds.Add(t.Kind().ToString());

            for (int i = 0; i < meta.Fields.Length; i++)
                meta = FillChoiceIndex(meta, meta.Fields[i], childKinds, childTokenKinds);

            return meta;
        }

        static NodeMeta FillTokensByReflection(NodeMeta meta, SyntaxNode syntaxNode)
        {
            if (!s_tokenFieldsCache.TryGetValue(meta.Name, out var tokenFields))
                s_tokenFieldsCache[meta.Name] = tokenFields = CollectTokenFieldsDict(meta.Fields);
            if (tokenFields.Count == 0) return meta;

            foreach (var prop in GetProps(syntaxNode.GetType()))
            {
                if (!tokenFields.TryGetValue(prop.Name, out var fieldType)) continue;
                var kind = FieldTypeClassifier.Classify(fieldType);

                if (kind == FieldTypeKind.Token && prop.PropertyType == typeof(SyntaxToken))
                {
                    var token = (SyntaxToken)prop.GetValue(syntaxNode);
                    if (!token.IsMissing && !string.IsNullOrEmpty(token.Text))
                        meta = meta.UpdateValue(prop.Name, token.Text);
                }
                else if (kind == FieldTypeKind.TokenList)
                {
                    var value = prop.GetValue(syntaxNode);
                    if (value is SyntaxTokenList tokenList && tokenList.Count > 0)
                    {
                        var sb = new StringBuilder();
                        foreach (var t in tokenList)
                        {
                            if (!t.IsMissing && !string.IsNullOrEmpty(t.Text))
                            {
                                if (sb.Length > 0) sb.Append(' ');
                                sb.Append(t.Text);
                            }
                        }
                        if (sb.Length > 0) meta = meta.UpdateValue(prop.Name, sb.ToString());
                    }
                }
            }

            return meta;
        }

        // ===================================================================
        // ChoiceIndex
        // ===================================================================

        static NodeMeta FillChoiceIndex(NodeMeta meta, FieldUnit unit,
            HashSet<string> childKinds, HashSet<string> childTokenKinds)
        {
            if (unit.Type == FieldUnitType.Choice && unit.Children?.Length > 0)
            {
                for (int i = 0; i < unit.Children.Length; i++)
                {
                    if (ChoiceChildExists(unit.Children[i], childKinds, childTokenKinds))
                    {
                        meta = meta.UpdateValue(unit.Data.Name, null, i);
                        break;
                    }
                }
            }
            if (unit.Children != null)
                foreach (var child in unit.Children)
                    meta = FillChoiceIndex(meta, child, childKinds, childTokenKinds);
            return meta;
        }

        static bool ChoiceChildExists(FieldUnit child, HashSet<string> childKinds, HashSet<string> childTokenKinds)
        {
            if (child.Type != FieldUnitType.Single || string.IsNullOrEmpty(child.Data.FieldType))
                return false;
            var kind = FieldTypeClassifier.Classify(child.Data.FieldType);
            if (FieldTypeClassifier.IsTokenType(kind))
            {
                if (child.Data.Kinds != null)
                    foreach (var k in child.Data.Kinds)
                        if (childTokenKinds.Contains(k)) return true;
                return false;
            }
            return FieldTypeClassifier.IsNodeType(kind) && childKinds.Count > 0;
        }

        static NodeMeta FixChoiceIndexByFieldChildren(NodeMeta meta, Dictionary<string, ConvertedNode[]> fieldChildren)
        {
            if (meta.Fields == null) return meta;
            for (int i = 0; i < meta.Fields.Length; i++)
                meta = FixChoiceRecursive(meta, meta.Fields[i], fieldChildren);
            return meta;
        }

        static NodeMeta FixChoiceRecursive(NodeMeta meta, FieldUnit unit, Dictionary<string, ConvertedNode[]> fieldChildren)
        {
            if (unit.Type == FieldUnitType.Choice && unit.Children?.Length > 0)
            {
                for (int i = 0; i < unit.Children.Length; i++)
                {
                    if (ChoiceChildHasData(unit.Children[i], fieldChildren))
                    {
                        meta = meta.UpdateValue(unit.Data.Name, null, i);
                        break;
                    }
                }
            }
            if (unit.Children != null)
                foreach (var child in unit.Children)
                    meta = FixChoiceRecursive(meta, child, fieldChildren);
            return meta;
        }

        static bool ChoiceChildHasData(FieldUnit child, Dictionary<string, ConvertedNode[]> fieldChildren)
        {
            if (child.Type == FieldUnitType.Single && !string.IsNullOrEmpty(child.Data.Name))
            {
                var kind = FieldTypeClassifier.Classify(child.Data.FieldType);
                if (FieldTypeClassifier.IsNodeType(kind))
                    return fieldChildren.ContainsKey(child.Data.Name);
            }
            if (child.Children != null)
                foreach (var c in child.Children)
                    if (ChoiceChildHasData(c, fieldChildren)) return true;
            return false;
        }
    }
}