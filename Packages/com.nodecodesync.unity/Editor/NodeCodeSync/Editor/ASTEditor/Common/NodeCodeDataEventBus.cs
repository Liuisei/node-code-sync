using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;

// Path: NodeCodeSync/Editor/ASTEditor/Common/NodeCodeDataEventBus.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// A centralized event bus that facilitates communication between the Source View, 
    /// Graph View, and AST Debugger. It ensures decoupled synchronization of code and node data.
    /// </summary>
    internal class NodeCodeDataEventBus
    {
        private static readonly object _lock = new object();
        private static NodeCodeDataEventBus _instance;

        private NodeCodeDataEventBus() { }

        /// <summary>
        /// Thread-safe singleton instance of the event bus.
        /// </summary>
        public static NodeCodeDataEventBus Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new NodeCodeDataEventBus();
                    return _instance;
                }
            }
        }

        // =========================================================
        // Event Definitions
        // =========================================================

        /// <summary> Triggered when raw C# source code is updated (e.g., file loaded or edited). </summary>
        public event Action<string> OnCodeUpdated;

        /// <summary> Triggered when the node graph structure or values are modified. </summary>
        public event Action<string> OnNodeUpdated;

        /// <summary> Triggered when the Roslyn CompilationUnit is re-analyzed. </summary>
        public event Action<CompilationUnitSyntax> OnCodeCompilationUnitSyntaxUpdated;

        // =========================================================
        // Broadcast Methods
        // =========================================================

        /// <summary>
        /// Broadcasts a newly parsed Roslyn syntax tree to all listeners (e.g., AST Debugger).
        /// </summary>
        public void UpdateCodeCompilationUnitSyntax(CompilationUnitSyntax cuSyntax)
        {
            OnCodeCompilationUnitSyntaxUpdated?.Invoke(cuSyntax);
        }

        /// <summary>
        /// Broadcasts source code changes to trigger graph regeneration or synchronization.
        /// </summary>
        public void UpdateCode(string code)
        {
            OnCodeUpdated?.Invoke(code);
        }

        /// <summary>
        /// Broadcasts node data updates to trigger code generation or preview updates.
        /// </summary>
        /// <param name="nodemetaArray">Serialized node data or status information.</param>
        public void UpdateNode(string nodemetaArray)
        {
            OnNodeUpdated?.Invoke(nodemetaArray);
        }
    }
}