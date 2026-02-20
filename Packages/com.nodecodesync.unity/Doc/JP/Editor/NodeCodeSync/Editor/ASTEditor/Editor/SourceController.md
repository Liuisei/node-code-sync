# SourceController

## 概要

`SourceController`はC#ソースコード入力と同期を管理するコントローラです。C#ファイル選択、ノードグラフ生成のトリガー、およびグラフ編集後の出力コードの実時間プレビュー表示を担当します。MainCenterWindow内のSourcePreview ペインを実装しており、IDisposableを実装してメモリリーク防止を行います。

## 継承・実装

- 継承元: なし
- 実装インターフェース: `IDisposable`

## 責務

- C#スクリプト選択UI（ObjectField）の管理
- 選択されたファイルの元のソースコード表示（読み取り専用）
- "Generate Node Graph" ボタンによるグラフ生成トリガー
- ノードグラフ編集後の同期ソースプレビュー表示
- イベントバス購読の明示的な解除（メモリリーク防止）

## 依存コンポーネント

| コンポーネント | 役割 |
|---|---|
| `NodeCodeDataEventBus` | コード更新とノード更新イベントを通じた疎結合通信 |
| `NodeToCodeConverter` | グラフからC#コードへの逆変換（ノード→コード） |
| `MonoScript` (Unity) | C#スクリプトアセット参照 |
| `VisualElement` (UIElements) | UI要素の構築 |

## フィールド・プロパティ

### Root (public)

- **型**: `VisualElement`
- **アクセス修飾子**: public get
- **説明**: SourceController全体のUI を収容するルート VisualElement。MainCenterWindow から直接 Split View に追加されます。padding、flexGrow、flexDirection 等のスタイルが事前に設定されています。

### _root (private)

- **型**: `VisualElement`
- **説明**: Root プロパティのバッキングフィールド。フレックスレイアウト設定済み。

### fileField (private)

- **型**: `ObjectField`
- **説明**: ターゲットC#ファイル選択用UI。`MonoScript` 型に制限されており、シーンオブジェクトは許可しません（allowSceneObjects = false）。

### originalCodeField (private)

- **型**: `TextField`
- **説明**: 選択されたC#ファイルの元のソースコード表示。読み取り専用（isReadOnly = true）で、複数行表示対応。

### sourcePreviewField (private)

- **型**: `TextField`
- **説明**: ノードグラフ編集後に生成されたC#コードのプレビュー表示。読み取り専用で、ノード更新イベント（OnNodeUpdated）により動的に更新されます。

## メソッド

### コンストラクタ()

- **アクセス修飾子**: public
- **説明**: SourceController インスタンス化とUI初期化。全てのTextFieldやButtonを構築し、イベント購読を設定します。
- **処理フロー**:

  1. **Root VisualElement 初期化**
     - flexGrow = 1（親に沿って拡大）
     - padding = (6, 6, 4, 4)
     - flexDirection = Column（縦方向レイアウト）

  2. **ファイル選択セクション**
     - ObjectField "Target C# File" を作成（MonoScript 型のみ）
     - RegisterValueChangedCallback() で値変更時の処理:
       - 新しい MonoScript が選択されたら originalCodeField.value に text を設定
       - `NodeToCodeConverter.CSharpToAST()` で Roslyn CompilationUnitSyntax を生成
       - `NodeCodeDataEventBus.Instance.UpdateCodeCompilationUnitSyntax()` でイベント発火（AST デバッガ更新用）

  3. **アクション セクション**
     - Button "Generate Node Graph" を作成
     - クリック時に `GanerateButtonClicked()` を呼び出し

  4. **元のソースコードビュー**
     - Label "Original Source Code"（太字）
     - ScrollView内に TextField（multiline, isReadOnly, whiteSpace=Pre）
     - ファイル選択時に autopopulate

  5. **同期ソースプレビュー**
     - Label "Synchronized Source Preview"（太字）
     - ScrollView内に TextField（multiline, isReadOnly, whiteSpace=Pre）
     - ノード更新時に `UpdateSourcePreview()` で動的更新

  6. **イベントバス購読**
     - `NodeCodeDataEventBus.Instance.OnNodeUpdated += UpdateSourcePreview` で購読開始

### GanerateButtonClicked() (private)

- **アクセス修飾子**: private
- **戻り値**: `void`
- **説明**: "Generate Node Graph" ボタンクリック時のコールバック。現在のソースコード（originalCodeField.value）をイベントバス経由で配信し、CodeToNodeConverter による グラフ生成をトリガーします。
- **処理内容**:
  1. Debug.Log で "[NodeCodeSync] Triggering Graph Generation from Source." を出力
  2. `NodeCodeDataEventBus.Instance.UpdateCode(originalCodeField.value)` を呼び出し
  3. OnCodeUpdated イベント発火 → CodeToNodeConverter が購読して グラフ生成を実行

### UpdateSourcePreview(string nodeMetas) (private)

- **アクセス修飾子**: private
- **戻り値**: `void`
- **説明**: NodeCodeDataEventBus の OnNodeUpdated イベントを購読する コールバック。ノードグラフが編集されるたびに、再構築されたC#コードを sourcePreviewField に反映します。
- **引数**:
  - `nodeMetas` (string): グラフから再構築されたC#ソースコード、またはシリアル化されたノードメタデータ文字列
- **処理内容**:
  - `sourcePreviewField.value = nodeMetas` で UI を更新
  - このメソッドが呼ばれることで、ユーザーが「グラフ編集→コード出力」の リアルタイム同期を視覚化できます

### Dispose() (public)

