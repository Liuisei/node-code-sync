# NodeToCodeConverter

## 概要
ビジュアルグラフの AstNode と Edge 構造から C# ソースコードに逆変換するコアエンジン。エッジインデックスを用いた高性能な再帰走査で、AstNode ツリーを StringBuilder で効率的に C# テキストに再構築します。

## 継承・実装
- 継承元: なし（静的ユーティリティクラス）
- 実装インターフェース: なし

## 責務
- AstNode 配列と AstGraphView を入力として C# ソースコード文字列を生成
- Edge インデックスから O(1) で子ノードを検索し、効率的な走査を実現
- FieldUnit スキーマに従い、Token / Node 型フィールドを再帰的に処理
- Choice フィールドの選択インデックスに応じて適切な子を出力
- NodeList / SeparatedNodeList の区別に応じて、カンマ区切り出力を制御
- オプションで Roslyn による整形（NormalizeWhitespace）を適用
- C# ソース文字列を Roslyn AST に解析するユーティリティ（CSharpToAST）を提供

## 依存コンポーネント
| コンポーネント | 役割 |
|---|---|
| `AstNode` | GraphView 上の個別ノード。RuntimeMeta で NodeMeta を保有 |
| `AstGraphView` | GraphView コンテナ。edges コレクションから parent → child 関係を取得 |
| `FieldTypeClassifier` | Token / NodeList / SeparatedNodeList 等を判定 |
| `Roslyn CSharpSyntaxTree` | 整形時に使用。ParseText() + NormalizeWhitespace() |

## フィールド・プロパティ

### EdgeKey（内部構造体）
- **型**: struct
- **メンバー**:
  - `Parent` (AstNode): ポートを持つ親ノード
  - `PortName` (string): フィールド名（出力ポート名）
- **説明**: Edge 検索の一意キー。GetHashCode() と Equals() を実装し、O(1) lookup を実現

### s_emptyChildren
- **型**: `List<AstNode>`
- **アクセス修飾子**: private static readonly
- **説明**: 子ノードのない場合のキャッシュ（アロケーション削減）

## メソッド

### NodeMetasToCSharp(roots, graphView, formatWithRoslyn, enableDebug, enableProfiling)
- **アクセス修飾子**: public static
- **戻り値**: `string`
- **説明**: 公開 API。AstNode 配列と GraphView から C# ソース文字列を生成します。3フェーズで実行
- **引数**:
  - `roots` (AstNode[]): ルートノード配列（CompilationUnit など）
  - `graphView` (AstGraphView): GraphView コンテナ（edges を提供）
  - `formatWithRoslyn` (bool): true の場合、Roslyn で整形
  - `enableDebug` (bool): 構造トレースログを出力
  - `enableProfiling` (bool): 各フェーズのタイミングを計測
- **処理フロー**:
  1. BuildEdgeLookup() で Edge マップ構築（O(n) 初期化）
  2. StringBuilder で再帰走査
  3. formatWithRoslyn が true なら ParseText() + NormalizeWhitespace()

### CSharpToAST(sourceCode, enableProfiling)
- **アクセス修飾子**: public static
- **戻り値**: `CompilationUnitSyntax`
- **説明**: C# ソース文字列を Roslyn CompilationUnitSyntax に解析。CodeToNodeConverter でも使用される共通ユーティリティ
- **引数**:
  - `sourceCode` (string): C# ソースコード
  - `enableProfiling` (bool): true の場合、Roslyn 解析時間をログ出力

### BuildEdgeLookup(graphView)
- **アクセス修飾子**: private static
- **戻り値**: `Dictionary<EdgeKey, List<AstNode>>`
- **説明**: GraphView の全 Edge をスキャンし、(Parent, PortName) → [Child] マッピングを構築。同一ポートに複数の子が接続可能（NodeList など）

### AppendNode(sb, node, edgeLookup, dbg, depth, enableDebug)
- **アクセス修飾子**: private static
- **説明**: 単一 AstNode をテキスト化。node.RuntimeMeta から FieldUnit 配列を取得し、AppendFields() に委譲

### AppendFieldUnit(sb, unit, node, edgeLookup, dbg, depth, enableDebug)
- **アクセス修飾子**: private static
- **説明**: FieldUnitType に応じて処理を分岐：
  - **Single**: FieldMetadata を AppendSingleField() で処理
  - **Choice**: ChoiceIndex の選択肢に対応する Child FieldUnit を再帰処理
  - **Sequence**: 全 Child FieldUnit を反復処理

### AppendSingleField(sb, data, node, edgeLookup, dbg, depth, enableDebug)
- **アクセス修飾子**: private static
- **説明**: Token / Node 型フィールドを処理。FieldTypeClassifier で kind を判定し出力方式を選択：
  - **Token/TokenList**: data.Value をそのまま出力（TokenList の "," は " " に正規化）
  - **SingleNode/NodeList**: GetChildNodesFast() で子を取得し AppendNode() で再帰
  - **SeparatedNodeList**: 最後以外の子の前に ", " を挿入

### GetChildNodesFast(parent, fieldName, edgeLookup)
- **アクセス修飾子**: private static
- **戻り値**: `List<AstNode>`
- **説明**: EdgeKey を用いて O(1) で子ノード list を検索。見つからない場合は s_emptyChildren を返却

## 使用例・連携フロー

```
SourceController.OnNodeUpdated()
  ↓
NodeToCodeConverter.NodeMetasToCSharp(roots, graphView)
  ├─ [Phase 1] BuildEdgeLookup(graphView)
  ├─ [Phase 2] foreach root → AppendNode()
  │  └─ AppendFields() → AppendFieldUnit() → AppendSingleField()
  │     └─ GetChildNodesFast(parent, fieldName)  [O(1)]
  │        └─ AppendNode(child, depth+1)         [再帰]
  └─ [Phase 3, optional] NormalizeWhitespace()
     ↓
  C# source string
     ↓
SourceController.UpdateCodeEditor(csharpString)
```

## 設計上の注意点

- **Edge 駆動な再構築**: ノード内に子へのポインタを持たない。GraphView の Edge が唯一の構造ソース
- **O(1) 子検索**: 再帰前に Edge テーブルを一括構築し、走査中はリニアサーチを行わない
- **StringBuilder 効率**: 16KB 初期容量で GC 圧力を削減
- **Choice インデックスの安全性**: Mathf.Clamp で bounds チェック。無効インデックスでも例外なし
- **Roslyn 整形のコスト**: formatWithRoslyn=true の場合、再パースが発生。不要なら省略推奨
- **CSharpToAST の共有**: CodeToNodeConverter と共通のユーティリティメソッドとして機能
