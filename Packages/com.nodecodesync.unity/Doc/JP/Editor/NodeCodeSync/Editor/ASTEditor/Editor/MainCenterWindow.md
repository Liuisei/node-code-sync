# MainCenterWindow

## 概要

`MainCenterWindow`はNodeCodeSync AST エディタの主要な`EditorWindow`です。GraphView（ノードグラフ編集キャンバス）、SourceController（ソースコード管理）、AstTreeView（構文ツリーデバッガ）を統合し、応答性の高いマルチペインレイアウトで提供します。Debug Mode による条件付きレイアウト切り替え機能も備えています。

## 継承・実装

- 継承元: `EditorWindow` (Unity Editor)
- 実装インターフェース: `IHasCustomMenu` (Unity Editor)

## 責務

- NCS Editor ウィンドウのメインエントリーポイント（メニュー項目 "NodeCodeSync/Open AST Editor"）
- 各UI層コンポーネント（`GraphVE`、`SourceController`、`AstTreeView`）のライフサイクル管理
- マルチペインレイアウト構築（通常モード / Debug モード）
- Debug Mode 設定の永続化（EditorPrefs）
- ウィンドウクローズ時のクリーンアップ（イベント購読解除）

## 依存コンポーネント

| コンポーネント | 役割 |
|---|---|
| `GraphVE` | AST Graph Canvas（ノード編集UI）の主要ラッパー |
| `AstGraphView` | 実際のGraphView実装（GraphVE が内部に保持） |
| `SourceController` | C#ファイル選択、ソースプレビュー管理 |
| `AstTreeView` | Roslyn 構文ツリーの構造表示（Debug Mode時のみ表示） |
| `TwoPaneSplitView` | Unity UIElements のマルチペインレイアウト |
| `EditorPrefs` | Debug Mode フラグの永続化 |

## フィールド

### graphView (private)

- **型**: `GraphVE`
- **説明**: AST Graph Canvas コンポーネント。ノードの追加、編集、ポート接続を行うUI。

### astView (private)

- **型**: `AstTreeView`
- **説明**: Roslyn 構文ツリーをテキスト形式で表示するDebugビュー。通常モードでは非表示。

### sourceView (private)

- **型**: `SourceController`
- **説明**: C#ファイル選択、ソースコード表示、プレビュー表示を管理するコンポーネント。

### mainSplitView (private)

- **型**: `TwoPaneSplitView`
- **説明**: 水平方向のメインスプリットビュー。GraphVE と（通常モードでは sourceView、Debug モードでは debugSplitView）を分割配置。

### debugSplitView (private)

- **型**: `TwoPaneSplitView`
- **説明**: 水平方向のセカンダリスプリットビュー。Debug Mode時のみ使用され、AstTreeView と SourceController を分割配置。

### isDebugMode (private)

- **型**: `bool`
- **デフォルト**: false
- **説明**: Debug Mode の有効/無効フラグ。EditorPrefs に永続化されます。

### DEBUG_MODE_KEY (private const)

- **型**: `string`
- **値**: `"ASTEditorDebugMode"`
- **説明**: EditorPrefs キー。Debug Mode フラグの保存/読み込みに使用。

## メソッド

### Open() (public static)

- **アクセス修飾子**: public static
- **戻り値**: `void`
- **説明**: Unity メニューから "NodeCodeSync/Open AST Editor" を選択して呼び出されるメニュー関数。新しい MainCenterWindow インスタンスを起動し、ウィンドウの最小サイズを設定します。
- **属性**: `[MenuItem("NodeCodeSync/Open AST Editor")]`
- **処理内容**:
  1. `GetWindow<MainCenterWindow>()` で新規ウィンドウを生成（同名ウィンドウが既に開いていれば既存インスタンスを返す）
  2. ウィンドウタイトル `"NodeCodeSync AST Editor"` を設定
  3. 最小サイズを `Vector2(800, 450)` に設定
- **使用例**: Unity Editor の トップメニュー → NodeCodeSync → Open AST Editor をクリック

### OnEnable() (private)

- **アクセス修飾子**: private（Unity lifecycle callback）
- **戻り値**: `void`
- **説明**: ウィンドウが開かれる際に Unity が自動呼び出します。EditorPrefs から Debug Mode フラグを読み込み、全UI層コンポーネントを初期化し、レイアウトを構築します。
- **処理内容**:
  1. `EditorPrefs.GetBool(DEBUG_MODE_KEY, false)` で前回のDebug Mode 状態を復元
  2. `GraphVE`、`SourceController`、`AstTreeView` インスタンス化
  3. Root VisualElement に対して列方向FlexLayout を設定
  4. `TwoPaneSplitView` (メイン、デバッグ用) を生成
  5. `BuildLayout()` を呼び出してレイアウト構築

### BuildLayout() (private)

- **アクセス修飾子**: private
- **戻り値**: `void`
- **説明**: 現在の `isDebugMode` 状態に基づいてUI レイアウトを再構築します。ウィンドウ起動時と Debug Mode トグル時の両方で呼び出されます。
- **処理内容**:
  - **通常モード** (`isDebugMode = false`):
    ```
    Layout: [ GraphCanvas (1200px) | SourcePreview (残り) ]
    ```
    mainSplitView に GraphVE.Root と SourceController.Root を追加。

  - **Debug モード** (`isDebugMode = true`):
    ```
    Layout: [ GraphCanvas (1200px) | [ AstTreeView (200px) | SourcePreview (残り) ] ]
    ```
    debugSplitView に AstTreeView.Root と SourceController.Root を追加し、その debugSplitView を mainSplitView に追加。

