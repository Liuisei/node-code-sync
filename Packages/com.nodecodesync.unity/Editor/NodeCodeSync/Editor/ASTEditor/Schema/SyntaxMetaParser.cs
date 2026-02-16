using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Schema/SyntaxMetaParser.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// The specialized parser that transforms Roslyn's XML schema into a structured NCSSyntaxTree.
    /// Acts as the bridge between raw data and the high-level semantic models.
    /// </summary>
    public static class SyntaxMetaParser
    {
        /// <summary>
        /// Entry point: Parses the entire XML document into a comprehensive Syntax Tree.
        /// </summary>
        public static NCSSyntaxTree Parse(XDocument xdoc)
        {
            var root = xdoc.Root;
            if (root == null || root.Name != "Tree")
                throw new Exception("Root element must be 'Tree'");

            var rootAttr = root.Attribute("Root")?.Value;

            // 1. Parse base definitions that don't change
            var predefinedNodes = root.Elements("PredefinedNode")
                .Select(ParsePredefinedNode)
                .ToArray();

            // 2. Parse inheritance-level definitions (e.g., ExpressionSyntax)
            var abstractNodes = root.Elements("AbstractNode")
                .Select(ParseAbstractNode)
                .ToArray();

            // 3. Parse concrete syntax node definitions (e.g., ClassDeclarationSyntax)
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

            // Abstract nodes also contain structural fields
            var fields = ParseFieldUnits(elem).ToArray();

            return new AbstractNodeMeta(name, baseType, fields, typeComment);
        }

        private static NodeMeta ParseNode(XElement elem)
        {
            var name = elem.Attribute("Name")?.Value;
            var baseType = elem.Attribute("Base")?.Value;
            var skipConvenienceFactories = (bool?)elem.Attribute("SkipConvenienceFactories") ?? false;

            // Map SyntaxKind names to this Node
            var kinds = elem.Elements("Kind")
                .Select(k => k.Attribute("Name")?.Value)
                .Where(k => k != null)
                .ToArray();

            var typeComment = elem.Element("TypeComment")?.Element("summary")?.Value?.Trim();
            var factoryComment = elem.Element("FactoryComment")?.Element("summary")?.Value?.Trim();

            // Recursively parse fields, choices, and sequences
            var fields = ParseFieldUnits(elem).ToArray();

            return new NodeMeta(name, baseType, kinds, fields, typeComment, factoryComment, skipConvenienceFactories);
        }

        /// <summary>
        /// Orchestrates the recursive parsing of nested structural units.
        /// </summary>
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

        /// <summary>
        /// Parses a single field, including its metadata and possible token kinds.
        /// </summary>
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

        /// <summary>
        /// Parses a 'Choice' element which allows selecting one of many children.
        /// </summary>
        private static FieldUnit ParseChoice(XElement elem)
        {
            var optional = (bool?)elem.Attribute("Optional") ?? false;
            var children = ParseFieldUnits(elem).ToArray();
            return FieldUnit.CreateChoice(children, optional);
        }

        /// <summary>
        /// Parses a 'Sequence' element representing an ordered collection of fields.
        /// </summary>
        private static FieldUnit ParseSequence(XElement elem)
        {
            var children = ParseFieldUnits(elem).ToArray();
            return FieldUnit.CreateSequence(children);
        }
    }
}