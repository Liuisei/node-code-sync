using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

// Path: NodeCodeSync/Editor/ASTEditor/Editor/Nodes/AstNode.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// Represents a visual node in the GraphView that corresponds to a Roslyn Abstract Syntax Tree (AST) node.
    /// This node dynamically generates its UI and ports based on the <see cref="NodeMeta"/> schema.
    /// </summary>
    public class AstNode : Node
    {
        // UI Containers
        private VisualElement fieldContainer;
        private TextField filterField;
        private PopupField<string> namePopup;

        // Data & Cache
        private List<string> allNodeNames;
        private NodeMeta? runtimeMeta;

        // Ports
        private Port inputPort;
        private readonly Dictionary<string, Port> outputPorts = new();

        /// <summary>
        /// Gets the main input port for this AST node.
        /// </summary>
        public Port InputPort => inputPort;

        /// <summary>
        /// Gets the current runtime metadata state of this node.
        /// </summary>
        public NodeMeta? RuntimeMeta => runtimeMeta;

        /// <summary>
        /// Retrieves an output port associated with a specific field name.
        /// </summary>
        public Port GetOutputPort(string fieldName)
            => outputPorts.TryGetValue(fieldName, out var p) ? p : null;

        /// <summary>
        /// Triggered when the node's internal data (values or structure) is modified.
        /// </summary>
        public event Action OnNodeDataChanged;

        /// <summary>
        /// Triggered when the node is being disposed or removed from the graph.
        /// </summary>
        public event Action OnDisposed;

        // =========================================================
        // Constructors
        // =========================================================

        public AstNode() : this(null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AstNode"/> class.
        /// </summary>
        /// <param name="nodeMeta">Optional initial metadata. If null, defaults to the first available node type.</param>
        public AstNode(NodeMeta? nodeMeta)
        {
            title = "AST Node";

            // Initialize Input Port (Common to all nodes)
            inputPort = InstantiatePort(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Single,
                typeof(bool)
            );
            inputPort.portName = "In";
            inputContainer.Add(inputPort);

            // Setup Schema Cache and Filtering
            var cache = RoslynSchemaCache.Instance;
            allNodeNames = cache.NodeNameOderByNameList.ToList();

            // Filter Field Initialization
            filterField = new TextField("Filter")
            {
                style = { marginBottom = 2 }
            };
            filterField.RegisterValueChangedCallback(evt => OnFilterChanged(evt.newValue));
            mainContainer.Add(filterField);

            // Node Selection Popup Initialization
            namePopup = new PopupField<string>(
                "Node",
                allNodeNames,
                allNodeNames[0]
            );
            namePopup.RegisterValueChangedCallback(evt =>
            {
                OnNodeNameSelected(evt.newValue);
                OnNodeDataChanged?.Invoke();
            });
            mainContainer.Add(namePopup);

            // Dynamic UI Container
            fieldContainer = new VisualElement();
            fieldContainer.style.marginTop = 4;
            mainContainer.Add(fieldContainer);

            // Logic: Determine whether to use provided meta or default to schema start
            if (nodeMeta.HasValue)
            {
                runtimeMeta = nodeMeta.Value;
                if (allNodeNames.Contains(nodeMeta.Value.Name))
                {
                    namePopup.SetValueWithoutNotify(nodeMeta.Value.Name);
                }
                title = nodeMeta.Value.Name;
            }
            else
            {
                runtimeMeta = cache.NodeMetaMap.TryGetValue(allNodeNames[0], out var defaultMeta)
                    ? defaultMeta
                    : (NodeMeta?)null;
            }

            RefreshUI();
        }

        // =========================================================
        // UI Interaction & Refresh Logic
        // =========================================================

        /// <summary>
        /// Updates the node selection popup based on the filter string.
        /// </summary>
        private void OnFilterChanged(string filter)
        {
            List<string> filtered = string.IsNullOrEmpty(filter)
                ? allNodeNames
                : allNodeNames
                    .Where(n => n.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

            if (filtered.Count == 0) return;

            string currentValue = namePopup.value;
            if (!filtered.Contains(currentValue))
                filtered.Insert(0, currentValue);

            // Re-inject the PopupField to refresh the list (GraphView UI workaround)
            int idx = mainContainer.IndexOf(namePopup);
            mainContainer.Remove(namePopup);

            namePopup = new PopupField<string>("Node", filtered, currentValue);
            namePopup.RegisterValueChangedCallback(evt => OnNodeNameSelected(evt.newValue));

            mainContainer.Insert(idx, namePopup);
        }
        // =========================================================
        // Node Selection & UI Reconstruction
        // =========================================================

        /// <summary>
        /// Handles the selection of a new node type from the popup.
        /// Resets the runtime metadata and triggers a full UI rebuild.
        /// </summary>
        private void OnNodeNameSelected(string name)
        {
            var cache = RoslynSchemaCache.Instance;
            if (!cache.NodeMetaMap.TryGetValue(name, out var meta))
            {
                runtimeMeta = null;
                fieldContainer.Clear();
                ClearDynamicPorts();
                fieldContainer.Add(new Label($"Metadata not found: {name}"));
                RefreshPorts();
                return;
            }

            // Initialize new RuntimeMeta from the schema template
            runtimeMeta = meta;
            title = name;

            RefreshUI();
            OnNodeDataChanged?.Invoke();
        }

        /// <summary>
        /// Clears and reconstructs the entire node UI (fields and ports) 
        /// based on the current <see cref="runtimeMeta"/>.
        /// </summary>
        private void RefreshUI()
        {
            fieldContainer.Clear();
            ClearDynamicPorts();

            if (runtimeMeta == null || !runtimeMeta.HasValue)
            {
                fieldContainer.Add(new Label("(No metadata assigned)"));
                RefreshPorts();
                return;
            }

            // Recursively build UI elements from the FieldUnit hierarchy
            BuildUI(runtimeMeta.Value.Fields);

            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary>
        /// Iterates through the top-level field units to build the UI.
        /// </summary>
        private void BuildUI(FieldUnit[] fields)
        {
            if (fields == null) return;

            foreach (var field in fields)
            {
                BuildFieldUnit(field);
            }
        }

        /// <summary>
        /// Dispatches the UI construction based on the <see cref="FieldUnitType"/>.
        /// </summary>
        private void BuildFieldUnit(FieldUnit unit)
        {
            switch (unit.Type)
            {
                case FieldUnitType.Single:
                    BuildSingleField(unit.Data);
                    break;

                case FieldUnitType.Choice:
                    BuildChoiceField(unit);
                    break;

                case FieldUnitType.Sequence:
                    if (unit.Children != null)
                    {
                        foreach (var child in unit.Children)
                            BuildFieldUnit(child);
                    }
                    break;
            }
        }

        // =========================================================
        // Field Specific Generators
        // =========================================================

        /// <summary>
        /// Generates a TextField for Tokens or an Output Port for Nodes.
        /// </summary>
        private void BuildSingleField(FieldMetadata data)
        {
            if (string.IsNullOrEmpty(data.Name) || string.IsNullOrEmpty(data.FieldType))
                return;

            var kind = FieldTypeClassifier.Classify(data.FieldType);
            var label = data.Optional ? $"{data.Name} (Optional)" : data.Name;

            // Branch: Token types are handled as editable TextFields
            if (FieldTypeClassifier.IsTokenType(kind))
            {
                var tf = new TextField(label)
                {
                    value = data.Value ?? ""
                };
                tf.style.marginBottom = 2;

                if (data.Optional)
                    tf.style.backgroundColor = new Color(0.2f, 0.2f, 0.25f);

                // Update runtime metadata on value change
                var fieldName = data.Name;
                tf.RegisterValueChangedCallback(evt =>
                {
                    if (runtimeMeta.HasValue)
                    {
                        runtimeMeta = runtimeMeta.Value.UpdateValue(fieldName, evt.newValue);
                        OnNodeDataChanged?.Invoke();
                    }
                });

                fieldContainer.Add(tf);
                return;
            }

            // Branch: Node types are handled as dynamic Output Ports
            if (FieldTypeClassifier.IsNodeType(kind))
            {
                Port.Capacity capacity = FieldTypeClassifier.IsListType(kind)
                    ? Port.Capacity.Multi
                    : Port.Capacity.Single;

                Port port = InstantiatePort(
                    Orientation.Horizontal,
                    Direction.Output,
                    capacity,
                    typeof(bool)
                );
                port.portName = data.Name;

                if (data.Optional)
                {
                    port.portColor = new Color(0.6f, 0.6f, 0.8f);
                    port.tooltip = $"{data.Name} (Optional connection)";
                }

                outputContainer.Add(port);
                outputPorts[data.Name] = port;
                return;
            }
        }

        /// <summary>
        /// Generates a PopupField for choice-based fields and recursively 
        /// builds the UI for the currently selected branch.
        /// </summary>
        private void BuildChoiceField(FieldUnit choice)
        {
            if (choice.Children == null || choice.Children.Length == 0)
                return;

            var optionNames = new List<string>();
            for (int i = 0; i < choice.Children.Length; i++)
            {
                optionNames.Add(GetFieldUnitLabel(choice.Children[i], i));
            }

            var currentIndex = Mathf.Clamp(choice.ChoiceIndex, 0, optionNames.Count - 1);
            var popup = new PopupField<string>("Choice", optionNames, optionNames[currentIndex]);

            popup.style.marginTop = 4;
            popup.style.marginBottom = 2;
            popup.style.backgroundColor = new Color(0.25f, 0.2f, 0.2f);

            var choiceName = choice.Data.Name;

            popup.RegisterValueChangedCallback(evt =>
            {
                var newIdx = optionNames.IndexOf(evt.newValue);
                if (newIdx < 0) return;

                if (runtimeMeta.HasValue)
                {
                    // Update choice index and trigger a full UI refresh to reflect structural changes
                    runtimeMeta = runtimeMeta.Value.UpdateValue(choiceName, null, newIdx);
                    RefreshUI();
                    OnNodeDataChanged?.Invoke();
                }
            });

            fieldContainer.Add(popup);

            // Render the UI for the selected branch only
            var selectedChild = choice.Children[currentIndex];
            BuildFieldUnit(selectedChild);
        }

        // =========================================================
        // Port & Utility Helpers
        // =========================================================

        private void ClearDynamicPorts()
        {
            outputContainer.Clear();
            outputPorts.Clear();
        }

        /// <summary>
        /// Generates a display label for a FieldUnit, used within selection popups.
        /// </summary>
        private string GetFieldUnitLabel(FieldUnit unit, int index)
        {
            switch (unit.Type)
            {
                case FieldUnitType.Single:
                    return !string.IsNullOrEmpty(unit.Data.Name)
                        ? unit.Data.Name
                        : $"Option {index + 1}";

                case FieldUnitType.Sequence:
                    if (unit.Children != null && unit.Children.Length > 0
                        && unit.Children[0].Type == FieldUnitType.Single
                        && !string.IsNullOrEmpty(unit.Children[0].Data.Name))
                    {
                        return $"Seq({unit.Children[0].Data.Name}...)";
                    }
                    return $"Sequence {index + 1}";

                case FieldUnitType.Choice:
                    return $"Choice {index + 1}";

                default:
                    return $"Option {index + 1}";
            }
        }
    }
}