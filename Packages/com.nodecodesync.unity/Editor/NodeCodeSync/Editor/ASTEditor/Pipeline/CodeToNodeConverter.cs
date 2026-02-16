using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

// Path: NodeCodeSync/Editor/ASTEditor/Pipeline/CodeToNodeConverter.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// Represents a temporary node structure during the conversion process from C# code to a visual graph.
    /// This structure is used to build the hierarchy before converting to persistent graph edges.
    /// </summary>
    public class ConvertedNode
    {
        public NodeMeta Self;
        public Dictionary<string, ConvertedNode[]> FieldChildren;
    }

    /// <summary>
    /// Core engine that converts C# Source Code (Roslyn SyntaxTree) into a ConvertedNode tree.
    /// Uses reflection-based mapping to align Roslyn's internal properties with the custom XML schema.
    /// </summary>
    public static class CodeToNodeConverter
    {
        /// <summary>
        /// Entry point: Converts raw C# source code into a visual-ready node tree.
        /// </summary>
        /// <param name="sourceCode">The raw C# source string.</param>
        /// <returns>The root ConvertedNode of the generated tree.</returns>
        public static ConvertedNode CsharpToConvertedTree(string sourceCode)
        {
            var astRoot = NodeToCodeConverter.CSharpToAST(sourceCode);
            var cache = RoslynSchemaCache.Instance;
            var sb = new StringBuilder();
            sb.AppendLine("[SB] === CodeToNode Start ===");

            var result = BuildConvertedNode(astRoot, cache, sb, 0);

            sb.AppendLine("[SB] === CodeToNode End ===");
            UnityEngine.Debug.Log(sb.ToString());

            return result;
        }

        /// <summary>
        /// Recursively builds ConvertedNodes from Roslyn SyntaxNodes.
        /// Maps SyntaxKinds to NodeMeta templates and populates values.
        /// </summary>
        static ConvertedNode BuildConvertedNode(SyntaxNode syntaxNode, RoslynSchemaCache cache, StringBuilder sb, int depth)
        {
            var indent = new string(' ', depth * 2);
            var kindName = syntaxNode.Kind().ToString();

            // Retrieve NodeMeta template matching the SyntaxKind
            if (!cache.KindToNodeMetaMap.TryGetValue(kindName, out var meta))
            {
                sb.AppendLine($"{indent}⚠ Kind not found: {kindName}");
                return null;
            }

            // Populate tokens and choice indices
            meta = FillValues(meta, syntaxNode, cache);

            // Assign unique identity
            var guid = System.Guid.NewGuid().ToString();
            meta = new NodeMeta(
                meta.Name, meta.Base, meta.Kinds, meta.Fields,
                meta.TypeComment, meta.FactoryComment, meta.SkipConvenienceFactories,
                guid
            );

            sb.AppendLine($"{indent}✓ {meta.Name} (Kind={kindName}, Guid={guid[..8]})");

            // Build structural children using reflection to ensure property-to-field alignment
            var fieldChildren = BuildFieldChildrenByReflection(syntaxNode, cache, sb, depth, meta);

            // Refine ChoiceIndex based on actual populated child data
            meta = FixChoiceIndexByFieldChildren(meta, fieldChildren);

            return new ConvertedNode
            {
                Self = meta,
                FieldChildren = fieldChildren
            };
        }

        /// <summary>
        /// Scans the FieldUnits to collect metadata for all fields categorized as 'Nodes'.
        /// </summary>
        static List<(string Name, string FieldType)> CollectNodeFields(FieldUnit[] fields)
        {
            var result = new List<(string, string)>();
            if (fields == null) return result;

            foreach (var field in fields)
                CollectNodeFieldsRecursive(field, result);

            return result;
        }

        static void CollectNodeFieldsRecursive(FieldUnit unit, List<(string Name, string FieldType)> result)
        {
            if (unit.Type == FieldUnitType.Single && !string.IsNullOrEmpty(unit.Data.Name))
            {
                var kind = FieldTypeClassifier.Classify(unit.Data.FieldType);
                if (FieldTypeClassifier.IsNodeType(kind))
                {
                    result.Add((unit.Data.Name, unit.Data.FieldType));
                }
            }

            if (unit.Children != null)
            {
                foreach (var child in unit.Children)
                    CollectNodeFieldsRecursive(child, result);
            }
        }

        /// <summary>
        /// Uses reflection to traverse Roslyn SyntaxNode properties and map them to schema fields.
        /// Filters out non-schema properties (like 'Parent') to avoid circular references.
        /// </summary>
        static Dictionary<string, ConvertedNode[]> BuildFieldChildrenByReflection(
            SyntaxNode syntaxNode, RoslynSchemaCache cache, StringBuilder sb, int depth,
            NodeMeta meta)
        {
            var indent = new string(' ', depth * 2);
            var fieldChildren = new Dictionary<string, ConvertedNode[]>();

            // Create a set of valid node-type field names from the schema
            var validFieldNames = new HashSet<string>();
            foreach (var (name, _) in CollectNodeFields(meta.Fields))
                validFieldNames.Add(name);

            if (validFieldNames.Count == 0) return fieldChildren;

            var props = syntaxNode.GetType().GetProperties(
                BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                // Skip properties not defined in our XML schema
                if (!validFieldNames.Contains(prop.Name)) continue;

                var propType = prop.PropertyType;

                // Case: Single SyntaxNode
                if (typeof(SyntaxNode).IsAssignableFrom(propType))
                {
                    var childSyntax = prop.GetValue(syntaxNode) as SyntaxNode;
                    if (childSyntax == null) continue;

                    var converted = BuildConvertedNode(childSyntax, cache, sb, depth + 1);
                    if (converted != null)
                    {
                        fieldChildren[prop.Name] = new[] { converted };
                        sb.AppendLine($"{indent}  .{prop.Name} → {converted.Self.Name}");
                    }
                    continue;
                }

                // Case: SyntaxList<T> or SeparatedSyntaxList<T>
                if (typeof(System.Collections.IEnumerable).IsAssignableFrom(propType)
                    && propType != typeof(string))
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
                            var converted = BuildConvertedNode(childNode, cache, sb, depth + 1);
                            if (converted != null)
                                list.Add(converted);
                        }
                    }

                    if (list.Count > 0)
                    {
                        fieldChildren[prop.Name] = list.ToArray();
                        sb.AppendLine($"{indent}  .{prop.Name} → [{list.Count}] items");
                    }
                }
            }

            return fieldChildren;
        }

        /// <summary>
        /// Corrects the ChoiceIndex of a NodeMeta by checking which field actually contains data.
        /// </summary>
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
                // Iterate through choice options to find the one with active data
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
            {
                foreach (var child in unit.Children)
                    meta = FixChoiceRecursive(meta, child, fieldChildren);
            }

            return meta;
        }

        /// <summary>
        /// Determines if a specific child of a Choice unit contains actual data in the fieldChildren map.
        /// </summary>
        static bool ChoiceChildHasData(FieldUnit child, Dictionary<string, ConvertedNode[]> fieldChildren)
        {
            if (child.Type == FieldUnitType.Single && !string.IsNullOrEmpty(child.Data.Name))
            {
                var kind = FieldTypeClassifier.Classify(child.Data.FieldType);
                if (FieldTypeClassifier.IsNodeType(kind))
                    return fieldChildren.ContainsKey(child.Data.Name);
            }

            // Sequence: True if any nested child has data
            if (child.Children != null)
            {
                foreach (var c in child.Children)
                {
                    if (ChoiceChildHasData(c, fieldChildren)) return true;
                }
            }

            return false;
        }

        // ===================================================================
        // Value Injection Helpers
        // ===================================================================

        static NodeMeta FillValues(NodeMeta meta, SyntaxNode syntaxNode, RoslynSchemaCache cache)
        {
            if (meta.Fields == null) return meta;

            // Map token values from Roslyn properties to schema fields
            meta = FillTokensByReflection(meta, syntaxNode);

            // Populate initial choice indices
            for (int i = 0; i < meta.Fields.Length; i++)
                meta = FillChoiceIndex(meta, meta.Fields[i], syntaxNode);

            return meta;
        }

        /// <summary>
        /// Maps Roslyn SyntaxTokens to schema fields.
        /// Concatenates TokenLists (e.g., Modifiers) with space separators.
        /// </summary>
        static NodeMeta FillTokensByReflection(NodeMeta meta, SyntaxNode syntaxNode)
        {
            var tokenFieldNames = CollectTokenFields(meta.Fields);
            if (tokenFieldNames.Count == 0) return meta;

            var props = syntaxNode.GetType().GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var prop in props)
            {
                if (!tokenFieldNames.TryGetValue(prop.Name, out var fieldType)) continue;
                var kind = FieldTypeClassifier.Classify(fieldType);

                if (kind == FieldTypeKind.Token)
                {
                    if (prop.PropertyType == typeof(SyntaxToken))
                    {
                        var token = (SyntaxToken)prop.GetValue(syntaxNode);
                        if (!token.IsMissing && !string.IsNullOrEmpty(token.Text))
                            meta = meta.UpdateValue(prop.Name, token.Text);
                    }
                }
                else if (kind == FieldTypeKind.TokenList)
                {
                    var value = prop.GetValue(syntaxNode);
                    if (value is SyntaxTokenList tokenList && tokenList.Count > 0)
                    {
                        var joined = string.Join(" ", tokenList
                            .Where(t => !t.IsMissing && !string.IsNullOrEmpty(t.Text))
                            .Select(t => t.Text));
                        if (!string.IsNullOrEmpty(joined))
                            meta = meta.UpdateValue(prop.Name, joined);
                    }
                }
            }

            return meta;
        }

        /// <summary>
        /// Scans for Token-type fields within the schema.
        /// </summary>
        static Dictionary<string, string> CollectTokenFields(FieldUnit[] fields)
        {
            var result = new Dictionary<string, string>();
            if (fields == null) return result;

            foreach (var field in fields)
                CollectTokenFieldsRecursive(field, result);

            return result;
        }

        static void CollectTokenFieldsRecursive(FieldUnit unit, Dictionary<string, string> result)
        {
            if (unit.Type == FieldUnitType.Single && !string.IsNullOrEmpty(unit.Data.Name))
            {
                var kind = FieldTypeClassifier.Classify(unit.Data.FieldType);
                if (FieldTypeClassifier.IsTokenType(kind))
                    result[unit.Data.Name] = unit.Data.FieldType;
            }

            if (unit.Children != null)
            {
                foreach (var child in unit.Children)
                    CollectTokenFieldsRecursive(child, result);
            }
        }

        /// <summary>
        /// Handles tokens without predefined Kinds (e.g., Identifiers, Literals).
        /// Fills the first available empty Token field.
        /// </summary>
        static NodeMeta FillUnmappedToken(NodeMeta meta, SyntaxToken token)
        {
            return FillUnmappedRecursive(meta, meta.Fields, token);
        }

        static NodeMeta FillUnmappedRecursive(NodeMeta meta, FieldUnit[] fields, SyntaxToken token)
        {
            if (fields == null) return meta;

            foreach (var field in fields)
            {
                if (field.Type == FieldUnitType.Single && !string.IsNullOrEmpty(field.Data.Name))
                {
                    var kind = FieldTypeClassifier.Classify(field.Data.FieldType);
                    if (FieldTypeClassifier.IsTokenType(kind)
                        && (field.Data.Kinds == null || field.Data.Kinds.Length == 0)
                        && string.IsNullOrEmpty(field.Data.Value))
                    {
                        meta = meta.UpdateValue(field.Data.Name, token.Text);
                        return meta;
                    }
                }

                if (field.Children != null)
                    meta = FillUnmappedRecursive(meta, field.Children, token);
            }

            return meta;
        }

        /// <summary>
        /// Initial pass to set ChoiceIndex based on the presence of child Kinds.
        /// </summary>
        static NodeMeta FillChoiceIndex(NodeMeta meta, FieldUnit unit, SyntaxNode syntaxNode)
        {
            if (unit.Type == FieldUnitType.Choice && unit.Children?.Length > 0)
            {
                var childKinds = new HashSet<string>(
                    syntaxNode.ChildNodes().Select(c => c.Kind().ToString())
                );
                var childTokenKinds = new HashSet<string>(
                    syntaxNode.ChildTokens().Select(t => t.Kind().ToString())
                );

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
            {
                foreach (var child in unit.Children)
                    meta = FillChoiceIndex(meta, child, syntaxNode);
            }

            return meta;
        }

        /// <summary>
        /// Checks if a child unit's Kinds exist within the current SyntaxNode's children.
        /// </summary>
        static bool ChoiceChildExists(FieldUnit child, HashSet<string> childKinds, HashSet<string> childTokenKinds)
        {
            if (child.Type != FieldUnitType.Single || string.IsNullOrEmpty(child.Data.FieldType))
                return false;

            var kind = FieldTypeClassifier.Classify(child.Data.FieldType);

            if (FieldTypeClassifier.IsTokenType(kind))
            {
                if (child.Data.Kinds != null)
                {
                    foreach (var k in child.Data.Kinds)
                    {
                        if (childTokenKinds.Contains(k)) return true;
                    }
                }
                return false;
            }

            if (FieldTypeClassifier.IsNodeType(kind))
            {
                return childKinds.Count > 0;
            }

            return false;
        }
    }
}