# AstNode

## 概要
GraphView 内で Roslyn AST ノードを視覚的に表現する Node クラス。NodeMeta スキーマテンプレートから動的に UI ポート・フィールドを生成し、ノード間の接続関係を管理します。

## 継承・実装
- 継承元: `UnityEditor.Experimental.GraphView.Node`

## 責務
- 動的 UI 生成：NodeMeta から TextField（トークン）と Port（ノード）を自動生成
- フィールド値管理：FieldUnit 構造を辿りながら、ユーザー入力を runtimeMeta に反映
- ノード選択 UI：フィルタリング可能な PopupField で複数の SyntaxNode 型を切り替え
- イベント発行：データ変更時に OnNodeDataChanged を発行し、グラフの再同期をトリガ

## 依存コンポーネント
| コンポーネント | 役割 |
|---|---|
| `NodeMeta` | UI 生成の基となるスキーマテンプレート |
| `FieldUnit` | 再帰的なフィールド構造（Single / Choice / Sequence）を定義 |
| `FieldMetadata` | トークン値や型情報を保持 |
| `RoslynSchemaCache` | NodeMeta マップやノード名リストへの高速アクセス |
| `FieldTypeClassifier` | フィールド型を分類し、UI 部品の種類を決定 |

## フィールド・プロパティ

### runtimeMeta
- **型**: `NodeMeta?`
- **アクセス修飾子**: private
- **説明**: 現在のノードが表現する SyntaxNode のメタデータ（値を含む）

### outputPorts
- **型**: `Dictionary<string, Port>`
- **アクセス修飾子**: private
- **説明**: フィールド名をキーとした出力ポート（子ノード接続用）の辞書

### InputPort
- **型**: `Port`（property）
- **アクセス修飾子**: public
- **説明**: 入力ポート（親ノードからの接続）

### RuntimeMeta
- **型**: `NodeMeta?`（property）
- **アクセス修飾子**: public
- **説明**: 現在のランタイムメタデータを取得

### filterField / namePopup / allNodeNames
- **説明**: ノード名フィルタ用 TextField、SyntaxNode 型選択 PopupField、全ノード名キャッシュ

## メソッド

### GetOutputPort(fieldName)
- **アクセス修飾子**: public
- **戻り値**: `Port`
- **説明**: フィールド名に対応する出力ポートを取得。存在しない場合は null

### RefreshUI()
- **アクセス修飾子**: private
- **説明**: runtimeMeta から UI を再構築。フィールドコンテナ・ポートをクリアして BuildUI() を呼び出す

### BuildUI(fields)
- **アクセス修飾子**: private
- **説明**: FieldUnit 配列をイテレートし BuildFieldUnit() で各フィールドを処理

### BuildFieldUnit(unit)
- **アクセス修飾子**: private
- **説明**: FieldUnitType に応じて処理をディスパッチ：
  - **Single** → BuildSingleField()
  - **Choice** → BuildChoiceField()
  - **Sequence** → 子を反復

### BuildSingleField(data)
- **アクセス修飾子**: private
- **説明**: Token 型は TextField、ノード型は出力 Port として生成。Optional フィールドは背景色で視認

### BuildChoiceField(choice)
- **アクセス修飾子**: private
- **説明**: Choice 型の子オプションを PopupField で提供し、選択ブランチの UI を再帰構築。選択変更時は RefreshUI() で全体再構築

### OnNodeNameSelected(name)
- **アクセス修飾子**: private
- **説明**: ノード型変更時に新しい NodeMeta を取得し UI を全面再構築

## イベント

### OnNodeDataChanged
- **型**: `Action`
- **説明**: ノードの値または構造が変更されたとき発行。グラフ再同期をトリガ

## 使用例・連携フロー

```
AstGraphView が ConvertedNode ツリーを走査
  ↓
new AstNode(convertedNode.Self) でインスタンス生成
  └─ RefreshUI() で動的 UI 生成（TextField / Port）
     ↓
ユーザが PopupField でノード型変更
  → OnNodeNameSelected() → RefreshUI()
     ↓
ユーザが TextField 編集
  → runtimeMeta.UpdateValue()
  → OnNodeDataChanged?.Invoke()
     ↓
AstGraphView が NodeToCodeConverter で C# に逆変換
```

## 設計上の注意点
- **FieldUnit は不変**：値更新は runtimeMeta.UpdateValue() で新インスタンスを生成
- **ポート情報のみ保持**：parent/child 関係の真実は graph edge。AstNode はポート参照だけを保有
- **Choice 変更コスト**：選択変更時に全体 RefreshUI()。大規模 hierarchy では性能に注意
