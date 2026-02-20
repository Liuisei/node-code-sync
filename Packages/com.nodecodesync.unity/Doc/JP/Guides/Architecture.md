# NodeCodeSync 設計思想とアーキテクチャ

## 概要

NodeCodeSync (NCS) は Unity Editor ツールで、C# ソースコードとビジュアルノードグラフを**双方向同期**します。

**設計の根本原則は「コードが真実である」ことです。** ノードグラフはコードから導出される UI 投影であり、ノードの状態が変わるたびにコードが再生成されます。

## コアの設計原則

### 1. コードが真実 (Code is Truth)

- C# ソースコード（`.cs` ファイル）が単一の情報源。
- ノードグラフはコードを視覚化し、編集するための UI 投影に過ぎない。
- グラフノードは自身の状態を永続化しない。すべてのデータは`NodeMeta`スキーマテンプレートと、ノード間の接続（エッジ）から復元可能。

### 2. エッジが構造を定義 (Edges Define Structure)

- ノード間の親子関係は、GraphView のエッジ（接続線）により完全に定義される。
- エッジの**接続順序**（インデックス）がコード上の子要素の順序を決定する。
- 中間的なツリー構造は保持しない。エッジは必要な場面で O(1) ルックアップ用に辞書化される。

### 3. スキーマ駆動型 UI 生成 (Schema-Driven UI)

- すべてのノード UI（テキストフィールド、ドロップダウン、ポート）は`NodeMeta`テンプレートから動的に生成される。
- Roslyn XML スキーマが UI/コード変換の単一の仕様源。

## レイヤー構成

```
┌─────────────────────────────────────────┐
│       Editor UI (Views / Windows)       │
│  AstGraphView / AstNode / AstTreeView   │
│  MainCenterWindow / SourceController    │
└────────────────┬────────────────────────┘
                 │
         ┌───────▼──────────┐
         │   Event Bus      │
         │ (NodeCodeData    │
         │  EventBus)       │
         └───────┬──────────┘
                 │
     ┌───────────┴────────────┐
     │                        │
┌────▼──────────────┐  ┌─────▼──────────────┐
│    Pipeline       │  │  Schema Layer      │
│  (Converters)     │  │  (SyntaxMetaMod-   │
│                   │  │   el / Cache)      │
│ • CodeToNode      │  │                    │
│   Converter       │  │ • RoslynSchema     │
│ • NodeToCode      │  │   Cache            │
│   Converter       │  │ • SyntaxMetaModel  │
│                   │  │ • RoslynXmlLoader  │
└───────────────────┘  │ • SyntaxMetaParser │
                       └────────────────────┘
                               │
                               ▼
                    Roslyn XML Schema
                       (Embedded)
```

### Schema レイヤー (`Schema/`)

**責務**: Roslyn 構文の形状を定義し、実行時スキーマを管理する。

#### キーコンポーネント

| コンポーネント | 役割 |
|---|---|
| `SyntaxMetaModel` | コア データ構造：`NCSSyntaxTree`, `NodeMeta`, `FieldUnit`, `FieldMetadata` を定義。`FieldUnit` は再帰的な Union 型で Single / Choice / Sequence を表現。 |
| `RoslynXmlLoader` | 埋め込み Roslyn 構文 XML をロード。 |
| `SyntaxMetaParser` | XML を型付き `NCSSyntaxTree` に解析。 |
| `RoslynSchemaCache` | スレッドセーフなシングルトン。複数のルックアップ辞書（`KindToNodeMetaMap`, `NodeNameOderByNameList` など）を構築・保持。 |

#### データフロー

```
Roslyn XML (embedded)
       │
       ▼
RoslynXmlLoader.Load()
       │
       ▼
System.Xml.Linq.XDocument
       │
       ▼
SyntaxMetaParser.Parse()
       │
       ▼
NCSSyntaxTree (root, predefined, abstract, nodes)
       │
       ▼
RoslynSchemaCache.Build()
       │
       ▼
_kindToNodeMetaMap (kind string → NodeMeta)
_nodeMetaMap (node name → NodeMeta)
_kindToFieldNameMap (node name → token kind map)
_nodeNameOrderByNameList (sorted node names)
```

### Pipeline レイヤー (`Pipeline/`)

