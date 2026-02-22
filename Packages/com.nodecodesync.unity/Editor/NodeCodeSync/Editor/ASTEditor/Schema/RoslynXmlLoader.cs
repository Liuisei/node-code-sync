using System.Xml.Linq;
using UnityEngine;

// Path: NodeCodeSync/Editor/ASTEditor/Schema/RoslynXmlLoader.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    public static class RoslynXmlLoader
    {
        private const string RoslynSyntaxResourcePath = "ThirdParty/Roslyn/RoslynSyntax";
        public static XDocument Load()
        {
            try
            {
                var textAsset = Resources.Load<TextAsset>(RoslynSyntaxResourcePath);
                if (textAsset == null)
                {
                    Debug.LogError($"RoslynSyntax.xml not found at Resources/{RoslynSyntaxResourcePath}.xml");
                    return null;
                }

                return XDocument.Parse(textAsset.text);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Failed to load or parse RoslynSyntax.xml.");
                Debug.LogException(ex);
                return null;
            }
        }
    }
}
