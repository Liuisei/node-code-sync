using System;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Schema/SyntaxMetaModel.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    // ============================================
    // トップレベル: Tree要素
    // ============================================
    public struct NCSSyntaxTree
    {
        public readonly string Root;
        public readonly PredefinedNodeMeta[] PredefinedNodes;
        public readonly AbstractNodeMeta[] AbstractNodes;
        public readonly NodeMeta[] Nodes;

        public NCSSyntaxTree(
            string root,
            PredefinedNodeMeta[] predefinedNodes,
            AbstractNodeMeta[] abstractNodes,
            NodeMeta[] nodes)
        {
            Root = root;
            PredefinedNodes = predefinedNodes;
            AbstractNodes = abstractNodes;
            Nodes = nodes;
        }
    }

    // ============================================
    // PredefinedNode要素
    // ============================================
    public readonly struct PredefinedNodeMeta
    {
        public readonly string Name;
        public readonly string Base;

        public PredefinedNodeMeta(string name, string @base)
        {
            Name = name;
            Base = @base;
        }
    }

    // ============================================
    // AbstractNode要素
    // ============================================
    public readonly struct AbstractNodeMeta
    {
        public readonly string Name;
        public readonly string Base;
        public readonly FieldUnit[] Fields;
        public readonly string TypeComment;

        public AbstractNodeMeta(string name, string @base, FieldUnit[] fields, string typeComment)
        {
            Name = name;
            Base = @base;
            Fields = fields;
            TypeComment = typeComment;
        }
    }

    // ============================================
    // Node要素
    // ============================================
    public readonly struct NodeMeta
    {
        public readonly string Name;
        public readonly string Base;
        public readonly string[] Kinds;
        public readonly FieldUnit[] Fields;
        public readonly string TypeComment;
        public readonly string FactoryComment;
        public readonly bool SkipConvenienceFactories;
        public readonly string Guid;

        public NodeMeta(
            string name,
            string @base,
            string[] kinds,
            FieldUnit[] fields,
            string typeComment,
            string factoryComment,
            bool skipConvenienceFactories,
            string guid = null)
        {
            Name = name;
            Base = @base;
            Kinds = kinds;
            Fields = fields;
            TypeComment = typeComment;
            FactoryComment = factoryComment;
            SkipConvenienceFactories = skipConvenienceFactories;
            Guid = guid;
        }
    }

    // ============================================
    // FieldUnit: Field/Choice/Sequenceの統合型
    // ============================================
    public readonly struct FieldUnit
    {
        public readonly FieldUnitType Type;
        public readonly FieldMetadata Data;      // 構造体として直接保持
        public readonly FieldUnit[] Children;
        public readonly int ChoiceIndex; // Choiceの中でどれが選ばれたかを示すインデックス

        public FieldUnit(FieldUnitType type, FieldMetadata data, FieldUnit[] children, int choiceIndex = 0)
        {
            Type = type;
            Data = data;
            Children = children;
            ChoiceIndex = choiceIndex;
        }

        public static FieldUnit CreateField(
            string name,
            string fieldType,
            bool optional = false,
            bool @override = false,
            int minCount = 0,
            bool allowTrailingSeparator = false,
            string[] kinds = null,
            string propertyComment = null)
        {
            var data = new FieldMetadata(
                name, fieldType, optional, @override,
                minCount, allowTrailingSeparator, kinds, propertyComment);
            return new FieldUnit(FieldUnitType.Single, data, null);
        }

        public static FieldUnit CreateChoice(FieldUnit[] children, bool optional = false)
        {
            // Choice自体がOptionalな場合も、Dataを使って情報を保持
            var data = new FieldMetadata(optional: optional);
            return new FieldUnit(FieldUnitType.Choice, data, children);
        }

        public static FieldUnit CreateSequence(FieldUnit[] children)
        {
            return new FieldUnit(FieldUnitType.Sequence, default, children);
        }
    }

    public enum FieldUnitType
    {
        Single,
        Choice,
        Sequence
    }

    // ============================================
    // FieldMetadata: 構造体化による最適化
    // ============================================
    public readonly struct FieldMetadata
    {
        public readonly string Name;
        public readonly string FieldType;
        public readonly bool Optional;
        public readonly bool Override;
        public readonly int MinCount;
        public readonly bool AllowTrailingSeparator;
        public readonly string[] Kinds;
        public readonly string PropertyComment;

        // これを追加することでただのメタデータから コードを書けるModelになる...うひょー最高!
        public readonly string Value; // 追加のフィールドが必要な場合はここに追加

        public FieldMetadata(
            string name = null,
            string fieldType = null,
            bool optional = false,
            bool @override = false,
            int minCount = 0,
            bool allowTrailingSeparator = false,
            string[] kinds = null,
            string propertyComment = null,
            string value = null)
        {
            Name = name;
            FieldType = fieldType;
            Optional = optional;
            Override = @override;
            MinCount = minCount;
            AllowTrailingSeparator = allowTrailingSeparator;
            Kinds = kinds ?? Array.Empty<string>();
            PropertyComment = propertyComment;
            Value = value;
        }
    }

    public static class NodeMetaExtensions
    {
        // 引数に newIndex を追加（デフォルト -1 は「更新しない」の意味）
        public static NodeMeta UpdateValue(this NodeMeta meta, string targetName, string newValue, int newIndex = -1)
        {
            if (meta.Fields == null) return meta;

            var newFields = new FieldUnit[meta.Fields.Length];
            for (int i = 0; i < meta.Fields.Length; i++)
            {
                newFields[i] = UpdateRecursive(meta.Fields[i], targetName, newValue, newIndex);
            }

            return new NodeMeta(
                meta.Name, meta.Base, meta.Kinds, newFields,
                meta.TypeComment, meta.FactoryComment, meta.SkipConvenienceFactories,
                meta.Guid
            );
        }

        private static FieldUnit UpdateRecursive(FieldUnit unit, string targetName, string newValue, int newIndex)
        {
            // 1. Choice自体のインデックスを更新する場合
            // ※ ChoiceのData.Nameがターゲットと一致した時だけ！
            if (unit.Type == FieldUnitType.Choice && unit.Data.Name == targetName)
            {
                // Indexが指定されていたら（-1じゃなければ）更新、そうでなければ今のを維持
                int nextIndex = (newIndex != -1) ? newIndex : unit.ChoiceIndex;
                return new FieldUnit(unit.Type, unit.Data, unit.Children, nextIndex);
            }

            // 2. SingleのValueを更新する場合（前と同じ）
            if (unit.Type == FieldUnitType.Single && unit.Data.Name == targetName)
            {
                var newData = new FieldMetadata(
                    unit.Data.Name, unit.Data.FieldType, unit.Data.Optional,
                    unit.Data.Override, unit.Data.MinCount, unit.Data.AllowTrailingSeparator,
                    unit.Data.Kinds, unit.Data.PropertyComment,
                    newValue
                );
                return new FieldUnit(unit.Type, newData, unit.Children, unit.ChoiceIndex);
            }

            // 3. 再帰処理（Childrenの中も同じルールで更新）
            if (unit.Children != null && unit.Children.Length > 0)
            {
                var newChildren = new FieldUnit[unit.Children.Length];
                for (int i = 0; i < unit.Children.Length; i++)
                {
                    newChildren[i] = UpdateRecursive(unit.Children[i], targetName, newValue, newIndex);
                }
                return new FieldUnit(unit.Type, unit.Data, newChildren, unit.ChoiceIndex);
            }

            return unit;
        }
    }
}
