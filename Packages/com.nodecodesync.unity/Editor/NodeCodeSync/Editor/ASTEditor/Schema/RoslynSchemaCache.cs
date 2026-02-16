using System.Collections.Generic;
using System.Linq;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Schema/RoslynSchemaCache.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// Roslyn XML から構築したスキーマ（NodeMeta / Family）を共有するキャッシュ。
    /// Editor起動中に何度もビルドしないための箱。
    /// </summary>
    internal sealed class RoslynSchemaCache
    {
        static readonly object _lock = new object();
        static RoslynSchemaCache _instance;
        RoslynSchemaCache() { }
        public static RoslynSchemaCache Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new RoslynSchemaCache();
                    _instance.Build();
                    return _instance;
                }
            }
        }

        private bool _loaded = false;
        private NCSSyntaxTree _syntaxTree;

        /// <summary> Node.Name → NodeMeta（AstNode の Popup 選択用） </summary>
        public IReadOnlyDictionary<string, NodeMeta> NodeMetaMap => _nodeMetaMap;

        /// <summary> Kind名 → NodeMeta（C# → NodeMeta 変換用） </summary>
        public IReadOnlyDictionary<string, NodeMeta> KindToNodeMetaMap => _kindToNodeMetaMap;

        /// <summary> NodeName → { TokenKind名 → FieldName }（Token値埋め用） </summary>
        public IReadOnlyDictionary<string, Dictionary<string, string>> KindToFieldNameMap => _kindToFieldNameMap;

        public IReadOnlyList<string> NodeNameOderByNameList => _nodeNameOrderByNameList;

        Dictionary<string, NodeMeta> _nodeMetaMap;
        Dictionary<string, NodeMeta> _kindToNodeMetaMap;
        Dictionary<string, Dictionary<string, string>> _kindToFieldNameMap;
        List<string> _nodeNameOrderByNameList;

        private void Build()
        {
            if (_loaded) return;
            System.Xml.Linq.XDocument xDocument = RoslynXmlLoader.Load();
            _syntaxTree = SyntaxMetaParser.Parse(xDocument);
            BuildNodeMetaDic();
            BuildKindToNodeMetaMap();
            BuildKindToFieldNameMap();
            BuildNodeNameList();
            _loaded = true;
        }

        private void BuildNodeMetaDic()
        {
            _nodeMetaMap = _syntaxTree.Nodes.ToDictionary(n => n.Name, n => n);
        }

        private void BuildKindToNodeMetaMap()
        {
            _kindToNodeMetaMap = new Dictionary<string, NodeMeta>();
            foreach (var node in _syntaxTree.Nodes)
            {
                if (node.Kinds == null) continue;
                foreach (var kind in node.Kinds)
                    _kindToNodeMetaMap[kind] = node;
            }
        }

        private void BuildKindToFieldNameMap()
        {
            _kindToFieldNameMap = new Dictionary<string, Dictionary<string, string>>();
            foreach (var node in _syntaxTree.Nodes)
            {
                var map = new Dictionary<string, string>();
                if (node.Fields != null)
                    CollectTokenKinds(node.Fields, map);

                if (map.Count > 0)
                    _kindToFieldNameMap[node.Name] = map;
            }
        }

        private void CollectTokenKinds(FieldUnit[] fields, Dictionary<string, string> map)
        {
            if (fields == null) return;
            foreach (var field in fields)
            {
                if (field.Type == FieldUnitType.Single
                    && !string.IsNullOrEmpty(field.Data.Name)
                    && FieldTypeClassifier.IsTokenType(FieldTypeClassifier.Classify(field.Data.FieldType)))
                {
                    if (field.Data.Kinds != null)
                    {
                        foreach (var kind in field.Data.Kinds)
                            map[kind] = field.Data.Name;
                    }
                }

                if (field.Children != null)
                    CollectTokenKinds(field.Children, map);
            }
        }

        private void BuildNodeNameList()
        {
            _nodeNameOrderByNameList = NodeMetaMap.Keys.OrderBy(x => x).ToList();
        }

        /// <summary>
        /// 明示的に再構築したいとき用（XML更新など）
        /// </summary>
        public void Rebuild()
        {
            lock (_lock)
            {
                _loaded = false;
                Build();
            }
        }
    }
}
