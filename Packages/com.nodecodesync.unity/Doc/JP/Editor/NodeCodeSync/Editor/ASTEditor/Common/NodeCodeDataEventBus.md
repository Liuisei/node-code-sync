# NodeCodeDataEventBus

## 概要

`NodeCodeDataEventBus`はNCSの各UI層（ソースビュー、グラフビュー、ASTデバッガ）を疎結合に保ちながら、C#コードとノードグラフの変更を同期するための中央イベントバスです。Singleton パターンで実装されており、スレッドセーフなインスタンス管理を提供します。

## 継承・実装

- 継承元: なし
- 実装インターフェース: なし

## 責務

- ソースコード更新イベント（`OnCodeUpdated`）の配信
- ノードグラフ変更イベント（`OnNodeUpdated`）の配信
- Roslyn `CompilationUnitSyntax`再解析イベント（`OnCodeCompilationUnitSyntaxUpdated`）の配信
- Singletonライフサイクル管理とスレッドセーフな初期化

## 依存コンポーネント

| コンポーネント | 役割 |
|---|---|
| `SourceController` | コード読み込み時に`OnCodeUpdated`を発行、`OnNodeUpdated`を購読 |
| `AstGraphView` | グラフ変更時に`OnNodeUpdated`を発行 |
| `AstTreeView` | `OnCodeCompilationUnitSyntaxUpdated`を購読して構文ツリーを表示 |
| `CodeToNodeConverter` | `OnCodeUpdated`を購読してグラフ生成を実行 |

## フィールド

### _instance (private static)

- **型**: `NodeCodeDataEventBus`
- **説明**: Singletonインスタンスキャッシュ

### _lock (private static readonly)

- **型**: `object`
- **説明**: スレッドセーフなシングルトン初期化用のロックオブジェクト

## プロパティ

### Instance (public static)

- **型**: `NodeCodeDataEventBus`
- **アクセス修飾子**: public static get
- **説明**: スレッドセーフなSingletonインスタンス取得。初回アクセス時に自動初期化されます。ダブルチェックロック機構により、初期化後の同期オーバーヘッドを最小化しています。

## イベント

### OnCodeUpdated

- **型**: `event Action<string>`
- **説明**: C#ソースコードが更新された時に発火します（ファイル読み込み、ユーザー編集等）。文字列引数はC#ソースコード全体です。
- **購読者**: `CodeToNodeConverter`（グラフ生成をトリガー）、`AstGraphView`（ノードレイアウト更新）
- **発火者**: `SourceController.GanerateButtonClicked()`、UI外部の自動トリガー

### OnNodeUpdated

- **型**: `event Action<string>`
- **説明**: ノードグラフが変更された時に発火します（ノード属性編集、ポート接続変更等）。文字列引数は再構築されたC#ソースコード、またはシリアル化されたノードメタデータです。
- **購読者**: `SourceController.UpdateSourcePreview()`（プレビュー更新）、UI外部の表示更新
- **発火者**: `AstGraphView`（ノード値変更時）、`NodeToCodeConverter`（コード生成完了時）

### OnCodeCompilationUnitSyntaxUpdated

- **型**: `event Action<CompilationUnitSyntax>`
- **説明**: Roslyn `CompilationUnitSyntax`（C#コードの解析結果）が更新された時に発火します。Debug Mode の AST構造表示に使用されます。
- **購読者**: `AstTreeView`（AST構造ツリーの再描画）
- **発火者**: `SourceController`（ファイル選択時）、`CodeToNodeConverter`（解析完了時）

## メソッド

### UpdateCodeCompilationUnitSyntax(CompilationUnitSyntax cuSyntax)

- **アクセス修飾子**: public
- **戻り値**: `void`
- **説明**: Roslyn syntax treeの最新状態をすべてのリスナーにブロードキャストします。ASTデバッガ等、構造表示が必要なコンポーネント向けです。
- **引数**:
  - `cuSyntax` (`CompilationUnitSyntax`): 解析対象のRoslyn CompilationUnit
- **使用例**:
  ```csharp
  var compilationUnit = CSharpSyntaxTree.ParseText(code).GetCompilationUnitRoot();
  NodeCodeDataEventBus.Instance.UpdateCodeCompilationUnitSyntax(compilationUnit);
  ```

### UpdateCode(string code)

