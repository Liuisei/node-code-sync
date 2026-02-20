# CodeToNodeConverter

## 概要
C# ソースコード（Roslyn SyntaxTree）をビジュアルグラフ用の中間構造（ConvertedNode ツリー）に変換するコアエンジン。リフレクションベースの走査により、Roslyn の内部プロパティをカスタム XML スキーマと照合し、スキーマテンプレート付きの node 木構造を生成します。

## 継承・実装
- 継承元: なし（静的ユーティリティクラス）
- 実装インターフェース: なし

## 責務
- C# ソースコードを Roslyn で解析し、SyntaxNode ツリーを構築
- SyntaxNode のプロパティをリフレクションで走査し、スキーマ定義と照合
- Token 値（キーワード、修飾子など）をスキーマフィールドに抽出・マッピング
- Choice フィールドの選択インデックスを、実際の子ノードの有無で確定
- 一意の GUID を各ノードに割り当て、スキーマテンプレートをインスタンス化
- ConvertedNode ツリーを返却（後続の EdgeBuilder で GraphView 構造に変換される）

## 依存コンポーネント
| コンポーネント | 役割 |
|---|---|
| `RoslynSchemaCache` | SyntaxKind → NodeMeta マッピング、フィールド定義の高速参照 |
| `FieldTypeClassifier` | フィールド型を Token / TokenList / NodeList / SeparatedNodeList / SingleNode に分類 |
| `NodeToCodeConverter` | 逆変換エンジン（CSharpToAST で Roslyn 解析を提供） |
| `NodeMeta` / `FieldUnit` / `FieldMetadata` | スキーマデータ構造 |

## フィールド・プロパティ

### ConvertedNode.Self
- **型**: `NodeMeta`
- **アクセス修飾子**: public
- **説明**: このノードに対応するスキーマテンプレートと実行時インスタンスデータ

### ConvertedNode.FieldChildren
- **型**: `Dictionary<string, ConvertedNode[]>`
- **アクセス修飾子**: public
- **説明**: フィールド名（例: "Parameters", "Body"）から、そのフィールドに属する子 ConvertedNode 配列へのマッピング。複数の子を持つリスト型フィールドは配列で表現される

## メソッド

### CsharpToConvertedTree(sourceCode)
- **アクセス修飾子**: public static
- **戻り値**: `ConvertedNode`
- **説明**: エントリポイント。C# ソース文字列を ConvertedNode ツリーに変換します。内部的には Roslyn 解析 → キャッシュ取得 → 再帰的ノード構築を実行
- **引数**:
  - `sourceCode` (string): 変換対象の C# ソースコード
- **処理フロー**:
  1. NodeToCodeConverter.CSharpToAST() で Roslyn 解析
  2. RoslynSchemaCache.Instance を取得
  3. BuildConvertedNode() を呼び出し再帰構築
  4. StringBuilder に処理ログを蓄積し Unity Console に出力

### BuildConvertedNode(syntaxNode, cache, sb, depth)
- **アクセス修飾子**: private static
- **戻り値**: `ConvertedNode`
- **説明**: 単一の SyntaxNode を ConvertedNode に変換。SyntaxKind をスキーマのノードメタテンプレートと照合し、Token 値と Choice インデックスを埋め込み、子ノードを再帰的に構築
- **引数**:
  - `syntaxNode` (SyntaxNode): 変換対象の Roslyn ノード
  - `cache` (RoslynSchemaCache): スキーマキャッシュ
  - `sb` (StringBuilder): デバッグログ蓄積用
  - `depth` (int): ツリーの深さ（インデント用）
- **例外処理**: SyntaxKind がスキーマに存在しない場合は警告ログを出力して null を返す

### FillValues(meta, syntaxNode, cache)
- **アクセス修飾子**: private static
- **戻り値**: `NodeMeta`
- **説明**: NodeMeta に Token 値と Choice インデックスを埋め込みます。リフレクションで SyntaxNode のプロパティを走査し、フィールド型に応じた値抽出を実施
- **処理フロー**:
  1. FillTokensByReflection() で SyntaxToken / SyntaxTokenList を抽出
  2. FillChoiceIndex() で各 Choice フィールドのインデックスを初期設定

### FillTokensByReflection(meta, syntaxNode)
- **アクセス修飾子**: private static
- **戻り値**: `NodeMeta`
- **説明**: Roslyn の SyntaxToken / SyntaxTokenList プロパティを検出し、スキーマ定義と照合します。Token は単一値、TokenList は空白で連結
- **例**: Modifiers フィールド → "public static" に変換

### BuildFieldChildrenByReflection(syntaxNode, cache, sb, depth, meta)
- **アクセス修飾子**: private static
- **戻り値**: `Dictionary<string, ConvertedNode[]>`
- **説明**: SyntaxNode のプロパティをリフレクションで走査。ノード型フィールドのみを検出し、SyntaxNode または SyntaxList を再帰的に ConvertedNode に変換。スキーマで定義されたフィールド名のみ処理（Parent など無関連プロパティは除外）

### FixChoiceIndexByFieldChildren(meta, fieldChildren)
- **アクセス修飾子**: private static
- **戻り値**: `NodeMeta`
- **説明**: 子ノードが実際に存在するかを確認し、Choice フィールドの選択インデックスを確定

### CollectNodeFields(fields) / CollectTokenFields(fields)
- **アクセス修飾子**: private static
- **説明**: FieldUnit スキーマをツリー走査し、ノード型またはToken型フィールドの定義を収集

## 使用例・連携フロー

```
SourceController.OnCodeEdited()
  ↓
CodeToNodeConverter.CsharpToConvertedTree(sourceCode)
  ├─ NodeToCodeConverter.CSharpToAST(sourceCode)   [Roslyn 解析]
  ├─ RoslynSchemaCache.Instance
  └─ BuildConvertedNode(CompilationUnitSyntax)
     ├─ FillValues()                  [Token / ChoiceIndex 埋め込み]
     ├─ BuildFieldChildrenByReflection()  [子ノード走査]
     └─ FixChoiceIndexByFieldChildren()  [Choice 確定]
        ↓
     ConvertedNode tree
        ↓
EdgeBuilder.BuildGraphFromConvertedTree()
```

## 設計上の注意点

- **スキーマ駆動**: XML スキーマで定義されたフィールド名のみ処理。未定義プロパティは無視
- **不変性**: NodeMeta は UpdateValue() で関数型に更新。直接変異させない
- **エラーハンドリング**: SyntaxKind 未検出時は警告のみ出力し処理継続。null チェック必須
- **GUID 割り当て**: 各ノードに一意 GUID を割り当て、GraphView の node ID として使用