### AddItemsToMenu(GenericMenu menu) (IHasCustomMenu実装)

- **アクセス修飾子**: void (explicit interface)
- **戻り値**: `void`
- **説明**: ウィンドウ右上の「kebab menu」（⋯ アイコン）にカスタムメニュー項目を追加します。Debug Mode を簡単にトグルでき、UI をクラッター化しません。
- **処理内容**:
  1. `menu.AddItem()` で "Debug Mode" 項目を追加
  2. チェックマーク表示 = `isDebugMode` の現在値
  3. クリック時のコールバック:
     - `isDebugMode` を反転
     - `EditorPrefs.SetBool()` で設定を永続化
     - `BuildLayout()` を呼び出してレイアウトを即座に切り替え
- **UI効果**: ウィンドウ右上のメニューボタン → "Debug Mode" をクリック → AstTreeView の表示/非表示が切り替わる

### OnDisable() (private)

- **アクセス修飾子**: private（Unity lifecycle callback）
- **戻り値**: `void`
- **説明**: ウィンドウがクローズされる際に Unity が自動呼び出します。イベント購読解除やメモリリーク防止のため、`SourceController` と `AstTreeView` の `Dispose()` を呼び出し、参照をnullクリアします。
- **処理内容**:
  1. `sourceView?.Dispose()` — OnNodeUpdated イベント購読を解除
  2. `astView?.Dispose()` — OnCodeCompilationUnitSyntaxUpdated イベント購読を解除
  3. 全コンポーネント参照 (graphView, astView, sourceView, mainSplitView, debugSplitView) を null に設定して GC対象化

## 使用例・連携フロー

### フロー1: NCS Editor ウィンドウの起動から初期表示まで

```
1. Unity メニュー: NodeCodeSync → Open AST Editor クリック
   → MainCenterWindow.Open() 呼び出し
2. GetWindow<MainCenterWindow>() が新規インスタンス生成
3. Unity が自動的に OnEnable() を呼び出し
4. EditorPrefs から isDebugMode を読み込み（デフォルト false）
5. GraphVE, SourceController, AstTreeView をインスタンス化
6. BuildLayout() で通常モード UI構築
7. ウィンドウが表示: [ GraphCanvas | SourcePreview ]
```

### フロー2: Debug Mode の有効化

```
1. ウィンドウ右上 ⋯ メニュー → "Debug Mode" クリック
2. IHasCustomMenu.AddItemsToMenu() のコールバック実行
3. isDebugMode = true に切り替え
4. EditorPrefs.SetBool("ASTEditorDebugMode", true) で永続化
5. BuildLayout() 再実行
6. debugSplitView を構築、AstTreeView と SourceController を左右に配置
7. mainSplitView 更新: [ GraphCanvas | debugSplitView ]
8. AstTreeView がRoslyn 構文ツリーを表示開始
```

### フロー3: ウィンドウクローズ時のクリーンアップ

```
1. ウィンドウクローズ
2. Unity が OnDisable() を自動呼び出し
3. sourceView.Dispose() → OnNodeUpdated から購読解除
4. astView.Dispose() → OnCodeCompilationUnitSyntaxUpdated から購読解除
5. 全参照を null 化
6. GC がコンポーネントを回収（メモリリークなし）
```

## 設計上の注意点

### レスポンシブレイアウト設計

`TwoPaneSplitView` の `fixedPaneInitialDimension` で初期サイズを固定することで、異なる画面サイズやウィンドウサイズリサイズ時でも、GraphCanvas に十分な領域を確保しながら SourceController を表示できます。

```csharp
mainSplitView.fixedPaneInitialDimension = 1200;   // GraphCanvas: 1200px
// 残り: SourcePreview (自動拡大)

debugSplitView.fixedPaneInitialDimension = 200;   // AstTreeView: 200px
// 残り: SourcePreview (自動拡大)
```

### Debug Mode の条件付きレイアウト

`isDebugMode` フラグで、AstTreeView（Roslyn 構文デバッガ）の表示/非表示を動的に制御します。これにより：
- 通常ユーザーは Graph + Source の2ペインで作業（シンプル）
- デバッグ時は Graph + Tree + Source の3ペイン（詳細な構造確認可能）

### EditorPrefs による永続化

Debug Mode フラグは EditorPrefs に保存され、ウィンドウを閉じても設定が保持されます。これはUnity開発者の利便性向上が目的です。

```csharp
// 読み込み（OnEnable）
isDebugMode = EditorPrefs.GetBool("ASTEditorDebugMode", false);

// 保存（AddItemsToMenu）
EditorPrefs.SetBool("ASTEditorDebugMode", isDebugMode);
```

### Dispose パターンによるメモリ安全性

`SourceController` と `AstTreeView` は `IDisposable` インターフェース実装（または Dispose メソッド提供）で、イベント購読の明示的な解除を可能にしています。Unity Editor環境でのメモリリーク（特に長時間実行時のイベント蓄積）を防ぐため重要です。

```csharp
// OnDisable 内での呼び出し
if (sourceView != null)
    sourceView.Dispose();
if (astView != null)
    astView.Dispose();
```

### 最小サイズ制約

`window.minSize = new Vector2(800, 450)` により、ウィンドウ縮小時の UI崩れを防止します。これは GraphCanvas の最小必要幅と SourcePreview の可読性を考慮した値です。