**責務**: C# コードとノードグラフを双方向で変換する。

#### CodeToNodeConverter

C# ソースコード → ノードツリー への変換。

**処理フロー**:
1. Roslyn で C# を解析 (`CSharpSyntaxTree.ParseText()`)
2. `RoslynSchemaCache.KindToNodeMetaMap` で各 SyntaxNode の Kind から NodeMeta テンプレートを検索
3. リフレクションで SyntaxNode のプロパティを走査し、スキーマの「ノード型フィールド」に対応
4. トークン値を填埋（Token / TokenList）し、Choice インデックスを確定
5. 各ノードに GUID を付与して`ConvertedNode`ツリーを返す

**重要**: 変換後のツリーは一時的。グラフビューに追加される際、GUID によりノードが識別される。

#### NodeToCodeConverter

ノードグラフ → C# ソースコード への変換。

**処理フロー**:
1. GraphView のすべてのエッジから`EdgeKey` (親ノード + ポート名) → 子ノード配列 の辞書を構築 (O(1) ルックアップ用)
2. ルートノードから再帰的に走査
3. 各ノードの`RuntimeMeta`フィールドを検査：
   - Token フィールド → 値を出力
   - ノードフィールド → エッジを辿り、子ノードを再帰的に出力
   - Choice → 選択インデックスで分岐
4. StringBuilder で効率的に出力

**特徴**:
- エッジのインデックス順序が子要素の出力順序を決定
- Separator (`,`) が必要な型 (`SeparatedNodeList`) は自動で挿入

### Editor UI レイヤー (`Editor/`)

**責務**: ユーザーインタラクション、グラフの視覚化と編集、コードプレビュー。

#### AstGraphView

GraphView コンテナ。ノード配置とエッジ接続を管理。

**主要責務**:
- C# コードから ConvertedNode ツリーを 2 パスで展開：
  - **Pass 1**: ノード生成（DFS、GUID マッピング）
  - **Pass 2**: エッジ接続（FieldChildren を参照）
- グラフデータ変更 → `OnGraphDataChanged` イベント → コード同期
- 外部コード更新 → `OnCodeUpdated` イベントを受取 → グラフ再構築

**UI 操作**:
- ContentZoomer、ContentDragger、SelectionDragger、RectangleSelector を Manipulator として登録
- GridBackground で視認性向上

#### AstNode

GraphView のノード。`NodeMeta` テンプレートから UI を動的生成。

**主要責務**:
- `NodeMeta`から UI を動的に生成（フィルタフィールド、ノード名ポップアップ、フィールド UI、ポート）
- Token フィールド → EditableTextField（値は`runtimeMeta`に記録）
- ノードフィールド → OutputPort（子への接続点）
- Choice フィールド → DropdownPopup（インデックス切替で UI 再構築）
- `OnNodeDataChanged` イベントで変更をブロードキャスト

**ポート**:
- `inputPort`: 単一のInput（親への接続）
- `outputPorts[]`: フィールド名ごとの Output（子への接続、リスト型はMulti-capacity）

#### SourceController

ファイル選択と、コードプレビュー UI を管理。

