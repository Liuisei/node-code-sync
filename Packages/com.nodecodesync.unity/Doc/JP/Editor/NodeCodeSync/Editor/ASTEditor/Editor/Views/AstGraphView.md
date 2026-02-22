# AstGraphView

## 概要

`AstGraphView` は Unity の GraphView を継承するコンテナクラスで、C# ソースコードから生成されたノードツリーを視覚化・編集するための主要な GraphView です。ノード配置、エッジ接続、グラフと C# コードの双方向同期を統括します。

**ファイル**: `Packages/com.nodecodesync.unity/Editor/NodeCodeSync/Editor/ASTEditor/Editor/Views/AstGraphView.cs`

## 継承・実装

- **継承元**: `UnityEditor.Experimental.GraphView.GraphView`

## 責務

| 責務 | 説明 |
|---|---|
| **グラフビューのセットアップ** | Manipulator (ContentZoomer, ContentDragger など) と GridBackground を初期化 |
| **コード → ノード変換の統合** | C# ソースコードを受け取り、ConvertedNode ツリーから AstNode を生成・配置 |
| **エッジ接続の確立** | ConvertedNode の親子関係を GraphView エッジに変換 |
| **グラフ → コード同期** | ノード編集時にグラフをコードに変換し、EventBus 経由で配信 |
| **UI イベント管理** | ノード変更イベントを集約し、全体の同期をトリガ |

## 依存コンポーネント

| コンポーネント | 役割 |
|---|---|
| `CodeToNodeConverter` | C# コードを ConvertedNode ツリーに変換 |
| `NodeToCodeConverter` | グラフノードをコードに逆変換 |
| `AstNode` | GraphView ノード。この GraphView に追加される |
| `NodeCodeDataEventBus` | EventBus。コード更新・ノード更新イベントを配信 |
| `RoslynSchemaCache` | スキーマキャッシュ（AstNode 生成時に参照） |

## フィールド・プロパティ

### OnGraphDataChanged
- **型**: `event Action`
- **アクセス修飾子**: public
- **説明**: グラフ構造またはノードデータが変更された時に発火するイベント。コンストラクタ内で自動的に `SyncGraphToCode()` に購読される。

## メソッド

### コンストラクタ

```csharp
public AstGraphView()
```

**説明**: AstGraphView を初期化。

**実装詳細**:
1. スタイル設定: `style.flexGrow = 1`（親要素に応じて拡大）
2. Manipulator を登録:
   - `ContentZoomer`: マウスホイールでズーム
   - `ContentDragger`: 中ボタンドラッグで pan
   - `SelectionDragger`: ノード選択状態でドラッグ移動
   - `RectangleSelector`: ドラッグで複数選択
3. `GridBackground` を追加して視認性向上
4. `OnGraphDataChanged` に `SyncGraphToCode` を購読
5. `NodeCodeDataEventBus.Instance.OnCodeUpdated` に `OnCodeUpdated` を購読

---

### OnCodeUpdated(string code)

```csharp
public void OnCodeUpdated(string code)
```

- **アクセス修飾子**: public
- **戻り値**: void
- **説明**: C# ソースコードを受け取り、グラフ全体を再構築する。外部からは `NodeCodeDataEventBus.OnCodeUpdated` イベント経由でコール される。
- **引数**:
  - `code` (`string`): 再構築対象の C# ソースコード
- **処理フロー**:
  1. `DeleteElements(graphElements.ToList())` で既存のノード・エッジを削除
  2. `CodeToNodeConverter.CsharpToConvertedTree(code)` で ConvertedNode ツリーを生成
  3. ツリーが null なら早期終了
  4. Pass 1: `CreateNodesRecursive()` で AstNode を生成し GUID マップに登録
  5. Pass 2: `ConnectEdgesRecursive()` でエッジを接続
  6. デバッグログで処理概要を出力

---

### CreateNodesRecursive(ConvertedNode converted, Dictionary<string, AstNode> guidMap, Vector2 basePos, int depth)

```csharp
private void CreateNodesRecursive(
    ConvertedNode converted,
    Dictionary<string, AstNode> guidMap,
    Vector2 basePos,
    int depth
)
```

- **アクセス修飾子**: private
- **戻り値**: void
- **説明**: 深さ優先探索 (DFS) で ConvertedNode ツリーから AstNode を再帰的に生成。
- **引数**:
  - `converted` (`ConvertedNode`): 処理対象のノード（Self と FieldChildren を持つ）
  - `guidMap` (`Dictionary<string, AstNode>`): GUID → AstNode の対応表（エッジ接続用）
  - `basePos` (`Vector2`): グラフ配置の基準位置
  - `depth` (`int`): 再帰深さ（自動レイアウト計算用）
