using UnityEngine.UIElements;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Editor/Views/GraphVE.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// AstGraphView をラップする VisualElement
    /// MainCenterWindow からはこの Root を使う
    /// </summary>
    public class GraphVE
    {
        public AstGraphView GraphView { get; }
        public VisualElement Root { get; }

        public GraphVE()
        {
            Root = new VisualElement();
            Root.style.flexGrow = 1;

            // ヘッダー
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 8;
            header.style.paddingRight = 8;
            header.style.paddingTop = 4;
            header.style.paddingBottom = 4;

            var title = new Label("AST Graph");
            header.Add(title);
            Root.Add(header);

            // GraphView
            GraphView = new AstGraphView();
            Root.Add(GraphView);
        }
    }
}
