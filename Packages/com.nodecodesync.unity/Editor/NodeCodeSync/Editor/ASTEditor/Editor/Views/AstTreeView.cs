using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Text;
using UnityEngine.UIElements;

// Path: NodeCodeSync/Editor/ASTEditor/Editor/Views/AstTreeView.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// A visual component that renders a text-based tree representation of the Roslyn Abstract Syntax Tree (AST).
    /// Useful for debugging the structural hierarchy of the synchronized source code.
    /// </summary>
    public class AstTreeView : IDisposable
    {
        private VisualElement _root;
        private ScrollView _scrollView;
        private Label _titleLabel;
        private Label _treeLabel;

        /// <summary>
        /// Gets the root visual element of the AST tree view.
        /// </summary>
        public VisualElement Root { get => _root; }

        public AstTreeView()
        {
            // Container layout configuration
            _root = new VisualElement()
            {
                style =
                {
                    flexGrow = 1,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 4,
                    paddingBottom = 4,
                    flexDirection = FlexDirection.Column
                }
            };

            _titleLabel = new Label("AST Structural Debugger")
            {
                style =
                {
                    unityFontStyleAndWeight = UnityEngine.FontStyle.Bold,
                    marginBottom = 4
                }
            };
            _root.Add(_titleLabel);

            _scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                style = { flexGrow = 1 }
            };

            // Text label for tree rendering (supports pre-formatted text and selection)
            _treeLabel = new Label("(waiting for syntax tree...)")
            {
                style =
                {
                    whiteSpace = WhiteSpace.Pre,
                    fontSize = 11,
                },
                selection =
                {
                    isSelectable = true
                }
            };

            _scrollView.Add(_treeLabel);
            _root.Add(_scrollView);

            // Subscribe to the global event bus to receive syntax updates
            NodeCodeDataEventBus.Instance.OnCodeCompilationUnitSyntaxUpdated += OnCompilationUnitUpdated;
        }

        /// <summary>
        /// Event handler triggered when the Roslyn Compilation Unit is modified.
        /// Re-renders the entire tree text.
        /// </summary>
        private void OnCompilationUnitUpdated(CompilationUnitSyntax compilationUnit)
        {
            if (compilationUnit == null)
            {
                _treeLabel.text = "(empty unit)";
                return;
            }

            var sb = new StringBuilder();
            BuildTreeRecursive(sb, compilationUnit, 0);
            _treeLabel.text = sb.ToString();
        }

        /// <summary>
        /// Recursively traverses the SyntaxNode hierarchy to generate an indented tree string.
        /// Also extracts Trivia (comments) and Child Tokens for complete visibility.
        /// </summary>
        /// <param name="sb">Target string builder.</param>
        /// <param name="node">Current syntax node to process.</param>
        /// <param name="depth">Current recursion depth for indentation.</param>
        private void BuildTreeRecursive(StringBuilder sb, SyntaxNode node, int depth)
        {
            var indent = new string(' ', depth * 2);
            var kind = node.Kind();

            sb.AppendLine($"{indent}{kind}");

            // Extract child tokens (keywords, identifiers, punctuations)
            foreach (SyntaxToken token in node.ChildTokens())
            {
                if (token.IsKind(SyntaxKind.None) || string.IsNullOrWhiteSpace(token.Text))
                    continue;

                // Process Leading Trivia (Comments) attached to the token
                foreach (var trivia in token.LeadingTrivia)
                {
                    if (IsCommentTrivia(trivia.Kind()))
                    {
                        sb.AppendLine($"{indent}  [Trivia] {trivia.ToString().Trim()}");
                    }
                }

                sb.AppendLine($"{indent}  {token.Kind()} = \"{token.Text}\"");
            }

            // Recurse into child nodes
            foreach (var child in node.ChildNodes())
            {
                BuildTreeRecursive(sb, child, depth + 1);
            }
        }

        /// <summary>
        /// Determines if the specific SyntaxKind represents a comment or documentation.
        /// </summary>
        private bool IsCommentTrivia(SyntaxKind kind)
        {
            return kind == SyntaxKind.SingleLineCommentTrivia
                || kind == SyntaxKind.MultiLineCommentTrivia
                || kind == SyntaxKind.SingleLineDocumentationCommentTrivia
                || kind == SyntaxKind.MultiLineDocumentationCommentTrivia
                || kind == SyntaxKind.DocumentationCommentExteriorTrivia;
        }

        /// <summary>
        /// Unsubscribes from events and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (NodeCodeDataEventBus.Instance != null)
            {
                NodeCodeDataEventBus.Instance.OnCodeCompilationUnitSyntaxUpdated -= OnCompilationUnitUpdated;
            }
        }
    }
}