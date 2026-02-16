using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Schema/SyntaxMetaParser.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    public static class SyntaxMetaParser
    {
        public static NCSSyntaxTree Parse(XDocument xdoc)
        {
            var root = xdoc.Root;
            if (root == null || root.Name != "Tree")
                throw new Exception("Root element must be 'Tree'");

            var rootAttr = root.Attribute("Root")?.Value;
            
            var predefinedNodes = root.Elements("PredefinedNode")
                .Select(ParsePredefinedNode)
                .ToArray();

            var abstractNodes = root.Elements("AbstractNode")
                .Select(ParseAbstractNode)
                .ToArray();

            var nodes = root.Elements("Node")
                .Select(ParseNode)
                .ToArray();

            return new NCSSyntaxTree(rootAttr, predefinedNodes, abstractNodes, nodes);
        }

        private static PredefinedNodeMeta ParsePredefinedNode(XElement elem)
        {
            var name = elem.Attribute("Name")?.Value;
            var baseType = elem.Attribute("Base")?.Value;
            return new PredefinedNodeMeta(name, baseType);
        }

        private static AbstractNodeMeta ParseAbstractNode(XElement elem)
        {
            var name = elem.Attribute("Name")?.Value;
            var baseType = elem.Attribute("Base")?.Value;
            
            var typeComment = elem.Element("TypeComment")?.Element("summary")?.Value?.Trim();
            
            var fields = ParseFieldUnits(elem).ToArray();

            return new AbstractNodeMeta(name, baseType, fields, typeComment);
        }

        private static NodeMeta ParseNode(XElement elem)
        {
            var name = elem.Attribute("Name")?.Value;
            var baseType = elem.Attribute("Base")?.Value;
            var skipConvenienceFactories = (bool?)elem.Attribute("SkipConvenienceFactories") ?? false;

            var kinds = elem.Elements("Kind")
                .Select(k => k.Attribute("Name")?.Value)
                .Where(k => k != null)
                .ToArray();

            var typeComment = elem.Element("TypeComment")?.Element("summary")?.Value?.Trim();
            var factoryComment = elem.Element("FactoryComment")?.Element("summary")?.Value?.Trim();

            var fields = ParseFieldUnits(elem).ToArray();

            return new NodeMeta(name, baseType, kinds, fields, typeComment, factoryComment, skipConvenienceFactories);
        }

        private static IEnumerable<FieldUnit> ParseFieldUnits(XElement parent)
        {
            foreach (var child in parent.Elements())
            {
                if (child.Name == "Field")
                {
                    yield return ParseField(child);
                }
                else if (child.Name == "Choice")
                {
                    yield return ParseChoice(child);
                }
                else if (child.Name == "Sequence")
                {
                    yield return ParseSequence(child);
                }
            }
        }

        private static FieldUnit ParseField(XElement elem)
        {
            var name = elem.Attribute("Name")?.Value;
            var type = elem.Attribute("Type")?.Value;
            var optional = (bool?)elem.Attribute("Optional") ?? false;
            var overrideAttr = (bool?)elem.Attribute("Override") ?? false;
            var minCount = (int?)elem.Attribute("MinCount") ?? 0;
            var allowTrailingSeparator = (bool?)elem.Attribute("AllowTrailingSeparator") ?? false;

            var kinds = elem.Elements("Kind")
                .Select(k => k.Attribute("Name")?.Value)
                .Where(k => k != null)
                .ToArray();

            var propertyComment = elem.Element("PropertyComment")?.Element("summary")?.Value?.Trim();

            return FieldUnit.CreateField(
                name, type, optional, overrideAttr, 
                minCount, allowTrailingSeparator, kinds, propertyComment);
        }

        private static FieldUnit ParseChoice(XElement elem)
        {
            var optional = (bool?)elem.Attribute("Optional") ?? false;
            var children = ParseFieldUnits(elem).ToArray();
            return FieldUnit.CreateChoice(children, optional);
        }

        private static FieldUnit ParseSequence(XElement elem)
        {
            var children = ParseFieldUnits(elem).ToArray();
            return FieldUnit.CreateSequence(children);
        }
    }
}
