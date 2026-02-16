using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Editor/Windows/MainCenterWindow.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    public class MainCenterWindow : EditorWindow, IHasCustomMenu
    {
        GraphVE graphView;
        AstTreeView astView;
        SourceController sourceView;

        TwoPaneSplitView mainSplitView;
        TwoPaneSplitView debugSplitView;

        const string DEBUG_MODE_KEY = "ASTEditorDebugMode";
        bool isDebugMode = false;

        [MenuItem("NodeCodeSync/Open AST Editor")]
        public static void Open()
        {
            GetWindow<MainCenterWindow>("NodeCodeSync AST Editor");
        }

        void OnEnable()
        {
            // EditorPrefsから読み込み
            isDebugMode = EditorPrefs.GetBool(DEBUG_MODE_KEY, false);

            // インスタンス生成(一度だけ!)
            graphView = new GraphVE();
            sourceView = new SourceController();
            astView = new AstTreeView();

            // ルートコンテナ
            VisualElement root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            // メインコンテンツエリア
            mainSplitView = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            debugSplitView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Horizontal);

            root.Add(mainSplitView);
            BuildLayout();
        }

        void BuildLayout()
        {
            mainSplitView.Clear();

            if (isDebugMode)
            {
                debugSplitView.Clear();
                debugSplitView.Add(astView.Root);
                debugSplitView.Add(sourceView.Root);
                mainSplitView.Add(graphView.Root);
                mainSplitView.Add(debugSplitView);
                debugSplitView.fixedPaneInitialDimension = 200;
            }
            else
            {
                mainSplitView.Add(graphView.Root);
                mainSplitView.Add(sourceView.Root);
            }

            mainSplitView.fixedPaneInitialDimension = 1200;
        }

        // 右上メニューに追加
        void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Debug Mode"), isDebugMode, () =>
            {
                isDebugMode = !isDebugMode;
                EditorPrefs.SetBool(DEBUG_MODE_KEY, isDebugMode);
                BuildLayout();
            });
        }
        void OnDisable()
        {
            // イベント解除を確実に実行
            if (sourceView != null)
            {
                sourceView.Dispose();
            }

            // クリーンアップ処理
            graphView = null;
            astView = null;
            sourceView = null;
            mainSplitView = null;
            debugSplitView = null;
        }
    }
}