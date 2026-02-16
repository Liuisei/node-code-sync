using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

// Path: NodeCodeSync/Editor/ASTEditor/Editor/Views/AstGraphView.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// Represents the main GraphView for visualizing and editing the Abstract Syntax Tree (AST).
    /// Provides functionality for converting between C# code and visual node structures.
    /// </summary>
    public class AstGraphView : GraphView
    {
        /// <summary>
        /// Occurs when the graph structure or node data is modified.
        /// </summary>
        public event Action OnGraphDataChanged;

        public AstGraphView()
        {
            style.flexGrow = 1;

            // Initialize standard manipulators for navigation and interaction
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // Add grid background for visual clarity
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // Subscribe to internal change events to sync with the EventBus
            OnGraphDataChanged += () =>
            {
                SyncGraphToCode();
            };

            // Listen for external code updates to rebuild the graph
            NodeCodeDataEventBus.Instance.OnCodeUpdated += OnCodeUpdated;
        }

        /// <summary>
        /// Rebuilds the entire graph view based on the provided C# source code.
        /// </summary>
        /// <param name="code">The C# source code to visualize.</param>
        public void OnCodeUpdated(string code)
        {
            // Clear existing elements
            DeleteElements(graphElements.ToList());

            var root = CodeToNodeConverter.CsharpToConvertedTree(code);
            if (root == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("[AstGraphView] Rebuilding graph from code...");
            sb.AppendLine($"Root Node: {root.Self.Name}");

            // Map to keep track of created nodes by their GUID for edge connection
            var guidMap = new Dictionary<string, AstNode>();

            // Pass 1: Create nodes using Depth-First Search (DFS)
            CreateNodesRecursive(root, guidMap, Vector2.zero, 0);
            sb.AppendLine($"Pass 1: {guidMap.Count} nodes created.");

            // Pass 2: Connect nodes based on field relationships
            ConnectEdgesRecursive(root, guidMap, sb);
            sb.AppendLine("[AstGraphView] Graph rebuild complete.");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Recursively creates AstNode instances from converted node data.
        /// </summary>
        private void CreateNodesRecursive(ConvertedNode converted, Dictionary<string, AstNode> guidMap, Vector2 basePos, int depth)
        {
            var node = new AstNode(converted.Self);

            // Basic auto-layout logic (can be replaced by a more sophisticated layout engine)
            var pos = basePos + new Vector2(depth * 350, guidMap.Count * 120);
            node.SetPosition(new Rect(pos, new Vector2(300, 200)));
            AddElement(node);

            guidMap[converted.Self.Guid] = node;

            // Handle data changes within the node
            Action changeHandler = () => OnGraphDataChanged?.Invoke();
            node.OnNodeDataChanged += changeHandler;

            // Ensure cleanup when node is removed
            node.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                node.OnNodeDataChanged -= changeHandler;
            });

            // Process children recursively
            if (converted.FieldChildren != null)
            {
                foreach (var kvp in converted.FieldChildren)
                {
                    foreach (var child in kvp.Value)
                    {
                        CreateNodesRecursive(child, guidMap, basePos, depth + 1);
                    }
                }
            }
        }

        /// <summary>
        /// Recursively establishes edges between parent ports and child nodes.
        /// </summary>
        private void ConnectEdgesRecursive(ConvertedNode converted, Dictionary<string, AstNode> guidMap, StringBuilder sb)
        {
            if (converted.FieldChildren == null) return;

            if (!guidMap.TryGetValue(converted.Self.Guid, out var parentNode))
            {
                sb.AppendLine($"[Warning] Parent node not found in map: {converted.Self.Name} ({converted.Self.Guid.Substring(0, 8)})");
                return;
            }

            foreach (var kvp in converted.FieldChildren)
            {
                var fieldName = kvp.Key;
                var outputPort = parentNode.GetOutputPort(fieldName);

                if (outputPort == null)
                {
                    sb.AppendLine($"[Warning] Output port not found: {converted.Self.Name}.{fieldName}");
                    continue;
                }

                foreach (var child in kvp.Value)
                {
                    if (!guidMap.TryGetValue(child.Self.Guid, out var childNode))
                    {
                        sb.AppendLine($"[Warning] Child node not found in map: {child.Self.Name}");
                        continue;
                    }

                    // Create and add the visual edge
                    var edge = outputPort.ConnectTo(childNode.InputPort);
                    AddElement(edge);
                    sb.AppendLine($"Edge Created: {converted.Self.Name}.{fieldName} -> {child.Self.Name}");
                }
            }

            // Recurse for each child
            foreach (var kvp in converted.FieldChildren)
            {
                foreach (var child in kvp.Value)
                {
                    ConnectEdgesRecursive(child, guidMap, sb);
                }
            }
        }

        /// <summary>
        /// Identifies nodes that do not have any incoming connections.
        /// </summary>
        /// <returns>An array of root AstNodes.</returns>
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

        /// <summary>
        /// Serializes the current graph state back into C# code and broadcasts it via the EventBus.
        /// </summary>
        public void SyncGraphToCode()
        {
            AstNode[] roots = GetRootNodes();
            string code = NodeToCodeConverter.NodeMetasToCSharp(roots, this, true);
            NodeCodeDataEventBus.Instance.UpdateNode(code);
        }

        // =========================================================
        // Port Compatibility
        // =========================================================

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            ports.ForEach(port =>
            {
                // Validation: Cannot connect to self, same node, or same direction
                if (port == startPort) return;
                if (port.node == startPort.node) return;
                if (port.direction == startPort.direction) return;

                compatible.Add(port);
            });
            return compatible;
        }

        // =========================================================
        // Context Menu
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

        /// <summary>
        /// Manually adds a new AstNode to the graph at the specified position.
        /// </summary>
        private void AddNode(Vector2 position)
        {
            var node = new AstNode();
            node.SetPosition(new Rect(position, new Vector2(300, 200)));
            AddElement(node);

            Action changeHandler = () => OnGraphDataChanged?.Invoke();
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