# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**NodeCodeSync (NCS)** is a Unity Editor tool that provides bidirectional synchronization between C# source code and a visual node graph. C# `.cs` files are the single source of truth; node graphs are UI projections backed by Microsoft Roslyn.

## Repository Layout

```
node-code-sync/
├── Packages/com.nodecodesync.unity/     # UPM package (the distributable product)
│   ├── package.json                     # Unity package manifest (v0.1.0, requires Unity 6000.3+)
│   ├── Editor/NodeCodeSync/Editor/ASTEditor/   # All source code lives here
│   │   ├── Schema/                      # Roslyn schema loading & caching
│   │   ├── Pipeline/                    # Code↔Node conversion
│   │   ├── Editor/                      # Unity Editor UI (nodes, views, windows)
│   │   ├── Common/                      # Shared utilities and event bus
│   │   └── NodeCodeSync.Editor.ASTEditor.asmdef
│   └── Test/Test.cs                     # Sample C# fixture for manual testing
└── NCSDevelopUnity/                     # Development Unity project (Unity 6)
    └── Assets/NCS/TestCS.cs             # Additional test fixture
```

## Development Setup

This is a Unity Editor package — there are no CLI build commands. Development happens entirely within Unity:

1. Open `NCSDevelopUnity/` as a Unity project (Unity 6000.3+)
2. The package is linked locally via `NCSDevelopUnity/Packages/manifest.json`
3. Use JetBrains Rider with `NCSDevelopUnity.slnx`, or Visual Studio via the generated `.csproj` files
4. NuGetForUnity manages .NET dependencies (Roslyn)

**Running tests:** Unity Test Runner (`Window > General > Test Runner`) for automated tests. Manual testing is done by opening the NCS Editor window and loading `Test/Test.cs` or `Assets/NCS/TestCS.cs` as source fixtures.

## Core Architecture

The system has three layers connected by a conversion pipeline:

```
C# Source (.cs)  ←──────────────────→  Node Graph (GraphView)
      │                                        │
      ↓  Roslyn.ParseText()                    ↑  NodeToCodeConverter
  Roslyn AST                         ConvertedNode tree
      │                                        │
      ↓  CodeToNodeConverter                   │
  NodeMeta (schema template) ─────────────────→
```

### Schema Layer (`Schema/`)

- **`SyntaxMetaModel.cs`** — Core data structures: `NCSSyntaxTree`, `NodeMeta`, `FieldUnit`, `FieldMetadata`. `FieldUnit` is a recursive union type (Single / Choice / Sequence) representing all possible Roslyn syntax shapes. `FieldMetadata` doubles as schema definition and runtime instance data (via `Value` field).
- **`RoslynXmlLoader.cs`** — Loads the Roslyn syntax XML from embedded third-party resources.
- **`SyntaxMetaParser.cs`** — Parses the XML into the typed `NCSSyntaxTree` structure.
- **`RoslynSchemaCache.cs`** — Singleton that indexes the parsed schema into multiple lookup dictionaries (`KindToNodeMetaMap`, `NodeNameOderByNameList`, `KindToFieldNameMap`) for fast runtime lookups.

### Conversion Pipeline (`Pipeline/`)

- **`CodeToNodeConverter.cs`** — Parses C# source via Roslyn, then uses reflection to walk the resulting `SyntaxNode` tree, matching each node's properties against schema `FieldUnit` definitions to produce a `ConvertedNode` tree stamped with `NodeMeta` templates.
- **`NodeToCodeConverter.cs`** — Reads `AstNode` graph nodes and their edges (edge index = child order) to reconstruct C# source via `StringBuilder`. Edges are the single source of parent/child structure — no intermediate tree is maintained.

### Editor UI (`Editor/`)

- **`AstNode.cs`** — A GraphView `Node` subclass. Dynamically generates ports and UI fields from a `NodeMeta` template at construction time.
- **`AstGraphView.cs`** — GraphView container; manages node placement and edge connections.
- **`AstTreeView.cs`** — Hierarchical tree view alternative to the graph.
- **`SourceController.cs`** — Mediates between the code text editor and the graph view; triggers conversion in both directions.
- **`MainCenterWindow.cs`** — Primary `EditorWindow` that hosts all views.

### Event Bus (`Common/NodeCodeDataEventBus.cs`)

Singleton event bus for decoupled communication between components:
- `OnCodeUpdated` — C# source text changed
- `OnNodeUpdated` — Graph structure changed
- `OnCodeCompilationUnitSyntaxUpdated` — Roslyn `CompilationUnitSyntax` updated

### Utility (`Common/FieldTypeClassifier.cs`)

Classifies Roslyn field types into five categories: `Token`, `TokenList`, `NodeList`, `SeparatedNodeList`, `SingleNode`. These categories drive both UI generation in `AstNode` and code reconstruction in `NodeToCodeConverter`.

## Key Design Rules

- **Code is truth.** Nodes are derived views. Never store authoritative state in graph nodes.
- **Edges define structure.** The parent/child ordering of `AstNode` children is encoded entirely in graph edges (by index); do not maintain a separate tree.
- **`FieldUnit` is immutable.** Use `NodeMetaExtensions.UpdateValue()` for functional updates rather than mutation.
- **Schema-agnostic pipeline.** The XML schema is the only Roslyn-specific part; the rest of the pipeline is generic. Keep `CodeToNodeConverter` and `NodeToCodeConverter` free of hardcoded syntax knowledge.
