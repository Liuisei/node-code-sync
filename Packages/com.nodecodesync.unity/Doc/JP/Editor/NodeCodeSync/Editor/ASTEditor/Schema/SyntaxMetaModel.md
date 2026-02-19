# ノードモデル設計

## 概要

NCS のノードモデルは、Roslyn の構文スキーマを Unity のグラフ UI に投影するための中間データ構造です。
コードが Single Source of Truth であり、ノードはその **投影** にすぎません。

---

## 型の全体像

```text
NCSSyntaxTree
├── PredefinedNodeMeta[]   // 組み込み型 (SyntaxToken など)
├── AbstractNodeMeta[]     // 抽象基底クラス (ExpressionSyntax など)
└── NodeMeta[]             // 具体ノード (ClassDeclarationSyntax など)
                               └── FieldUnit[]
                                       ├── FieldUnit (Single)
                                       ├── FieldUnit (Choice)
                                       │       └── FieldUnit[] children
                                       └── FieldUnit (Sequence)
                                               └── FieldUnit[] children
```

---

## NCSSyntaxTree

Roslyn 構文階層全体を表すルート構造体。
スキーマの "ワールドマップ" として機能します。

```csharp
public struct NCSSyntaxTree
{
    public readonly string Root;
    public readonly PredefinedNodeMeta[] PredefinedNodes;
    public readonly AbstractNodeMeta[]  AbstractNodes;
    public readonly NodeMeta[]          Nodes;
}
```

| フィールド | 説明 |
|---|---|
| `Root` | ルートノードの名前 |
| `PredefinedNodes` | 組み込み型メタデータの配列 |
| `AbstractNodes` | 抽象ノードメタデータの配列 |
| `Nodes` | 具体ノードメタデータの配列（グラフノードの雛形） |

---

## PredefinedNodeMeta

`SyntaxToken`・`SyntaxNode` などの Roslyn 組み込み型を表します。

```csharp
public readonly struct PredefinedNodeMeta
{
    public readonly string Name;
    public readonly string Base;
}
```

---

## AbstractNodeMeta

`ExpressionSyntax`・`StatementSyntax` など、複数の具体ノードが継承する抽象基底クラスを表します。

```csharp
public readonly struct AbstractNodeMeta
{
    public readonly string    Name;
    public readonly string    Base;
    public readonly FieldUnit[] Fields;
    public readonly string    TypeComment;
}
```

---

## NodeMeta

`ClassDeclarationSyntax`・`InvocationExpressionSyntax` など、具体的な構文ノードを表します。
**グラフノード 1 つの雛形** となる主要な型です。

```csharp
public readonly struct NodeMeta
{
    public readonly string    Name;
    public readonly string    Base;
    public readonly string[]  Kinds;
    public readonly FieldUnit[] Fields;
    public readonly string    TypeComment;
    public readonly string    FactoryComment;
    public readonly bool      SkipConvenienceFactories;
    public readonly string    Guid;   // グラフ上のインスタンス識別子
}
```

| フィールド | 説明 |
|---|---|
| `Name` | 構文ノードの型名 |
| `Base` | 継承元の型名 |
| `Kinds` | 対応する `SyntaxKind` の一覧 |
| `Fields` | フィールド定義の配列 |
| `Guid` | グラフ上の一意識別子（スキーマ定義時は `null`、インスタンス化時に付与） |

---

## FieldUnit

フィールド・選択肢（Choice）・連続構造（Sequence）を **統一的に** 表す構造体。
再帰合成により C# 構文の複雑さをそのまま反映できます。

```csharp
public readonly struct FieldUnit
{
    public readonly FieldUnitType Type;
    public readonly FieldMetadata Data;
    public readonly FieldUnit[]   Children;
    public readonly int           ChoiceIndex;
}

public enum FieldUnitType
{
    Single,    // 単一フィールド
    Choice,    // いずれか 1 つを選択
    Sequence,  // 順序を持つ複数要素
}
```

### ファクトリメソッド

| メソッド | 説明 |
|---|---|
| `FieldUnit.CreateField(...)` | `Single` 型のフィールドを生成 |
| `FieldUnit.CreateChoice(children)` | `Choice` 型を生成（子要素から 1 つ選択） |
| `FieldUnit.CreateSequence(children)` | `Sequence` 型を生成（順序付き複数要素） |

---

## FieldMetadata

フィールドの **スキーマ定義**（Name, Type など）と **動的な値**（Value）を両方保持します。
`Value` が入ることで「スキーマ」から「インスタンスデータ」に変わります。

```csharp
public readonly struct FieldMetadata
{
    public readonly string   Name;
    public readonly string   FieldType;
    public readonly bool     Optional;
    public readonly bool     Override;
    public readonly int      MinCount;
    public readonly bool     AllowTrailingSeparator;
    public readonly string[] Kinds;
    public readonly string   PropertyComment;
    public readonly string   Value;   // 実際のコード値（変数名・リテラルなど）
}
```

| フィールド | 説明 |
|---|---|
| `Optional` | 省略可能かどうか |
| `MinCount` | SeparatedList の最小要素数 |
| `AllowTrailingSeparator` | 末尾セパレータを許可するか |
| `Value` | コード生成時に使用される実値 |

---

## NodeMetaExtensions

`NodeMeta` を **不変（immutable）** のまま値を更新するユーティリティです。
状態変更は常に新しいインスタンスを返すことで安全に管理されます。

```csharp
// 指定フィールドの値を更新した新しい NodeMeta を返す
NodeMeta updated = nodeMeta.UpdateValue("Identifier", "myVariable");

// Choice フィールドの選択インデックスを更新
NodeMeta updated = nodeMeta.UpdateValue("ChoiceField", null, newIndex: 1);
```

---

## データフロー

```text
RoslynSyntax.xml
      │
      ▼
SyntaxMetaParser      // XML → NCSSyntaxTree に変換
      │
      ▼
RoslynSchemaCache     // NodeMeta をキャッシュ・管理
      │
      ├──▶ CodeToNodeConverter  // C# コード → NodeMeta インスタンス
      │
      └──▶ NodeToCodeConverter  // NodeMeta インスタンス → C# コード
```

---

## 設計方針

- **不変性** : すべての構造体は `readonly`。値更新は新規インスタンス生成で行う
- **中間ツリーなし** : NodeMeta がスキーマとインスタンスデータを兼ねる
- **スキーマ駆動** : XML スキーマの差し替えで多言語対応が可能
- **コードが真実** : NodeMeta は常にコードの投影であり、本体ではない