- **処理詳細**:
  1. `new AstNode(converted.Self)` で ノード生成
  2. 自動レイアウト: `pos = basePos + new Vector2(depth * 350, guidMap.Count * 120)`
  3. `node.SetPosition()` で位置設定
  4. `AddElement(node)` で GraphView に追加
  5. GUID マップに登録: `guidMap[converted.Self.Guid] = node`
  6. `node.OnNodeDataChanged += OnGraphDataChanged` でイベント購読
  7. `node.RegisterCallback<DetachFromPanelEvent>()` でクリーンアップハンドラ登録
  8. `converted.FieldChildren` を再帰処理

**注意事項**:
- 自動レイアウトは簡易的。複雑な大規模グラフは手動配置が必要になる可能性あり。
- ノード削除時のイベントハンドラ購読解除で メモリリーク防止。

---

### ConnectEdgesRecursive(ConvertedNode converted, Dictionary<string, AstNode> guidMap, StringBuilder sb)

```csharp
private void ConnectEdgesRecursive(
    ConvertedNode converted,
    Dictionary<string, AstNode> guidMap,
    StringBuilder sb
)
```

- **アクセス修飾子**: private
- **戻り値**: void
- **説明**: ConvertedNode ツリーの親子関係を GraphView エッジに変換して接続。
- **引数**:
  - `converted` (`ConvertedNode`): 処理対象ノード
  - `guidMap` (`Dictionary<string, AstNode>`): ノード検索用マップ
  - `sb` (`StringBuilder`): デバッグログ用
- **処理詳細**:
  1. `converted.FieldChildren == null` なら早期終了
  2. GUID マップから親ノードを検索（見つからなければ警告ログ）
  3. 各フィールド名ごとに親の outputPort を取得
  4. 子ノード群を走査：
     - 子の GUID からノードを検索
     - `outputPort.ConnectTo(childNode.InputPort)` でエッジ生成
     - `AddElement(edge)` で GraphView に追加
  5. 子ノード群に対して再帰呼び出し

**エッジの重要性**:
- エッジの接続順序（インデックス）がコード上の子要素の出力順序を決定（NodeToCodeConverter で活用）。

---

### GetRootNodes()

```csharp
public AstNode[] GetRootNodes()
```

- **アクセス修飾子**: public
- **戻り値**: `AstNode[]` — 入力エッジを持たないノードの配列
- **説明**: グラフ内の「ルート」ノード（親を持たないノード）を特定。通常、CompilationUnitSyntax ノードが唯一のルート。
- **処理詳細**:
  1. `connectedInputs` HashSet にすべての入力エッジのターゲット（子）を登録
  2. グラフの全ノードから `connectedInputs` に含まれないものをフィルタ
  3. 配列で返却

---

### SyncGraphToCode()

```csharp
public void SyncGraphToCode()
```

- **アクセス修飾子**: public
- **戻り値**: void
- **説明**: 現在のグラフ状態を C# コードに逆変換し、EventBus 経由で配信。ノード編集時に自動的にコール される。
- **処理フロー**:
  1. `GetRootNodes()` でルートノード配列を取得
  2. `NodeToCodeConverter.NodeMetasToCSharp()` でコード生成
  3. `NodeCodeDataEventBus.Instance.UpdateNode(code)` で配信
  4. SourceController など が このイベントを受け取り UI 更新

---

### GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)

```csharp
public override List<Port> GetCompatiblePorts(
    Port startPort,
    NodeAdapter nodeAdapter
)
```

- **アクセス修飾子**: public override
- **戻り値**: `List<Port>` — 接続可能なポート一覧
- **説明**: GraphView 標準メソッド。ドラッグ中のポートに接続可能なポートを返す。
- **接続ルール**:
  - 自分自身のポートは接続不可
  - 同じノード内のポート同士は接続不可
  - 同じ方向（Input/Output）同士は接続不可
  - それ以外は互換性あり

---

### BuildContextualMenu(ContextualMenuPopulateEvent evt)

```csharp
public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
```

- **アクセス修飾子**: public override
- **戻り値**: void
- **説明**: GraphView 上で右クリック時のコンテキストメニューをビルド。
- **メニュー項目**:
  - "Add AST Node" — マウス位置に新しいノードを追加

