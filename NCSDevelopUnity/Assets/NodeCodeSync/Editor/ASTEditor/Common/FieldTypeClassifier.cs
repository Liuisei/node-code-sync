// Path: Assets/NodeCodeSync/Editor/ASTEditor/Common/FieldTypeClassifier.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// フィールドの型分類
    /// </summary>
    public enum FieldTypeKind
    {
        /// <summary>単一トークン（SyntaxToken）</summary>
        Token,

        /// <summary>トークンリスト（SyntaxList&lt;SyntaxToken&gt;）</summary>
        TokenList,

        /// <summary>ノードリスト（SyntaxList&lt;T&gt; where T : SyntaxNode）</summary>
        NodeList,

        /// <summary>区切り付きノードリスト（SeparatedSyntaxList&lt;T&gt;）</summary>
        SeparatedNodeList,

        /// <summary>単一ノード（xxxSyntax）</summary>
        SingleNode,

        /// <summary>不明な型</summary>
        Unknown
    }

    /// <summary>
    /// フィールド型文字列から FieldTypeKind を判定するユーティリティ
    /// </summary>
    public static class FieldTypeClassifier
    {
        /// <summary>
        /// 型文字列を分類する
        /// </summary>
        public static FieldTypeKind Classify(string fieldType)
        {
            if (string.IsNullOrEmpty(fieldType))
                return FieldTypeKind.Unknown;

            // 完全一致チェック（優先度高）
            if (fieldType == "SyntaxToken")
                return FieldTypeKind.Token;

            if (fieldType == "SyntaxList<SyntaxToken>")
                return FieldTypeKind.TokenList;

            // 前方一致チェック（SeparatedSyntaxList を先にチェック）
            if (fieldType.StartsWith("SeparatedSyntaxList"))
                return FieldTypeKind.SeparatedNodeList;

            if (fieldType.StartsWith("SyntaxList"))
                return FieldTypeKind.NodeList;

            // 後方一致チェック
            if (fieldType.EndsWith("Syntax"))
                return FieldTypeKind.SingleNode;

            return FieldTypeKind.Unknown;
        }

        /// <summary>
        /// トークン系か（TextField で編集する型）
        /// </summary>
        public static bool IsTokenType(FieldTypeKind kind)
        {
            return kind == FieldTypeKind.Token || kind == FieldTypeKind.TokenList;
        }

        /// <summary>
        /// ノード系か（Port で接続する型）
        /// </summary>
        public static bool IsNodeType(FieldTypeKind kind)
        {
            return kind == FieldTypeKind.SingleNode
                || kind == FieldTypeKind.NodeList
                || kind == FieldTypeKind.SeparatedNodeList;
        }

        /// <summary>
        /// リスト系か（Multi Port）
        /// </summary>
        public static bool IsListType(FieldTypeKind kind)
        {
            return kind == FieldTypeKind.NodeList
                || kind == FieldTypeKind.SeparatedNodeList;
        }

        /// <summary>
        /// 区切り文字が必要か
        /// </summary>
        public static bool NeedsSeparator(FieldTypeKind kind)
        {
            return kind == FieldTypeKind.SeparatedNodeList;
        }
    }
}
