# GraphVE

## 概要
AstGraphView をラップする UI コンテナクラス。ヘッダーセクション付きの構造化ルート要素を提供し、MainCenterWindow 内で node graph 編集機能を統合します。

## 継承・実装
- なし（スタンドアロンラッパークラス）

## 責務
- AstGraphView インスタンスの生成・保有
- ルート VisualElement の構築・レイアウト管理
- ヘッダーセクション（タイトル・将来的なツールバー用スペース）の提供
- 親コンテナ（MainCenterWindow）への統合用の Root 要素を公開

## 依存コンポーネント
| コンポーネント | 役割 |
|---|---|
| `AstGraphView` | 実際の node graph 編集を担当 |
| `VisualElement` (UIElements) | UI ルートコンテナ |
| `Label` (UIElements) | ヘッダータイトル表示 |

## フィールド・プロパティ

### GraphView
- **型**: `AstGraphView`（property）
- **アクセス修飾子**: public
- **説明**: node graph の実装を担当する AstGraphView インスタンス

### Root
- **型**: `VisualElement`（property）
- **アクセス修飾子**: public
- **説明**: UI 全体のルート要素。ヘッダーと GraphView を包含し、MainCenterWindow に追加される

## メソッド

### コンストラクタ
- **アクセス修飾子**: public
- **説明**: Root VisualElement を構築し、header セクション + AstGraphView を配置

**構成フロー**:
1. Root VisualElement を生成（flexGrow = 1 で親に対してフルサイズ）
2. Header VisualElement を生成（FlexDirection.Row、SpaceBetween justify）
3. タイトル Label "AST Graph Canvas" をヘッダーに追加
4. Header を Root に追加
5. AstGraphView() を生成して Root に追加（ヘッダー下に配置）

## 使用例・連携フロー

```
MainCenterWindow 初期化時:
  ↓
graphVE = new GraphVE()
  └─ Root（flexGrow=1）
     ├─ Header（Row, SpaceBetween）
     │   └─ Label "AST Graph Canvas"
     └─ AstGraphView（flexGrow=1 で残りスペースをフィル）
        ↓
MainCenterWindow は graphVE.Root を SplitView 内に追加
        ↓
ユーザがノード編集
  → AstGraphView.OnGraphDataChanged 発火
  → NodeToCodeConverter で C# に逆変換
        ↓
Code 変更検知
  → NodeCodeDataEventBus.UpdateCode(code)
  → AstGraphView.OnCodeUpdated() でグラフ再構築
```

## 設計上の注意点
- **ラッパーの単純性**：GraphVE は layout と header の提供に徹し、graph 編集ロジックは AstGraphView に完全委譲
- **ヘッダー拡張性**：FlexDirection.Row の Row layout により、将来のツールバーボタン追加が容易
- **Root の再利用**：一度構築後に MainCenterWindow がそのまま利用。再構築・入れ替えは想定していない
- **Dispose なし**：AstGraphView 側でイベントのアンサブスクライブなど cleanup を実装する必要がある