---

### AddNode(Vector2 position)

```csharp
private void AddNode(Vector2 position)
```

- **アクセス修飾子**: private
- **戻り値**: void
- **説明**: 指定位置に新しい空の AstNode を追加。コンテキストメニューからコール される。
- **処理詳細**:
  1. `new AstNode()` で デフォルトノード生成
  2. 位置設定
  3. GraphView に追加
  4. イベントハンドラ登録
  5. `OnGraphDataChanged` をトリガ

**用途**: ユーザーが手動でノードを追加する際に使用。既定で最初のスキーマノード型が選ばれる。

## 使用例・連携フロー

### シナリオ 1: C# ファイルを読み込んでグラフを表示

```
1. ユーザーが SourceController でファイル選択
2. MonoScript.text を取得
3. NodeCodeDataEventBus.UpdateCode(code) コール
   ↓
4. AstGraphView.OnCodeUpdated(code) 実行
   a. 既存グラフ削除
   b. CodeToNodeConverter でツリー生成
   c. CreateNodesRecursive() で ノード生成・配置
   d. ConnectEdgesRecursive() で エッジ接続
   ↓
5. グラフ完成。ユーザーが見える
```

### シナリオ 2: ノードを編集してコードに同期

```
1. ユーザーが AstNode フィールド編集
2. AstNode.OnNodeDataChanged イベント発火
3. AstGraphView.OnGraphDataChanged() 実行
   ↓
4. SyncGraphToCode() 実行
   a. GetRootNodes() でルート検索
   b. NodeToCodeConverter.NodeMetasToCSharp() でコード生成
   c. NodeCodeDataEventBus.UpdateNode(code) 配信
   ↓
5. SourceController.UpdateSourcePreview() で コード表示更新
```

## 設計上の注意点

### 1. グラフの「真実」はエッジの接続順序

```csharp
// ❌ ノード自体に子リストを保持しない
class WrongNode { List<Node> children; }

// ✅ エッジのみが親子関係を定義
// edge.output.node = parent
// edge.input.node = child
// 複数エッジの場合、接続順序（インデックス）で子要素順序を決定
```

### 2. ConvertedNode は一時的

```
ConvertedNode ツリー
    ↓ (CreateNodesRecursive)
AstNode グラフ + エッジ
    ↓ (NodeToCodeConverter)
C# コード

グラフが「真実」。ツリーは変換バッファに過ぎない。
```

### 3. UI イベントの慎重な登録・解除

デタッチ時にハンドラを解除しないと、削除済みノードへの参照が残る可能性。

```csharp
node.RegisterCallback<DetachFromPanelEvent>(_ =>
{
    node.OnNodeDataChanged -= changeHandler;
});
```

### 4. 自動レイアウトの限界

現在の配置ロジック:
```csharp
var pos = basePos + new Vector2(depth * 350, guidMap.Count * 120);
```

大規模グラフではノード重複や見づらさが発生。手動レイアウトエンジン導入を検討。

### 5. デバッグログの活用

`OnCodeUpdated`, `CreateNodesRecursive`, `ConnectEdgesRecursive` は検索条件にマッチしない場合に警告ログを出力。

```csharp
sb.AppendLine($"[Warning] Parent node not found in map: {converted.Self.Name}");
```

問題デバッグ時に Console で確認。

## パフォーマンス特性

| 操作 | 計算量 | 説明 |
|---|---|---|
| `OnCodeUpdated()` | O(n + m) | n=ノード数, m=エッジ数 |
| `CreateNodesRecursive()` | O(n) | 各ノード 1 度のみ訪問 |
| `ConnectEdgesRecursive()` | O(m) | 各フィールド・子を 1 度訪問 |
| `GetRootNodes()` | O(n + m) | 全ノード・エッジ走査 |
| `SyncGraphToCode()` | O(n + m) | NodeToCodeConverter 内で O(1) ルックアップ活用 |

**大規模ファイル（1000+ ノード）対応**:
- グラフ再構築自体は高速
- UI 描画がボトルネック（Unity の UIElements レンダリング）
- 必要に応じて フォーカスエリアのみ描画、ノードの非表示化を検討

## 関連ファイル

- `AstNode.cs` — ノード実装
- `CodeToNodeConverter.cs` — コード → ツリー変換
- `NodeToCodeConverter.cs` — グラフ → コード変換
- `NodeCodeDataEventBus.cs` — EventBus
- `Architecture.md` — 全体設計