- **アクセス修飾子**: public
- **戻り値**: `void`
- **説明**: IDisposable インターフェース実装。ウィンドウクローズ時に MainCenterWindow.OnDisable() から呼び出され、イベント購読を明示的に解除してメモリリークを防止します。
- **処理内容**:
  1. NodeCodeDataEventBus.Instance が null でないことを確認
  2. `NodeCodeDataEventBus.Instance.OnNodeUpdated -= UpdateSourcePreview` で購読解除
- **重要**: Dispose を呼ばないと、ウィンドウを何度も開き閉じした場合、UpdateSourcePreview が蓄積してメモリリークが発生します。

## 使用例・連携フロー

### シーン1: ファイル選択からグラフ生成まで

```
1. SourceController UI が表示される
2. ユーザーが ObjectField "Target C# File" をクリック
3. プロジェクト内のC#スクリプト（MonoScript）を選択
   → fileField.RegisterValueChangedCallback() が発火
4. originalCodeField.value = script.text で読み込み
5. NodeToCodeConverter.CSharpToAST() で Roslyn パース
6. UpdateCodeCompilationUnitSyntax() でイベント配信
   → AstTreeView（Debug Mode）が構文ツリーを表示
7. ユーザーが "Generate Node Graph" ボタンをクリック
   → GanerateButtonClicked() 実行
8. UpdateCode() でイベント発火
   → CodeToNodeConverter が グラフ生成を実行
   → AstGraphView にノードが表示される
```

### シーン2: グラフ編集による同期プレビュー更新

```
1. ユーザーが AstGraphView でノード属性を編集
   （例: 変数名 "x" → "y"）
2. グラフ変更イベントが発火
3. NodeToCodeConverter.NodeMetasToCSharp() で新しいコードを生成
4. NodeCodeDataEventBus.Instance.UpdateNode(newCode) を呼び出し
   → OnNodeUpdated イベント発火
5. SourceController.UpdateSourcePreview(newCode) が呼ばれる
6. sourcePreviewField.value が新しいコードに更新される
7. ユーザーは即座に「グラフ変更→コード出力」を確認できる
```

### シーン3: ObjectField での段階的な更新

```
1. ユーザーが fileField で スクリプトA を選択
   → originalCodeField にスクリプトA のコード表示
   → UpdateCodeCompilationUnitSyntax() で AST デバッガ更新
2. "Generate Node Graph" クリック
   → AstGraphView にスクリプトA のグラフ表示
3. グラフ編集
   → sourcePreviewField で編集結果の コード表示
4. 別のスクリプトB を fileField で選択
   → originalCodeField が スクリプトB に置き換え
   → sourcePreviewField は未更新（スクリプトB の グラフ生成前）
5. "Generate Node Graph" クリック
   → スクリプトB のグラフが新規に表示
   → sourcePreviewField が新規コードで更新可能
```

## 設計上の注意点

### UIElements Flexレイアウト

Root、各セクションのラベル、ScrollView、TextField は全て flexGrow 等のフレックスプロパティで構成されており、MainCenterWindow 内での Split View リサイズに応答します。

```csharp
_root.style.flexGrow = 1;           // 親に沿って拡大
originalScroll.style.flexGrow = 1;  // 複数スクロール領域も同様
previewScroll.style.flexGrow = 1;
```

### イベント駆動による疎結合

SourceController は CodeToNodeConverter や AstGraphView を直接参照しません。すべて NodeCodeDataEventBus 経由のイベント（OnCodeUpdated、OnNodeUpdated、OnCodeCompilationUnitSyntaxUpdated）で通信します。

```
UpdateCode(code)
   ↓ (イベント発火)
OnCodeUpdated
   ↓
CodeToNodeConverter が購読して グラフ生成実行
AstGraphView が購読して ノード描画実行
```

これにより、新しい処理フェーズを追加する際に SourceController 自体の変更が不要です。

### 読み取り専用フィールド

originalCodeField と sourcePreviewField は両方とも isReadOnly = true で、ユーザーによる直接編集を禁止しています。これは以下を保証：
- 元のコード（originalCodeField）は選択されたスクリプトの内容を信頼できるソースとして保持
- プレビュー（sourcePreviewField）はグラフ構造から逆算生成されたコードを表示

UI から直接コード編集を許可すると、グラフ↔コード の同期が複雑になるため、UI は「表示」「入力トリガー」に限定されています。

### UpdateSourcePreview の呼び出しタイミング

`OnNodeUpdated` イベントの購読により、UpdateSourcePreview は以下の場合に呼ばれます：
- ユーザーが グラフ のノード属性を編集
- ユーザーが ノード間をポート接続
- 外部システムが NodeToCodeConverter を呼び出してコード再生成

これにより、グラフ編集のあらゆる形態がプレビュー更新に反映されます。

### Dispose パターン

MainCenterWindow.OnDisable() 内での sourceView.Dispose() 呼び出しにより、イベント購読の確実な解除が保証されます。Unity Editor 環境では長時間のセッションで複数回のウィンドウ開き閉じが発生するため、このパターンは重要です。

```csharp
// MainCenterWindow.OnDisable()
if (sourceView != null)
{
    sourceView.Dispose();  // OnNodeUpdated購読を解除
}
```

## XMLドキュメントコメントからの設計推論

ソースコード内の XMLドキュメントコメント（`///`）より：
- 「controller responsible for managing source code input and synchronization」→ 入力・同期の責務を強調
- 「real-time preview of the source code generated from the node graph」→ リアルタイム同期が設計目標
- 「triggers graph generation」→ グラフ生成トリガーはこのクラスの責務

これらは「コードが真実」というNCS設計原則を支える、ユーザーインターフェース層の実装です。
