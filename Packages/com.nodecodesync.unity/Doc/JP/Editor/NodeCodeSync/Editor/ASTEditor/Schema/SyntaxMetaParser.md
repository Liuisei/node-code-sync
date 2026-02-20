# SyntaxMetaParser

## 概要
Roslyn の XML スキーマを高レベルな NCSSyntaxTree 構造体に変換する静的パーサー。XML の DOM 構造を、シンタックスノード型（PredefinedNode / AbstractNode / ConcreteNode）の意味論的階層に昇華させ、スキーマレイヤーの「ワールドマップ」を構築します。

## 継承・実装
- `public static`（静的ユーティリティクラス）

## 責務
- XDocument をバリデーション付きで読み込む
- `<PredefinedNode>` 要素を PredefinedNodeMeta に変換
- `<AbstractNode>` 要素を AbstractNodeMeta に変換
- `<Node>` 要素を NodeMeta に変換
- ネストされた `<Field>` / `<Choice>` / `<Sequence>` を再帰的に FieldUnit に変換
- Kind（SyntaxKind）・Optional・MinCount などの属性を抽出

## 依存コンポーネント
| コンポーネント | 役割 |
|---|---|
| `System.Xml.Linq` (XDocument, XElement) | XML の DOM 操作 |
| `NCSSyntaxTree` | パース結果を格納するルート構造体 |
| `PredefinedNodeMeta` | 組み込み型のメタデータ |
| `AbstractNodeMeta` | 抽象基底クラスのメタデータ |
| `NodeMeta` | 具体的な構文ノード型のメタデータ |
| `FieldUnit` / `FieldMetadata` | フィールド構造の統一表現 |

## メソッド

### Parse(xdoc)
- **アクセス修飾子**: public static
- **戻り値**: `NCSSyntaxTree`
- **説明**: エントリポイント。XML ドキュメント全体をパースして NCSSyntaxTree を返す
- **引数**:
  - `xdoc` (XDocument): Roslyn XML スキーマファイル
- **処理フロー**:
  1. Root 要素を取得（null または Name が "Tree" 以外なら例外スロー）
  2. Root 属性 "Root" を取得（ルートノード名）
  3. `<PredefinedNode>` → ParsePredefinedNode() で変換
  4. `<AbstractNode>` → ParseAbstractNode() で変換
  5. `<Node>` → ParseNode() で変換
  6. 3配列と Root 名から NCSSyntaxTree を構築して返却

### ParsePredefinedNode(elem)
- **アクセス修飾子**: private static
- **戻り値**: `PredefinedNodeMeta`
- **説明**: "Name" / "Base" 属性を取得して PredefinedNodeMeta を生成

### ParseAbstractNode(elem)
- **アクセス修飾子**: private static
- **戻り値**: `AbstractNodeMeta`
- **説明**: "Name" / "Base" 属性、TypeComment テキスト、ParseFieldUnits() のフィールド配列から AbstractNodeMeta を生成

### ParseNode(elem)
- **アクセス修飾子**: private static
- **戻り値**: `NodeMeta`
- **説明**: `<Node>` 要素を解析して NodeMeta を生成
- **処理**:
  1. "Name" / "Base" / "SkipConvenienceFactories" 属性を取得
  2. `<Kind>` 子要素の "Name" 属性を配列に収集
  3. TypeComment / FactoryComment テキストを抽出
  4. ParseFieldUnits() でフィールド配列を取得

### ParseFieldUnits(parent)
- **アクセス修飾子**: private static
- **戻り値**: `IEnumerable<FieldUnit>`
- **説明**: 親要素直下の child 要素を yield return で変換するジェネレータ。再帰的なネスト構造に対応
- **分岐**:
  - `"Field"` → ParseField()
  - `"Choice"` → ParseChoice()
  - `"Sequence"` → ParseSequence()

### ParseField(elem)
- **アクセス修飾子**: private static
- **戻り値**: `FieldUnit`
- **説明**: "Name" / "Type" / "Optional" / "MinCount" / "AllowTrailingSeparator" 属性と `<Kind>` 子要素から Single 型 FieldUnit を生成

### ParseChoice(elem)
- **アクセス修飾子**: private static
- **戻り値**: `FieldUnit`
- **説明**: "Optional" 属性と ParseFieldUnits() の子配列から Choice 型 FieldUnit を生成

### ParseSequence(elem)
- **アクセス修飾子**: private static
- **戻り値**: `FieldUnit`
- **説明**: ParseFieldUnits() の子配列から Sequence 型 FieldUnit を生成

## 使用例・連携フロー

```
RoslynXmlLoader.Load()
  → XDocument
     ↓
SyntaxMetaParser.Parse(xdoc)
  ├─ ParsePredefinedNode → PredefinedNodeMeta[]
  ├─ ParseAbstractNode  → AbstractNodeMeta[]
  └─ ParseNode          → NodeMeta[]
       └─ ParseFieldUnits（再帰）
            ├─ ParseField    → FieldUnit (Single)
            ├─ ParseChoice   → FieldUnit (Choice)
            └─ ParseSequence → FieldUnit (Sequence)
     ↓
NCSSyntaxTree
  → RoslynSchemaCache でキャッシュ化
```

## 設計上の注意点
- **XML バリデーション**: Root 要素名を厳密チェックし不正な入力を早期検出
- **遅延評価**: ParseFieldUnits() は yield return で中間オブジェクト生成を回避
- **null 安全性**: `?.Attribute()` + `??` 演算子でデフォルト値を確保
- **再帰構造**: Choice / Sequence の子要素も ParseFieldUnits() で同一ロジック処理
- **スキーマ駆動**: XML とメタデータ構造の 1:1 対応でスキーマ更新の影響を最小化
