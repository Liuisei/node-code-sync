using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Pipeline/CodeToNodeConverter.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// 変換時の一時ツリー構造（グラフ配置後は捨てる）
    /// </summary>
    public class ConvertedNode
    {
        public NodeMeta Self;
        public Dictionary<string, ConvertedNode[]> FieldChildren;
    }

    /// <summary>
    /// C# → ConvertedNode ツリー（リフレクション完全ゼロ）
    /// Kind名でキャッシュから雛形取得 → Value / ChoiceIndex を差し込み
    /// 親子関係は FieldChildren で保持 → グラフ配置時に Edge へ変換
    /// </summary>
    public static class CodeToNodeConverter
    {
        /// <summary>
        /// C#ソースコード → ConvertedNode ツリーのルートを返す
        /// </summary>
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
        /// SyntaxNode → ConvertedNode（再帰）
        /// 自分のNodeMetaを作り、子SyntaxNodeをFieldName別に振り分ける
        /// </summary>
        static ConvertedNode BuildConvertedNode(SyntaxNode syntaxNode, RoslynSchemaCache cache, StringBuilder sb, int depth)
        {
            var indent = new string(' ', depth * 2);
            var kindName = syntaxNode.Kind().ToString();

            // Kind→NodeMeta テンプレート取得
            if (!cache.KindToNodeMetaMap.TryGetValue(kindName, out var meta))
            {
                sb.AppendLine($"{indent}⚠ Kind not found: {kindName}");
                return null;
            }

            // Token値・ChoiceIndex埋め
            meta = FillValues(meta, syntaxNode, cache);

            // GUID付与
            var guid = System.Guid.NewGuid().ToString();
            meta = new NodeMeta(
                meta.Name, meta.Base, meta.Kinds, meta.Fields,
                meta.TypeComment, meta.FactoryComment, meta.SkipConvenienceFactories,
                guid
            );

            sb.AppendLine($"{indent}✓ {meta.Name} (Kind={kindName}, Guid={guid[..8]})");

            // FieldChildren構築: リフレクションでプロパティ名→子SyntaxNodeを確実に紐付け
            var fieldChildren = BuildFieldChildrenByReflection(syntaxNode, cache, sb, depth, meta);

            // FieldChildrenの実データを使ってChoiceIndexを修正
            meta = FixChoiceIndexByFieldChildren(meta, fieldChildren);

            return new ConvertedNode
            {
                Self = meta,
                FieldChildren = fieldChildren
            };
        }

        /// <summary>
        /// Fields配列からNode系フィールドの(Name, FieldType)を走査順で収集
        /// Choice/Sequence内も再帰的に探索
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
        /// リフレクションでSyntaxNodeのプロパティを走査し、
        /// プロパティ名＝fieldNameとしてFieldChildrenを構築する
        /// スキーマに存在するNode系フィールド名だけ対象にする（Parent等の逆参照を除外）
        /// </summary>
        static Dictionary<string, ConvertedNode[]> BuildFieldChildrenByReflection(
            SyntaxNode syntaxNode, RoslynSchemaCache cache, StringBuilder sb, int depth,
            NodeMeta meta)
        {
            var indent = new string(' ', depth * 2);
            var fieldChildren = new Dictionary<string, ConvertedNode[]>();

            // スキーマからNode系フィールド名のSetを作る（これに含まれるプロパティだけ対象）
            var validFieldNames = new HashSet<string>();
            foreach (var (name, _) in CollectNodeFields(meta.Fields))
                validFieldNames.Add(name);

            if (validFieldNames.Count == 0) return fieldChildren;

            var props = syntaxNode.GetType().GetProperties(
                BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                // スキーマに無いプロパティはスキップ（Parent等の逆参照防止）
                if (!validFieldNames.Contains(prop.Name)) continue;

                var propType = prop.PropertyType;

                // 単一SyntaxNode
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

                // SyntaxList<T> / SeparatedSyntaxList<T>
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
        /// FieldChildrenの実データを使ってChoiceIndexを修正する
        /// リフレクションで正確な子振り分けが済んだ後に呼ぶ
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
                // Choiceの各子を見て、FieldChildrenにキーが存在する方を選ぶ
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
        /// Choice の子フィールドが FieldChildren に実データを持つか判定
        /// </summary>
        static bool ChoiceChildHasData(FieldUnit child, Dictionary<string, ConvertedNode[]> fieldChildren)
        {
            if (child.Type == FieldUnitType.Single && !string.IsNullOrEmpty(child.Data.Name))
            {
                var kind = FieldTypeClassifier.Classify(child.Data.FieldType);
                if (FieldTypeClassifier.IsNodeType(kind))
                    return fieldChildren.ContainsKey(child.Data.Name);
            }

            // Sequence: 子のどれかがFieldChildrenに存在すればOK
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
        // 以下、既存のFillValues系（変更なし）
        // ===================================================================

        static NodeMeta FillValues(NodeMeta meta, SyntaxNode syntaxNode, RoslynSchemaCache cache)
        {
            if (meta.Fields == null) return meta;

            // Token値埋め: リフレクションでプロパティ名→フィールド名を正確に紐付け
            meta = FillTokensByReflection(meta, syntaxNode);

            // ChoiceIndex 埋め
            for (int i = 0; i < meta.Fields.Length; i++)
                meta = FillChoiceIndex(meta, meta.Fields[i], syntaxNode);

            return meta;
        }

        /// <summary>
        /// リフレクションでSyntaxNodeのTokenプロパティを走査し、
        /// フィールド名と一致するプロパティの値をNodeMetaに埋める
        /// TokenList（Modifiers等）はスペース区切りで結合
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
                    // 単一トークン: SyntaxToken型
                    if (prop.PropertyType == typeof(SyntaxToken))
                    {
                        var token = (SyntaxToken)prop.GetValue(syntaxNode);
                        if (!token.IsMissing && !string.IsNullOrEmpty(token.Text))
                            meta = meta.UpdateValue(prop.Name, token.Text);
                    }
                }
                else if (kind == FieldTypeKind.TokenList)
                {
                    // TokenList: SyntaxTokenList型 → スペース区切りで結合
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
        /// Fields配列からToken系フィールドの(Name→FieldType)を収集
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
        /// Kinds[] に登録がないトークン（Identifier, Literal 等）
        /// まだ Value が空の Kinds なし Token フィールドに入れる
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
