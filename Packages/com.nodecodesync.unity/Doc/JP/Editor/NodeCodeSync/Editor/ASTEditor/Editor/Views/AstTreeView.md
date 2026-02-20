# AstTreeView

## 概要
Roslyn AST をテキストベースの階層構造で表示するデバッグ用ビュー。SourceController が解析した CompilationUnitSyntax をツリー形式に変換し、コード構造の可視化をサポートします。

## 継承・実装
- 実装インターフェース: `IDisposable`

## 責務
- AST 階層の視覚化：Roslyn SyntaxNode ツリーをインデント付きテキストで表示
- Trivia（コメント）の抽出：単行・複数行・ドキュメントコメントを表示
- イベント駆動更新：NodeCodeDataEventBus.OnCodeCompilationUnitSyntaxUpdated をリッスンして自動更新
- リソース管理：Dispose でイベント登録を確実にアンサブスクライブ

## 依存コンポーネント
| コンポーネント | 役割 |
|---|---|
| `NodeCodeDataEventBus` | OnCodeCompilationUnitSyntaxUpdated イベントをリッスン |
| `CompilationUnitSyntax` (Roslyn) | ツリー走査対象の AST ルート |
| `ScrollView` (UIElements) | スクロール可能なテキスト表示領域 |
| `Label` (UIElements) | ツリーテキストの表示 |

## フィールド・プロパティ

### _root
- **型**: `VisualElement`
- **アクセス修飾子**: private
- **説明**: ルートコンテナ。タイトル・ScrollView・Label を包含

### _treeLabel
- **型**: `Label`
- **アクセス修飾子**: private
- **説明**: インデント済みツリーテキストを保有。WhiteSpace.Pre で等幅フォント表示、isSelectable で選択可能

### Root
- **型**: `VisualElement`（property）
- **アクセス修飾子**: public
- **説明**: UI ルート要素。MainCenterWindow などの親コンテナに追加される

## メソッド

### OnCompilationUnitUpdated(compilationUnit)
- **アクセス修飾子**: private
- **戻り値**: void
- **説明**: イベントハンドラ。CompilationUnitSyntax を受け取り、ツリー文字列を再構築して _treeLabel に設定
- **引数**:
  - `compilationUnit` (CompilationUnitSyntax): Roslyn が解析した CompilationUnit
- **注意**: null の場合は "(empty unit)" を表示

### BuildTreeRecursive(sb, node, depth)
- **アクセス修飾子**: private
- **戻り値**: void
- **説明**: SyntaxNode を深さ優先で走査し、インデント付きツリー文字列を生成。ChildTokens() から LeadingTrivia を検索してコメントを抽出
- **引数**:
  - `sb` (StringBuilder): 文字列構築対象
  - `node` (SyntaxNode): 走査対象
  - `depth` (int): インデント深さ（2 スペース単位）

### IsCommentTrivia(kind)
- **アクセス修飾子**: private
- **戻り値**: bool
- **説明**: SyntaxKind が SingleLineCommentTrivia / MultiLineCommentTrivia / ドキュメントコメント関連かを判定

### Dispose()
- **アクセス修飾子**: public
- **説明**: OnCodeCompilationUnitSyntaxUpdated をアンサブスクライブしてクリーンアップ

## 使用例・連携フロー

```
MainCenterWindow が AstTreeView インスタンスを生成
  ↓
コンストラクタで:
  - Root VisualElement（タイトル + ScrollView）を構築
  - NodeCodeDataEventBus.OnCodeCompilationUnitSyntaxUpdated += OnCompilationUnitUpdated
     ↓
SourceController が code を Roslyn 解析
  → NodeCodeDataEventBus.UpdateCodeCompilationUnitSyntax(cuSyntax)
     ↓
OnCompilationUnitUpdated() が発火
  → BuildTreeRecursive() で SyntaxNode を再帰走査
  → _treeLabel.text に設定
     ↓
ユーザが ScrollView でツリーをスクロール・選択
```

## 設計上の注意点
- **デバッグ専用**：node graph 編集には影響なし。純粋に read-only な表示機能
- **等幅表示**：WhiteSpace.Pre でインデント構造を視覚的に保証
- **大規模ファイルの性能**：再帰的走査のため、極めて大きな AST ではパフォーマンスに注意
- **イベントライフサイクル**：Dispose() で確実にアンサブスクライブしてメモリリークを防止
