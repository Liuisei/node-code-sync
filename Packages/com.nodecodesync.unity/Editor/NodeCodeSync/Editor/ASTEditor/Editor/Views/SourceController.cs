using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Editor/Views/SourceController.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    public class SourceController : IDisposable
    {
        public VisualElement Root { get => _root; }

        private VisualElement _root;

        ObjectField fileField;
        TextField originalCodeField;
        TextField sourcePreviewField;


        public void Dispose()
        {
            if (NodeCodeDataEventBus.Instance != null)
            {
                NodeCodeDataEventBus.Instance.OnNodeUpdated -= UpdateSourcePreview;
            }
        }

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

            // --- File Input ---
            fileField = new ObjectField("C# File")
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
                    CompilationUnitSyntax compilationUnitSyntax = NodeToCodeConverter.CSharpToAST(originalCodeField.value);
                    NodeCodeDataEventBus.Instance.UpdateCodeCompilationUnitSyntax(compilationUnitSyntax);
                }
                else
                {
                    originalCodeField.value = "";
                }
            });
            _root.Add(fileField);

            // --- Generate Button ---
            var generateButton = new Button(GanerateButtonClicked)
            {
                text = "Generate Graph",
                style = { marginTop = 8 }
            };
            _root.Add(generateButton);

            // --- Original Code ---
            var originalTitle = new Label("Original Code")
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
                style =
                {
                    flexGrow = 1,
                    minHeight = 100
                }
            };

            originalCodeField = new TextField
            {
                multiline = true,
                isReadOnly = true,
                style =
                {
                    whiteSpace = WhiteSpace.Pre,
                    fontSize = 11
                }
            };
            originalScroll.Add(originalCodeField);
            _root.Add(originalScroll);

            // --- Source Preview ---
            var previewTitle = new Label("Source Preview")
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
                style =
                {
                    flexGrow = 1,
                    minHeight = 100
                }
            };

            sourcePreviewField = new TextField
            {
                multiline = true,
                isReadOnly = true,
                style =
                {
                    whiteSpace = WhiteSpace.Pre,
                    fontSize = 11
                }
            };
            previewScroll.Add(sourcePreviewField);
            _root.Add(previewScroll);
            NodeCodeDataEventBus.Instance.OnNodeUpdated += UpdateSourcePreview;
        }

        /// <summary>
        /// Publishes the current source code to the event bus (CodeData).
        /// Triggered by the "Generate Graph" button.
        /// </summary>
        private void GanerateButtonClicked()
        {
            Debug.Log("Generate Graph button clicked");
            NodeCodeDataEventBus.Instance.UpdateCode(originalCodeField.value);
        }

        /// <summary>
        /// Receives NodeData from the event bus and updates the source preview.
        /// </summary>
        private void UpdateSourcePreview(string nodeMetas)
        {
            Debug.Log("Updating Source Preview");
            sourcePreviewField.value = nodeMetas;
        }
    }
}
