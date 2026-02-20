# FieldTypeClassifier

## 概要

`FieldTypeClassifier`はRoslyn型文字列を5つのカテゴリに分類するユーティリティクラスです。Roslyn ASTのフィールド型をUIコンポーネント生成やコード再構築の際にどう扱うか（テキストフィールド、グラフポート等）を判定するために使用されます。

## 継承・実装

- 継承元: なし（静的ユーティリティクラス）
- 実装インターフェース: なし

## 責務

- Roslyn型文字列（`SyntaxToken`、`SyntaxList<T>`等）を`FieldTypeKind`列挙型に変換
- トークン型とノード型の判定
- リスト型と単一ノード型の判定
- セパレータが必要なリスト型（`SeparatedSyntaxList`）の識別

## 依存コンポーネント

| コンポーネント | 役割 |
|---|---|
| Roslyn (`Microsoft.CodeAnalysis`) | Syntax型の定義 |
| `AstNode` | UI生成の際、ポート・フィールドの種類を判定 |
| `CodeToNodeConverter` | スキーママッチングの際に型分類を参照 |
| `NodeToCodeConverter` | コード再構築時に型別の処理を実行 |

## 列挙型: FieldTypeKind

Roslyn型を5+1のカテゴリに分類します。

```csharp
public enum FieldTypeKind
{
    Token,              // SyntaxToken（キーワード、識別子等）
    TokenList,          // SyntaxList<SyntaxToken>
    NodeList,           // SyntaxList<T> where T : SyntaxNode
    SeparatedNodeList,  // SeparatedSyntaxList<T>（コンマ等区切り文字を含む）
    SingleNode,         // 単一ノード型（*Syntax）
    Unknown             // サポートされない型
}
```

## メソッド

### Classify(string fieldType)

- **アクセス修飾子**: public static
- **戻り値**: `FieldTypeKind`
- **説明**: Roslyn型文字列を`FieldTypeKind`に分類します。マッチング優先度は以下の通り。
  1. 完全一致チェック（`SyntaxToken`、`SyntaxList<SyntaxToken>`）
  2. プレフィックス一致（`SeparatedSyntaxList`は`SyntaxList`より先にチェック）
  3. サフィックス一致（`Syntax`で終わる＝単一ノード型）
  4. 未分類なら`Unknown`を返す
- **引数**:
  - `fieldType` (string): 分類対象のRoslyn型文字列
- **注意事項**: null/空文字列の入力は`Unknown`を返す

### IsTokenType(FieldTypeKind kind)

- **アクセス修飾子**: public static
- **戻り値**: `bool`
- **説明**: 渡された`FieldTypeKind`がトークン型（`Token`または`TokenList`）かどうかを判定します。トークン型はテキストフィールドで編集されるべきフィールドです。
- **引数**:
  - `kind` (`FieldTypeKind`): 判定対象の型カテゴリ
- **戻り値の意味**: true = トークン型（テキスト編集対象）、false = ノード型

### IsNodeType(FieldTypeKind kind)

- **アクセス修飾子**: public static
- **戻り値**: `bool`
- **説明**: 渡された`FieldTypeKind`がノード型（`SingleNode`、`NodeList`、`SeparatedNodeList`のいずれか）かどうかを判定します。ノード型はGraphView上でポート接続される可能性があるフィールドです。
- **引数**:
  - `kind` (`FieldTypeKind`): 判定対象の型カテゴリ
- **戻り値の意味**: true = ノード型（ポート接続対象）、false = トークン型

### IsListType(FieldTypeKind kind)

- **アクセス修飾子**: public static
- **戻り値**: `bool`
- **説明**: 渡された`FieldTypeKind`がリスト型（`NodeList`または`SeparatedNodeList`）かどうかを判定します。リスト型は複数子要素を持つため、UI上では複数ポートで表現されます。
- **引数**:
  - `kind` (`FieldTypeKind`): 判定対象の型カテゴリ
- **戻り値の意味**: true = リスト型（複数要素）、false = 単一ノード型

### NeedsSeparator(FieldTypeKind kind)

- **アクセス修飾子**: public static
- **戻り値**: `bool`
- **説明**: リスト型がセパレータ（コンマ、セミコロン等）を必要とするかどうかを判定します。`SeparatedNodeList`のみtrueを返します。コード再構築時に、子要素の間にセパレータを挿入する必要があるかの判定に使用されます。
- **引数**:
  - `kind` (`FieldTypeKind`): 判定対象の型カテゴリ
- **戻り値の意味**: true = セパレータが必須（`SeparatedNodeList`）、false = セパレータ不要

## 使用例・連携フロー

### 例1: UIコンポーネント生成

`AstNode`がスキーマテンプレート`NodeMeta`からポートとUIフィールドを動的に生成する際、各フィールドの分類を参照します。

```
1. NodeMeta.Fields を列挙
2. 各FieldUnit.FieldType を FieldTypeClassifier.Classify()
3. IsTokenType() → TextField生成
4. IsNodeType() → Port生成
5. IsListType() → 複数ポートを配列処理
```

### 例2: コード再構築

`NodeToCodeConverter.AppendNode()`がノードのフィールド値から出力文字列を構築する際、セパレータの有無を判定します。

```
1. 対象FieldUnit.FieldType を Classify()
2. NeedsSeparator() で子要素間に区切り文字を挿入すべきか判定
3. IsTokenType() なら Token.Text をそのまま出力
4. IsNodeType() なら子ノードへの再帰呼び出し
```

## 設計上の注意点

### マッチング優先度

`Classify()`は以下の順序でマッチングします。不正な順序でマッチすると誤分類の可能性があります。

1. **完全一致**: `SyntaxToken`、`SyntaxList<SyntaxToken>`
2. **プレフィックス**: `SeparatedSyntaxList`を`SyntaxList`より先にチェック（重要）
3. **サフィックス**: `*Syntax`で終わる型
4. **フォールバック**: `Unknown`

### スキーマ非依存設計

`FieldTypeClassifier`はRoslyn型文字列のパターン認識のみを行い、スキーマレイヤーの詳細な型情報には依存しません。これにより、Roslyn更新時の影響を最小化しています。

### トークン型とノード型の明確な分岐

UIコンポーネント生成とコード再構築の両方で、`IsTokenType()`と`IsNodeType()`の二分法により、処理フローが一貫性を保ちます。これは「トークンは文字列として編集、ノードはグラフ構造として接続」というNCSの設計原則を支えています。
