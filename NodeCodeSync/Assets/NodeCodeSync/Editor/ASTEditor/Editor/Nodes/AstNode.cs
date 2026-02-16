using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Editor/Nodes/AstNode.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// AST ノード
    /// NodeName 選択 → RuntimeMeta の Fields から UI を動的生成
    /// Choice 切り替え・TextField 編集 → RuntimeMeta を更新 → RefreshUI() で全体再描画
    /// </summary>
    public class AstNode : Node
    {
        // UI コンテナ
        VisualElement fieldContainer;

        // TextFieldFilter
        TextField filterField;

        // NodeName 選択 Popup
        PopupField<string> namePopup;

        // 全ノード名（フィルタ元）
        List<string> allNodeNames;

        // ランタイム状態を保持する NodeMeta（UpdateValue で更新される）
        NodeMeta? runtimeMeta;

        // 入力ポート
        Port inputPort;

        // 動的生成したポートを管理
        readonly Dictionary<string, Port> outputPorts = new();

        public Port InputPort => inputPort;
        public NodeMeta? RuntimeMeta { get => runtimeMeta; }

        public Port GetOutputPort(string fieldName)
            => outputPorts.TryGetValue(fieldName, out var p) ? p : null;

        public event Action OnNodeDataChanged;

        public event Action OnDisposed;

        // =========================================================
        // コンストラクタ
        // =========================================================

        //あとでCodeToAstグラフで使える
        public AstNode() : this(null) { }
        public AstNode(NodeMeta? nodeMeta)
        {
            title = "AST Node";

            // 入力ポート（全ノード共通・一度だけ生成）
            inputPort = InstantiatePort(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Single,
                typeof(bool)
            );
            inputPort.portName = "In";
            inputContainer.Add(inputPort);

            // NodeName 選択 Popup
            var cache = RoslynSchemaCache.Instance;
            allNodeNames = cache.NodeNameOderByNameList.ToList();

            filterField = new TextField("Filter")
            {
                style = { marginBottom = 2 }
            };
            filterField.RegisterValueChangedCallback(evt =>
            {
                OnFilterChanged(evt.newValue);
            });
            mainContainer.Add(filterField);

            namePopup = new PopupField<string>(
                "Node",
                allNodeNames,
                allNodeNames[0]
            );
            namePopup.RegisterValueChangedCallback(evt =>
            {
                OnNodeNameSelected(evt.newValue);
                OnNodeDataChanged.Invoke();// NodeName 選択時にイベントも発火
            });
            mainContainer.Add(namePopup);

            fieldContainer = new VisualElement();
            fieldContainer.style.marginTop = 4;
            mainContainer.Add(fieldContainer);

            // ここが分岐点：渡された NodeMeta があればそれを使う、なければデフォルト選択
            if (nodeMeta.HasValue)
            {
                runtimeMeta = nodeMeta.Value;
                // Popup の表示も合わせる
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
        // フィルター変更時
        // =========================================================

        void OnFilterChanged(string filter)
        {
            List<string> filtered = string.IsNullOrEmpty(filter)
                ? allNodeNames
                : allNodeNames
                    .Where(n => n.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

            if (filtered.Count == 0)
                return;

            string currentValue = namePopup.value;

            if (!filtered.Contains(currentValue))
                filtered.Insert(0, currentValue);

            int idx = mainContainer.IndexOf(namePopup);
            mainContainer.Remove(namePopup);

            namePopup = new PopupField<string>("Node", filtered, currentValue);
            namePopup.RegisterValueChangedCallback(evt =>
            {
                OnNodeNameSelected(evt.newValue);
            });

            mainContainer.Insert(idx, namePopup);

        }

        // =========================================================
        // Name 選択時 → RuntimeMeta をスキーマからコピーして保持
        // =========================================================

        void OnNodeNameSelected(string name)
        {
            var cache = RoslynSchemaCache.Instance;
            if (!cache.NodeMetaMap.TryGetValue(name, out var meta))
            {
                runtimeMeta = null;
                fieldContainer.Clear();
                ClearDynamicPorts();
                fieldContainer.Add(new Label($"Meta not found: {name}"));
                RefreshPorts();
                return;
            }

            // スキーマから新しい RuntimeMeta を作成（Value / ChoiceIndex はデフォルト）
            runtimeMeta = meta;
            title = name;

            RefreshUI();
            OnNodeDataChanged.Invoke();
        }

        // =========================================================
        // RefreshUI: 全体再描画
        // fieldContainer + outputContainer をクリアして RuntimeMeta から再構築
        // =========================================================

        void RefreshUI()
        {
            fieldContainer.Clear();
            ClearDynamicPorts();

            if (runtimeMeta == null || !runtimeMeta.HasValue)
            {
                fieldContainer.Add(new Label("(no meta)"));
                RefreshPorts();
                return;
            }

            BuildUI(runtimeMeta.Value.Fields);

            RefreshExpandedState();
            RefreshPorts();
        }

        // =========================================================
        // BuildUI: Fields 配列をループして BuildFieldUnit を呼ぶ
        // =========================================================

        void BuildUI(FieldUnit[] fields)
        {
            if (fields == null) return;

            foreach (var field in fields)
            {
                BuildFieldUnit(field);
            }
        }

        // =========================================================
        // FieldUnit → UI 生成
        // =========================================================

        void BuildFieldUnit(FieldUnit unit)
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
        // Single → TextField or Port
        // =========================================================

        void BuildSingleField(FieldMetadata data)
        {
            if (string.IsNullOrEmpty(data.Name) || string.IsNullOrEmpty(data.FieldType))
                return;

            var kind = FieldTypeClassifier.Classify(data.FieldType);
            var label = data.Optional ? $"{data.Name}?" : data.Name;

            if (FieldTypeClassifier.IsTokenType(kind))
            {
                // Token → TextField
                var tf = new TextField(label);
                tf.style.marginBottom = 2;
                if (data.Optional)
                    tf.style.backgroundColor = new Color(0.2f, 0.2f, 0.25f);

                // RuntimeMeta に保存されている Value を復元
                tf.value = data.Value ?? "";

                // 値変更時に RuntimeMeta を更新
                var fieldName = data.Name; // クロージャ用にキャプチャ
                tf.RegisterValueChangedCallback(evt =>
                {
                    if (runtimeMeta.HasValue)
                    {
                        runtimeMeta = runtimeMeta.Value.UpdateValue(fieldName, evt.newValue);
                        OnNodeDataChanged.Invoke();
                    }
                });

                fieldContainer.Add(tf);
                return;
            }

            if (FieldTypeClassifier.IsNodeType(kind))
            {
                // Node → 出力ポート
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
                    port.tooltip = $"{data.Name} (Optional)";
                }

                outputContainer.Add(port);
                outputPorts[data.Name] = port;
                return;
            }
        }

        // =========================================================
        // Choice → PopupField + 選択中の子だけ描画
        // ChoiceIndex を使って状態を復元・更新
        // =========================================================

        void BuildChoiceField(FieldUnit choice)
        {
            if (choice.Children == null || choice.Children.Length == 0)
                return;

            // 選択肢の表示名を生成
            var optionNames = new List<string>();
            for (int i = 0; i < choice.Children.Length; i++)
            {
                optionNames.Add(GetFieldUnitLabel(choice.Children[i], i));
            }

            // RuntimeMeta の ChoiceIndex から現在の選択を復元
            var currentIndex = Mathf.Clamp(choice.ChoiceIndex, 0, optionNames.Count - 1);

            var popup = new PopupField<string>("Choice", optionNames, optionNames[currentIndex]);
            popup.style.marginTop = 4;
            popup.style.marginBottom = 2;
            popup.style.backgroundColor = new Color(0.25f, 0.2f, 0.2f);

            // Choice の Data.Name をキーとして使う（null の場合はフォールバック）
            var choiceName = choice.Data.Name;

            popup.RegisterValueChangedCallback(evt =>
            {
                var newIdx = optionNames.IndexOf(evt.newValue);
                if (newIdx < 0) return;

                if (runtimeMeta.HasValue)
                {
                    // RuntimeMeta の ChoiceIndex を更新
                    runtimeMeta = runtimeMeta.Value.UpdateValue(choiceName, null, newIdx);

                    // 全体再描画（ポート増殖を防ぐ）
                    RefreshUI();
                }
            });

            fieldContainer.Add(popup);

            // 現在選択中の子だけを描画（局所コンテナではなく直接 fieldContainer に追加）
            var selectedChild = choice.Children[currentIndex];
            BuildFieldUnit(selectedChild);
        }

        // =========================================================
        // ポート管理
        // =========================================================

        void ClearDynamicPorts()
        {
            outputContainer.Clear();
            outputPorts.Clear();
        }

        // =========================================================
        // ユーティリティ
        // =========================================================

        string GetFieldUnitLabel(FieldUnit unit, int index)
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