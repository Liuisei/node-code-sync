using UnityEngine.UIElements;

// Path: NodeCodeSync/Editor/ASTEditor/Editor/Views/GraphVE.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// A wrapper VisualElement that encapsulates the <see cref="AstGraphView"/>.
    /// This class provides a structured root container, including headers and layout management,
    /// intended to be used as a primary component within the MainCenterWindow.
    /// </summary>
    public class GraphVE
    {
        /// <summary>
        /// Gets the underlying GraphView instance responsible for node editing.
        /// </summary>
        public AstGraphView GraphView { get; }

        /// <summary>
        /// Gets the root visual element of this view, which contains the header and the graph.
        /// </summary>
        public VisualElement Root { get; }

        public GraphVE()
        {
            // Root container initialization with flexible growth
            Root = new VisualElement();
            Root.style.flexGrow = 1;

            // Header Section: Displays titles and provides space for future toolbars
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 8;
            header.style.paddingRight = 8;
            header.style.paddingTop = 4;
            header.style.paddingBottom = 4;

            var title = new Label("AST Graph Canvas")
            {
                style =
                {
                    unityFontStyleAndWeight = UnityEngine.FontStyle.Bold
                }
            };
            header.Add(title);
            Root.Add(header);

            // GraphView Integration: Fills the remaining space below the header
            GraphView = new AstGraphView();
            Root.Add(GraphView);
        }
    }
}