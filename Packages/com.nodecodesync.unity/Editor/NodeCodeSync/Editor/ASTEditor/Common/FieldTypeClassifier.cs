// Path: NodeCodeSync/Editor/ASTEditor/Common/FieldTypeClassifier.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// Classifies field types used by Roslyn syntax nodes.
    /// This is primarily used to decide how a field should be represented in the UI
    /// (e.g., editable text vs. graph ports).
    /// </summary>
    public enum FieldTypeKind
    {
        /// <summary>Single token (SyntaxToken).</summary>
        Token,

        /// <summary>Token list (SyntaxList&lt;SyntaxToken&gt;).</summary>
        TokenList,

        /// <summary>Node list (SyntaxList&lt;T&gt; where T : SyntaxNode).</summary>
        NodeList,

        /// <summary>Separated node list (SeparatedSyntaxList&lt;T&gt;).</summary>
        SeparatedNodeList,

        /// <summary>Single syntax node type (e.g., *Syntax).</summary>
        SingleNode,

        /// <summary>Unknown or unsupported type.</summary>
        Unknown
    }

    /// <summary>
    /// Utility for mapping Roslyn type strings to <see cref="FieldTypeKind"/>.
    /// </summary>
    public static class FieldTypeClassifier
    {
        /// <summary>
        /// Classifies a Roslyn field type string into a <see cref="FieldTypeKind"/>.
        /// </summary>
        public static FieldTypeKind Classify(string fieldType)
        {
            if (string.IsNullOrEmpty(fieldType))
                return FieldTypeKind.Unknown;

            // Exact matches (highest priority).
            if (fieldType == "SyntaxToken")
                return FieldTypeKind.Token;

            if (fieldType == "SyntaxList<SyntaxToken>")
                return FieldTypeKind.TokenList;

            // Prefix matches (check SeparatedSyntaxList before SyntaxList).
            if (fieldType.StartsWith("SeparatedSyntaxList"))
                return FieldTypeKind.SeparatedNodeList;

            if (fieldType.StartsWith("SyntaxList"))
                return FieldTypeKind.NodeList;

            // Suffix matches.
            if (fieldType.EndsWith("Syntax"))
                return FieldTypeKind.SingleNode;

            return FieldTypeKind.Unknown;
        }

        /// <summary>
        /// Returns true if the kind is token-based (typically edited with a text field).
        /// </summary>
        public static bool IsTokenType(FieldTypeKind kind)
        {
            return kind == FieldTypeKind.Token || kind == FieldTypeKind.TokenList;
        }

        /// <summary>
        /// Returns true if the kind is node-based (typically connected via ports).
        /// </summary>
        public static bool IsNodeType(FieldTypeKind kind)
        {
            return kind == FieldTypeKind.SingleNode
                || kind == FieldTypeKind.NodeList
                || kind == FieldTypeKind.SeparatedNodeList;
        }

        /// <summary>
        /// Returns true if the kind represents a list type (typically rendered as multi-ports).
        /// </summary>
        public static bool IsListType(FieldTypeKind kind)
        {
            return kind == FieldTypeKind.NodeList
                || kind == FieldTypeKind.SeparatedNodeList;
        }

        /// <summary>
        /// Returns true if the list requires separators (e.g., comma-separated lists).
        /// </summary>
        public static bool NeedsSeparator(FieldTypeKind kind)
        {
            return kind == FieldTypeKind.SeparatedNodeList;
        }
    }
}