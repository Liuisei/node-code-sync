using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Text;
using UnityEngine.UIElements;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Editor/Views/AstTreeView.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    public class AstTreeView : IDisposable
    {
        private VisualElement _root;
        private ScrollView _scrollView;
        private Label _titleLabel;
        private Label _treeLabel;

        public VisualElement Root { get => _root; }

        public AstTreeView()
        {
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

            _titleLabel = new Label("AST Tree")
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

            _treeLabel = new Label("(empty)")
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

            // イベント購読
            NodeCodeDataEventBus.Instance.OnCodeCompilationUnitSyntaxUpdated += OnCompilationUnitUpdated;
        }

        private void OnCompilationUnitUpdated(CompilationUnitSyntax compilationUnit)
        {
            if (compilationUnit == null)
            {
                _treeLabel.text = "(empty)";
                return;
            }

            var sb = new StringBuilder();
            BuildTreeRecursive(sb, compilationUnit, 0);
            _treeLabel.text = sb.ToString();
        }

        private void BuildTreeRecursive(StringBuilder sb, SyntaxNode node, int depth)
        {
            var indent = new string(' ', depth * 2);
            var kind = node.Kind();

            sb.AppendLine($"{indent}{kind}");

            foreach (SyntaxToken token in node.ChildTokens())
            {
                if (token.IsKind(SyntaxKind.None) || string.IsNullOrWhiteSpace(token.Text))
                    continue;

                // トークン前のTrivia（コメント）
                foreach (var trivia in token.LeadingTrivia)
                {
                    if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                        || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
                        || trivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia))
                    {
                        sb.AppendLine($"{indent}{trivia.ToString().Trim()}");
                    }
                }

                sb.AppendLine($"{indent}{token.Kind()} = \"{token.Text}\"");
            }

            foreach (var child in node.ChildNodes())
            {
                BuildTreeRecursive(sb, child, depth + 1);
            }
        }

        public void Dispose()
        {
            if (NodeCodeDataEventBus.Instance != null)
            {
                NodeCodeDataEventBus.Instance.OnCodeCompilationUnitSyntaxUpdated -= OnCompilationUnitUpdated;
            }
        }
    }
}