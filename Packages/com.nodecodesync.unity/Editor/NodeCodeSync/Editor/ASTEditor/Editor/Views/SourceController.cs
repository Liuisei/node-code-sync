using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Path: NodeCodeSync/Editor/ASTEditor/Editor/Views/SourceController.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// Controller responsible for managing source code input and synchronization.
    /// It handles C# file selection, triggers graph generation, and provides 
    /// a real-time preview of the source code generated from the node graph.
    /// </summary>
    public class SourceController : IDisposable
    {
        /// <summary>
        /// Gets the root visual element containing the file selector and code previews.
        /// </summary>
        public VisualElement Root { get => _root; }

        private VisualElement _root;

        // UI Components
        private ObjectField fileField;
        private TextField originalCodeField;
        private TextField sourcePreviewField;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceController"/> class.
        /// Sets up the UI layout and subscribes to synchronization events.
        /// </summary>
        public SourceController()
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

            // --- Section: File Selection ---
            // Allows users to pick a C# script (MonoScript) from the project assets.
            fileField = new ObjectField("Target C# File")
            {
                objectType = typeof(MonoScript),
                allowSceneObjects = false
            };
            fileField.RegisterValueChangedCallback(evt =>
            {
                var script = evt.newValue as MonoScript;
                if (script != null)
                {
                    originalCodeField.value = script.text;
                    // Initial conversion to AST for the structural debugger
                    CompilationUnitSyntax compilationUnitSyntax = NodeToCodeConverter.CSharpToAST(originalCodeField.value);
                    NodeCodeDataEventBus.Instance.UpdateCodeCompilationUnitSyntax(compilationUnitSyntax);
                }
                else
                {
                    originalCodeField.value = string.Empty;
                }
            });
            _root.Add(fileField);

            // --- Section: Actions ---
            var generateButton = new Button(GanerateButtonClicked)
            {
                text = "Generate Node Graph",
                style = { marginTop = 8 }
            };
            _root.Add(generateButton);

            // --- Section: Original Source View ---
            // Displays the raw content of the selected C# file.
            var originalTitle = new Label("Original Source Code")
            {
                style =
                {
                    unityFontStyleAndWeight = UnityEngine.FontStyle.Bold,
                    marginTop = 8,
                    marginBottom = 4
                }
            };
            _root.Add(originalTitle);

            var originalScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                style = { flexGrow = 1, minHeight = 100 }
            };

            originalCodeField = new TextField
            {
                multiline = true,
                isReadOnly = true,
                style = { whiteSpace = WhiteSpace.Pre, fontSize = 11 }
            };
            originalScroll.Add(originalCodeField);
            _root.Add(originalScroll);

            // --- Section: Live Preview ---
            // Shows the resulting code after editing the AST nodes.
            var previewTitle = new Label("Synchronized Source Preview")
            {
                style =
                {
                    unityFontStyleAndWeight = UnityEngine.FontStyle.Bold,
                    marginTop = 8,
                    marginBottom = 4
                }
            };
            _root.Add(previewTitle);

            var previewScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                style = { flexGrow = 1, minHeight = 100 }
            };

            sourcePreviewField = new TextField
            {
                multiline = true,
                isReadOnly = true,
                style = { whiteSpace = WhiteSpace.Pre, fontSize = 11 }
            };
            previewScroll.Add(sourcePreviewField);
            _root.Add(previewScroll);

            // Subscribe to node updates to reflect changes in the preview field
            NodeCodeDataEventBus.Instance.OnNodeUpdated += UpdateSourcePreview;
        }

        /// <summary>
        /// Broadcasts the current source code to the event bus to trigger graph generation.
        /// </summary>
        private void GanerateButtonClicked()
        {
            Debug.Log("[NodeCodeSync] Triggering Graph Generation from Source.");
            NodeCodeDataEventBus.Instance.UpdateCode(originalCodeField.value);
        }

        /// <summary>
        /// Updates the preview text field when the node graph data is modified.
        /// </summary>
        /// <param name="nodeMetas">The generated source code string (or serialized node metadata).</param>
        private void UpdateSourcePreview(string nodeMetas)
        {
            // Update the UI to show the latest state of the code
            sourcePreviewField.value = nodeMetas;
        }

        /// <summary>
        /// Cleans up event subscriptions to prevent memory leaks in the Unity Editor.
        /// </summary>
        public void Dispose()
        {
            if (NodeCodeDataEventBus.Instance != null)
            {
                NodeCodeDataEventBus.Instance.OnNodeUpdated -= UpdateSourcePreview;
            }
        }
    }
}