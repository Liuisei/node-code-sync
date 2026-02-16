using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Editor/Views/AstGraphView.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// AST ノードグラフの本体
    /// ノード追加・ポート互換性を管理する最低限の実装
    /// </summary>
    public class AstGraphView : GraphView
    {
        public event Action OnGraphDataChanged;

        public AstGraphView()
        {
            style.flexGrow = 1;

            // マニピュレータ
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // グリッド背景
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            OnGraphDataChanged += () =>
            {
                NodeDataStartBus();
            };

            NodeCodeDataEventBus.Instance.OnCodeUpdated += OnCodeUpdated;
        }

        public void OnCodeUpdated(string code)
        {
            DeleteElements(graphElements.ToList());

            var root = CodeToNodeConverter.CsharpToConvertedTree(code);
            if (root == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("[SB] === GraphView Start ===");
            sb.AppendLine($"Root: {root.Self.Name}");

            // Guid → AstNode 辞書
            var guidMap = new Dictionary<string, AstNode>();

            // 1パス目: DFSでAstNode生成
            CreateNodesRecursive(root, guidMap, Vector2.zero, 0);
            sb.AppendLine($"Pass1: {guidMap.Count} nodes created");

            // 2パス目: FieldChildrenでEdge接続
            ConnectEdgesRecursive(root, guidMap, sb);
            sb.AppendLine("[SB] === GraphView End ===");

            Debug.Log(sb.ToString());
        }

        void CreateNodesRecursive(ConvertedNode converted, Dictionary<string, AstNode> guidMap, Vector2 basePos, int depth)
        {
            var node = new AstNode(converted.Self);
            var pos = basePos + new Vector2(depth * 350, guidMap.Count * 120);
            node.SetPosition(new Rect(pos, new Vector2(300, 200)));
            AddElement(node);

            guidMap[converted.Self.Guid] = node;

            void changeHandler()
            {
                OnGraphDataChanged?.Invoke();
            }
            node.OnNodeDataChanged += changeHandler;

            // 子を再帰
            if (converted.FieldChildren != null)
            {
                foreach (var kvp in converted.FieldChildren)
                {
                    foreach (var child in kvp.Value)
                        CreateNodesRecursive(child, guidMap, basePos, depth + 1);
                }
            }
        }

        void ConnectEdgesRecursive(ConvertedNode converted, Dictionary<string, AstNode> guidMap, StringBuilder sb)
        {
            if (converted.FieldChildren == null) return;

            if (!guidMap.TryGetValue(converted.Self.Guid, out var parentNode))
            {
                sb.AppendLine($"⚠ parent not found: {converted.Self.Name} ({converted.Self.Guid[..8]})");
                return;
            }

            foreach (var kvp in converted.FieldChildren)
            {
                var fieldName = kvp.Key;
                var outputPort = parentNode.GetOutputPort(fieldName);
                if (outputPort == null)
                {
                    sb.AppendLine($"⚠ port not found: {converted.Self.Name}.{fieldName}");
                    continue;
                }

                foreach (var child in kvp.Value)
                {
                    if (!guidMap.TryGetValue(child.Self.Guid, out var childNode))
                    {
                        sb.AppendLine($"⚠ child not found: {child.Self.Name} ({child.Self.Guid[..8]})");
                        continue;
                    }

                    var edge = outputPort.ConnectTo(childNode.InputPort);
                    AddElement(edge);
                    sb.AppendLine($"Edge: {converted.Self.Name}.{fieldName} → {child.Self.Name}");
                }
            }

            // 子も再帰
            foreach (var kvp in converted.FieldChildren)
            {
                foreach (var child in kvp.Value)
                    ConnectEdgesRecursive(child, guidMap, sb);
            }
        }



        // AstGraphView内
        public AstNode[] GetRootNodes()
        {
            var connectedInputs = new HashSet<AstNode>();
            foreach (var edge in edges.ToList())
            {
                if (edge.input.node is AstNode child)
                    connectedInputs.Add(child);
            }

            return nodes
                .OfType<AstNode>()
                .Where(n => !connectedInputs.Contains(n))
                .ToArray();
        }
        public void NodeDataStartBus()
        {
            AstNode[] roots = GetRootNodes();
            string code = NodeToCodeConverter.NodeMetasToCSharp(roots, this, true);
            NodeCodeDataEventBus.Instance.UpdateNode(code);
        }

        // =========================================================
        // ポート互換性
        // =========================================================

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            ports.ForEach(port =>
            {
                if (port == startPort) return;
                if (port.node == startPort.node) return;
                if (port.direction == startPort.direction) return;
                compatible.Add(port);
            });
            return compatible;
        }

        // =========================================================
        // コンテキストメニュー
        // =========================================================

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            evt.menu.AppendSeparator();

            evt.menu.AppendAction(
                "Add AST Node",
                action => AddNode(action.eventInfo.localMousePosition)
            );
        }

        void AddNode(Vector2 position)
        {
            var node = new AstNode();
            node.SetPosition(new Rect(position, new Vector2(300, 200)));
            AddElement(node);

            void changeHandler()
            {
                OnGraphDataChanged?.Invoke();
            }

            node.OnNodeDataChanged += changeHandler;


            node.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                node.OnNodeDataChanged -= changeHandler;
                OnGraphDataChanged?.Invoke();
            });

            OnGraphDataChanged?.Invoke();
        }

        
    }
}
