using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Common/NodeCodeDataEventBus.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    internal class NodeCodeDataEventBus
    {
        static readonly object _lock = new object();
        static NodeCodeDataEventBus _instance;
        NodeCodeDataEventBus() { }
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


        public event Action<string> OnCodeUpdated;
        public event Action<string> OnNodeUpdated;
        public event Action<CompilationUnitSyntax> OnCodeCompilationUnitSyntaxUpdated;

        public void UpdateCodeCompilationUnitSyntax(CompilationUnitSyntax cuSyntax)
        {
            OnCodeCompilationUnitSyntaxUpdated?.Invoke(cuSyntax);
        }

        public void UpdateCode(string code)
        {
            OnCodeUpdated?.Invoke(code);
        }

        public void UpdateNode(string nodemetaArray)
        {
            OnNodeUpdated?.Invoke(nodemetaArray);
        }

    }
}
