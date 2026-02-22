using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Path: NodeCodeSync/Editor/ASTEditor/Editor/Windows/MainCenterWindow.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// The main entry point for the NodeCodeSync AST Editor.
    /// This window integrates the Graph View, Source Control, and AST Debugger into a single 
    /// responsive layout using multi-pane split views.
    /// </summary>
    public class MainCenterWindow : EditorWindow, IHasCustomMenu
    {
        // View components
        private GraphVE graphView;
        private AstTreeView astView;
        private SourceController sourceView;

        // Layout components
        private TwoPaneSplitView mainSplitView;
        private TwoPaneSplitView debugSplitView;

        // Persistent settings
        private const string DEBUG_MODE_KEY = "ASTEditorDebugMode";
        private bool isDebugMode = false;

        /// <summary>
        /// Opens the AST Editor window from the Unity menu.
        /// </summary>
        [MenuItem("NodeCodeSync/Open AST Editor")]
        public static void Open()
        {
            var window = GetWindow<MainCenterWindow>("NodeCodeSync AST Editor");
            window.minSize = new Vector2(800, 450);
        }

        private void OnEnable()
        {
            // Load user preference for debug mode
            isDebugMode = EditorPrefs.GetBool(DEBUG_MODE_KEY, false);

            // Initialize view instances
            graphView = new GraphVE();
            sourceView = new SourceController();
            astView = new AstTreeView();

            // Set up the root visual element
            VisualElement root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            // Initialize split views for dynamic layout management
            mainSplitView = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            debugSplitView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Horizontal);

            root.Add(mainSplitView);

            // Construct the initial UI based on the saved debug mode state
            BuildLayout();
        }

        /// <summary>
        /// Reconstructs the window layout. 
        /// In Debug Mode, an additional pane for the AST structural tree is displayed.
        /// </summary>
        private void BuildLayout()
        {
            mainSplitView.Clear();

            if (isDebugMode)
            {
                // Layout: [ Graph Canvas ] | [ AST Tree | Source Preview ]
                debugSplitView.Clear();
                debugSplitView.Add(astView.Root);
                debugSplitView.Add(sourceView.Root);

                mainSplitView.Add(graphView.Root);
                mainSplitView.Add(debugSplitView);

                debugSplitView.fixedPaneInitialDimension = 200;
            }
            else
            {
                // Layout: [ Graph Canvas ] | [ Source Preview ]
                mainSplitView.Add(graphView.Root);
                mainSplitView.Add(sourceView.Root);
            }

            // Adjust the primary split ratio
            mainSplitView.fixedPaneInitialDimension = 1200;
        }

        /// <summary>
        /// Adds custom items to the window's "kebab" menu (top-right corner).
        /// Allows toggling Debug Mode without cluttering the main UI.
        /// </summary>
        void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Debug Mode"), isDebugMode, () =>
            {
                isDebugMode = !isDebugMode;
                EditorPrefs.SetBool(DEBUG_MODE_KEY, isDebugMode);
                BuildLayout();
            });
        }

        private void OnDisable()
        {
            // Critical: Ensure event unsubscriptions are handled to avoid editor leaks
            if (sourceView != null)
            {
                sourceView.Dispose();
            }

            if (astView != null)
            {
                astView.Dispose();
            }

            // Explicit cleanup of references
            graphView = null;
            astView = null;
            sourceView = null;
            mainSplitView = null;
            debugSplitView = null;
        }
    }
}