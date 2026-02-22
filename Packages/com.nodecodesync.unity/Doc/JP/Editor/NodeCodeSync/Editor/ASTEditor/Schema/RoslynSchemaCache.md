# RoslynSchemaCache

## 概要
Roslyn XML から構築したスキーマ（NodeMeta）をエディター起動中にシングルトンとしてキャッシュするクラス。複数コンポーネントから頻繁にアクセスされるスキーマ lookup を高速化し、何度も再ビルドしないための集約点です。

## 継承・実装
- `internal sealed`
- 継承元: なし
- 実装インターフェース: なし

## 責務
- Roslyn XML から構築した NCSSyntaxTree を一度だけ解析してメモリに保持
- SyntaxKind → NodeMeta、NodeName → NodeMeta など複数の lookup dictionary を事前構築
- Token 型フィールドの TokenKind → FieldName マッピングを構築
- Code-to-Node / Node-to-Code 変換処理からの高速 lookup を提供
- UI（ノード選択ドロップダウン）に必要なノード一覧を管理
- スレッドセーフなシングルトンアクセスを保証

## 依存コンポーネント
| コンポーネント | 役割 |
|---|---|
| `RoslynXmlLoader` | Roslyn XML リソースをロード |
| `SyntaxMetaParser` | XML を解析し NCSSyntaxTree 構造体に変換 |
| `FieldTypeClassifier` | Token 型フィールドの分類（Kind マップ構築時） |
| `NCSSyntaxTree` | スキーマ全体（Predefined / Abstract / Concrete ノード） |
| `NodeMeta` / `FieldUnit` | スキーマデータ構造 |

## フィールド・プロパティ

### Instance
- **型**: `RoslynSchemaCache`（property, get のみ）
- **アクセス修飾子**: public static
- **説明**: スレッドセーフなシングルトンアクセスポイント。初回アクセス時に Build() を呼び出す（lock + null 合体演算子）

### KindToNodeMetaMap
- **型**: `IReadOnlyDictionary<string, NodeMeta>`
- **説明**: SyntaxKind 名 → NodeMeta のマッピング。C# → NodeMeta 変換用

### NodeMetaMap
- **型**: `IReadOnlyDictionary<string, NodeMeta>`
- **説明**: NodeName → NodeMeta のマッピング。AstNode のポップアップ選択用

### KindToFieldNameMap
- **型**: `IReadOnlyDictionary<string, Dictionary<string, string>>`
- **説明**: NodeName → { TokenKind 名 → FieldName } の入れ子マップ。Token 値埋め込み時に使用

### NodeNameOderByNameList
- **型**: `IReadOnlyList<string>`
- **説明**: ノード名のアルファベット順ソート済みリスト。UI ドロップダウンの表示順

## メソッド

### Build()
- **アクセス修飾子**: private
- **説明**: XML ロード → 解析 → 各種 dictionary 構築を一括実行。`_loaded` フラグで二重実行を防止
- **処理フロー**:
  1. `_loaded` が true なら即座に return
  2. RoslynXmlLoader.Load() で XML 取得
  3. SyntaxMetaParser.Parse() で NCSSyntaxTree 構築
  4. BuildNodeMetaDic() → BuildKindToNodeMetaMap() → BuildKindToFieldNameMap() → BuildNodeNameList()
  5. `_loaded = true` で完了フラグ設定

### BuildKindToNodeMetaMap()
- **アクセス修飾子**: private
- **説明**: 各ノードの Kinds 配列を走査し SyntaxKind → NodeMeta マッピングを作成。重複 Kind は後続ノードで上書き

### BuildKindToFieldNameMap()
- **アクセス修飾子**: private
- **説明**: ノードの Fields から Token 型フィールドを再帰的に抽出し、TokenKind → FieldName マッピングを構築

### CollectTokenKinds(fields, map)
- **アクセス修飾子**: private
- **引数**:
  - `fields` (FieldUnit[]): 解析対象フィールド配列
  - `map` (Dictionary<string, string>): TokenKind → FieldName 格納用
- **説明**: FieldUnit 配列を再帰的に走査し Token 型フィールドの Kinds を収集。Choice / Sequence にも対応

### Rebuild()
- **アクセス修飾子**: public
- **説明**: スキーマキャッシュを明示的に再構築（XML 更新時など）。lock で排他制御後 `_loaded = false` → Build()

## 使用例・連携フロー

```
CodeToNodeConverter
  → RoslynSchemaCache.Instance.KindToNodeMetaMap
  → SyntaxNode の Kind から NodeMeta テンプレート取得

AstNode（ポップアップ）
  → RoslynSchemaCache.Instance.NodeNameOderByNameList
  → ソート済みノード名リストで選択肢生成

NodeToCodeConverter
  → RoslynSchemaCache.Instance.KindToFieldNameMap
  → TokenKind から FieldName を逆引き
```

## 設計上の注意点
- **遅延初期化**: Instance 初回アクセス時に自動で Build()
- **スレッドセーフ**: lock で保護。複数スレッドからの同時アクセスも安全
- **IReadOnlyDictionary 公開**: 外部からのキャッシュ破壊を防ぐ
- **Token Kind マップ**: Token 型フィールドのみ Kinds を収集。ノード参照フィールドは Kind を持たないため除外
