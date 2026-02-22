using System;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Schema/SyntaxMetaModel.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// The root structure representing the entire Roslyn syntax hierarchy.
    /// Acts as the "World Map" for all possible code structures.
    /// </summary>
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

    /// <summary>
    /// Metadata for built-in Roslyn types (e.g., SyntaxToken, SyntaxNode).
    /// </summary>
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

    /// <summary>
    /// Metadata for base syntax classes (e.g., ExpressionSyntax, StatementSyntax).
    /// Defines shared properties inherited by multiple concrete nodes.
    /// </summary>
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

    /// <summary>
    /// Represents a concrete SyntaxNode (e.g., ClassDeclaration, InvocationExpression).
    /// This is the primary template for creating Graph Nodes.
    /// </summary>
    public readonly struct NodeMeta
    {
        public readonly string Name;
        public readonly string Base;
        public readonly string[] Kinds;
        public readonly FieldUnit[] Fields;
        public readonly string TypeComment;
        public readonly string FactoryComment;
        public readonly bool SkipConvenienceFactories;
        public readonly string Guid; // Unique identifier for the instance in the graph.

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

    /// <summary>
    /// A unified structural unit that can represent a single Field, a Choice, or a Sequence.
    /// Supports recursive composition to mirror the complexity of C# syntax.
    /// </summary>
    public readonly struct FieldUnit
    {
        public readonly FieldUnitType Type;
        public readonly FieldMetadata Data;      // Stores metadata and actual code values.
        public readonly FieldUnit[] Children;
        public readonly int ChoiceIndex; // Index of the selected option in a Choice unit.

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

    /// <summary>
    /// Holds both the schema definition (Name, Type) and the dynamic state (Value).
    /// This optimization allows the model to act as a direct data source for code generation.
    /// </summary>
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

        /// <summary>
        /// The actual content/value of this field (e.g., variable name, literal string).
        /// Presence of this field transforms this from "Schema" to "Instance Data".
        /// </summary>
        public readonly string Value;

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

    /// <summary>
    /// Utility for creating new, immutable instances of metadata with updated values.
    /// Ensures that state changes are handled safely via functional updates.
    /// </summary>
    public static class NodeMetaExtensions
    {
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
            // Case 1: Updating the selected index of a Choice unit.
            if (unit.Type == FieldUnitType.Choice && unit.Data.Name == targetName)
            {
                int nextIndex = (newIndex != -1) ? newIndex : unit.ChoiceIndex;
                return new FieldUnit(unit.Type, unit.Data, unit.Children, nextIndex);
            }

            // Case 2: Updating the Value of a Single field unit.
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

            // Case 3: Recursively searching within nested Children.
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