**責務**:
- MonoScript (C#) ファイルの選択インターフェース
- 「Generate Node Graph」ボタン → EventBus 経由でコード更新をトリガ
- ノード更新時にリアルタイムで生成コードをプレビュー表示

#### MainCenterWindow

エディタウィンドウの最上位コンテナ。AstGraphView、AstTreeView、SourceController を統合。

### Common レイヤー (`Common/`)

**責務**: 共有インフラ、イベント通信、型分類。

#### NodeCodeDataEventBus

スレッドセーフなシングルトン。EventBus パターンで疎結合な通信。

**イベント**:
| イベント | 発行者 | 購読者 |
|---|---|---|
| `OnCodeUpdated` | SourceController | AstGraphView (グラフ再構築), AST Debugger |
| `OnNodeUpdated` | AstGraphView (SyncGraphToCode) | SourceController (プレビュー更新) |
| `OnCodeCompilationUnitSyntaxUpdated` | SourceController | AST Debugger |

#### FieldTypeClassifier

Roslyn フィールド型を分類する Utility。

**分類**:
- `Token`: SyntaxToken
- `TokenList`: SyntaxList<SyntaxToken>
- `SingleNode`: *Syntax
- `NodeList`: SyntaxList<T> where T : SyntaxNode
- `SeparatedNodeList`: SeparatedSyntaxList<T>
- `Unknown`: 未対応

**用途**: UI 生成（TextField vs Port）、コード生成時の Separator 判定。

## データフロー: 双方向同期

### コード → グラフ

```
1. ユーザーがファイル選択 (SourceController)
   │
2. MonoScript.text を取得
   │
3. NodeCodeDataEventBus.UpdateCode() をコール
   │
4. AstGraphView.OnCodeUpdated()：
   a. 既存グラフをクリア
   b. CodeToNodeConverter.CsharpToConvertedTree() で ConvertedNode ツリー生成
   c. CreateNodesRecursive() で AstNode を生成、GUID マップに登録
   d. ConnectEdgesRecursive() で エッジを接続
   │
5. グラフ表示完了
```

### グラフ → コード

```
1. ユーザーがノードを編集 (AstNode.OnNodeDataChanged イベント)
   │
2. AstGraphView.OnGraphDataChanged()
   │
3. SyncGraphToCode()：
   a. GetRootNodes() でルートノードを検出（入力エッジなし）
   b. NodeToCodeConverter.NodeMetasToCSharp()：
      - EdgeKey 辞書を構築（O(1) ルックアップ）
      - ルートノードから再帰的に走査、コード生成
   │
4. NodeCodeDataEventBus.UpdateNode(generatedCode) で配信
   │
5. SourceController.UpdateSourcePreview() でプレビュー更新
```

## 重要な実装パターン

### 1. FieldUnit の不変性

`NodeMeta.UpdateValue()` を使用して**関数型アップデート**を実施。元のオブジェクトは変更しない。

```csharp
// ❌ 避ける
meta.Fields[0].Data.Value = "newValue";

// ✅ 推奨
meta = meta.UpdateValue("fieldName", "newValue");
```

### 2. エッジの O(1) ルックアップ

NodeToCodeConverter では`EdgeKey`辞書でエッジを高速検索。

```csharp
var edgeLookup = BuildEdgeLookup(graphView);
var children = GetChildNodesFast(parent, fieldName, edgeLookup);
```

### 3. リフレクション駆動の構造マッピング

CodeToNodeConverter は Roslyn SyntaxNode のプロパティをリフレクションで走査し、スキーマの「ノード型フィールド」に照合。スキーマに未定義のプロパティ（例：`Parent`）は自動的にスキップされる。

### 4. UI の再帰的生成

AstNode は`NodeMeta.Fields`を再帰的に走査し、`FieldUnitType`ごとに分岐：
- `Single` → TextFieldまたはPort
- `Choice` → PopupField + 選択ブランチのUI
- `Sequence` → 子を順次処理

## スキーマの安定性と拡張性

### スキーマが変わった場合

1. Roslyn XML を更新
2. `RoslynSchemaCache.Rebuild()` をコール（または Editor 再起動）
3. 既存のグラフは解析し直す必要なし。NodeMeta が変わっていなければ互換性を保つ。

### 新しい Roslyn 構文ノードを追加

1. Roslyn XML に新規ノード定義を追加
2. SyntaxMetaParser が自動で解析
3. CodeToNodeConverter が Kind マッチで自動認識
4. AstNode UI が自動生成

## パフォーマンス考慮事項

### 大規模ファイルの変換

- **CodeToNodeConverter**: リフレクション走査 + 再帰 → O(n) where n = SyntaxNode 数
- **NodeToCodeConverter**: O(m) where m = エッジ数
- スキーマキャッシュ初期化: 一度だけ実行（シングルトン）

### UI 描画

- GraphView ノード数 → UI 要素数は線形相関
- 大規模グラフの自動レイアウト: 現在は簡易版（depth * 350 + index * 120）。複雑な手動配置に対応予定。

## 参考実装ガイドライン

- **新しい UI 要素を追加**: `FieldUnit` の新型 or `FieldMetadata` 拡張を検討
- **新しいコード変換ロジック**: `CodeToNodeConverter` / `NodeToCodeConverter` に処理追加（スキーマ非依存に設計）
- **新しいイベント**: `NodeCodeDataEventBus` にイベント定義追加
