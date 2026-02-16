using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

// Path: NodeCodeSync/Editor/ASTEditor/Schema/RoslynXmlLoader.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    public static class RoslynXmlLoader
    {
        public static XDocument Load()
        {
            var textAsset = Resources.Load<TextAsset>("RoslynSyntax");
            if (textAsset == null)
            {
                Debug.LogError("RoslynSyntax.xml not found in Resources");
                return null;
            }

            return XDocument.Parse(textAsset.text);
        }
    }
}