- **アクセス修飾子**: public
- **戻り値**: `void`
- **説明**: C#ソースコード全体を`OnCodeUpdated`イベント経由で配信します。通常、ユーザーがC#ファイルを選択したり、ソースエディタで編集した後に呼び出されます。
- **引数**:
  - `code` (string): C#ソースコード全体
- **使用例**:
  ```csharp
  NodeCodeDataEventBus.Instance.UpdateCode(selectedScript.text);
  ```

### UpdateNode(string nodemetaArray)

- **アクセス修飾子**: public
- **戻り値**: `void`
- **説明**: ノードグラフの変更（新規ノード追加、値編集、ポート接続変更等）をすべてのリスナーに通知します。通常、再構築されたC#コードが文字列で渡されます。
- **引数**:
  - `nodemetaArray` (string): シリアル化されたノードメタデータ、または再構築済みC#ソースコード
- **使用例**:
  ```csharp
  var generatedCode = NodeToCodeConverter.NodeMetasToCSharp(roots, graphView);
  NodeCodeDataEventBus.Instance.UpdateNode(generatedCode);
  ```

## 使用例・連携フロー

### シーン1: ファイル選択から初期グラフ生成

```
1. ユーザーが SourceController の ObjectField で C# ファイルを選択
2. SourceController: MonoScript.text を originalCodeField に設定
3. SourceController: UpdateCodeCompilationUnitSyntax() を呼び出し
   → OnCodeCompilationUnitSyntaxUpdated 発火
   → AstTreeView が構文ツリーを表示（Debug Mode）
4. ユーザーが "Generate Node Graph" ボタンをクリック
5. SourceController: UpdateCode(code) を呼び出し
   → OnCodeUpdated 発火
   → CodeToNodeConverter がコード→ノード変換を実行
   → AstGraphView がノード描画
```

### シーン2: グラフ編集からコード再構築への同期

```
1. ユーザーが AstGraphView でノード属性を編集（例: 変数名を "x" → "y"）
2. AstGraphView: OnNodeUpdated イベントを発火
   → SourceController.UpdateSourcePreview() が呼ばれる
3. UpdateSourcePreview は引数で受け取った nodemetaArray（新コード）を
   sourcePreviewField に表示
```

### シーン3: デバッグモードでのAST構造確認

```
1. MainCenterWindow: Debug Mode をオン
   → debugSplitView が AstTreeView を表示
2. ユーザーがファイルを選択
3. SourceController: UpdateCodeCompilationUnitSyntax(cu) を呼び出し
   → OnCodeCompilationUnitSyntaxUpdated 発火
   → AstTreeView.OnCompilationUnitUpdated() が呼び出される
   → Roslyn AST をテキスト形式で表示（変数、メソッド、型の階層）
```

## 設計上の注意点

### スレッドセーフなSingleton実装

`Instance`プロパティはダブルチェックロック機構でスレッドセーフに初期化されます。

```csharp
lock (_lock)
{
    _instance ??= new NodeCodeDataEventBus();
    return _instance;
}
```

Unity Editor環境ではスレッド競合の可能性は低いですが、バックグラウンド解析が将来実装される際に備えています。

### イベント駆動による疎結合設計

各UI層（`SourceController`、`AstGraphView`、`AstTreeView`）は直接相互呼び出しを行わず、すべてイベントバス経由で通信します。これにより：
- UI層の追加・削除が容易（例: 新しいPreview形式を追加する場合、`OnNodeUpdated`を購読するだけ）
- テスト容易性（イベント発火を模擬化可能）
- 処理の順序を明確に（イベント配信順序で制御）

### 文字列型の汎用性

`OnCodeUpdated`、`OnNodeUpdated`、`OnCodeCompilationUnitSyntaxUpdated`のうち、前2つが文字列を引数に取るのは、様々なペイロード形式に対応するためです。呼び出し側は、再構築されたC#コード、シリアル化されたメタデータ、パラメータ情報等、必要に応じて文字列に含めることができます。ただし、型安全性が低下するため、将来的にジェネリック化の検討余地があります。

### 購読解除の重要性

`SourceController`と`AstTreeView`は`Dispose()`メソッドで明示的にイベント購読を解除します。これはUnity Editor環境でのメモリリーク防止が目的です。

```csharp
public void Dispose()
{
    NodeCodeDataEventBus.Instance.OnNodeUpdated -= UpdateSourcePreview;
}
```

`MainCenterWindow.OnDisable()`内で`sourceView.Dispose()`、`astView.Dispose()`を呼び出し、ウィンドウクローズ時に確実に購読解除を行います。
