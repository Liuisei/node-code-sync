# RoslynXmlLoader

## 概要
Roslyn シンタックススキーマ XML（ThirdParty/Roslyn/RoslynSyntax.xml）を Unity リソースから読み込み、XDocument として返却する静的ローダー。スキーマレイヤー全体の「起点」として機能します。

## 継承・実装
- `public static`（静的ユーティリティクラス）

## 責務
- Unity Resources フォルダから RoslynSyntax.xml を TextAsset としてロード
- TextAsset の text プロパティから XML 文字列を抽出
- XML 文字列を XDocument に parse する
- ロード失敗時に Debug エラーログを出力
- parse 例外をキャッチして詳細ログを記録

## 依存コンポーネント
| コンポーネント | 役割 |
|---|---|
| `UnityEngine.Resources` | TextAsset の動的ロード |
| `System.Xml.Linq.XDocument` | XML パースと DOM オブジェクト化 |
| `UnityEngine.Debug` | エラーログ出力 |
| RoslynSyntax.xml | 第三者ライブラリ（Roslyn 公式スキーマ） |

## フィールド・定数

### RoslynSyntaxResourcePath
- **型**: `string` const
- **値**: `"ThirdParty/Roslyn/RoslynSyntax"`
- **アクセス修飾子**: private const
- **説明**: Unity Resources フォルダ内の相対パス（`.xml` 拡張子は Resources.Load が自動追加）

## メソッド

### Load()
- **アクセス修飾子**: public static
- **戻り値**: `XDocument`（成功時）/ `null`（失敗時）
- **説明**: Roslyn XML を Unity リソースからロードして XDocument に parse する
- **処理フロー**:
  1. `Resources.Load<TextAsset>(RoslynSyntaxResourcePath)` で TextAsset を取得
  2. null の場合: `Debug.LogError()` → `return null`
  3. TextAsset.text から XML 文字列を抽出
  4. `XDocument.Parse()` で DOM に変換して返却
- **例外処理**:
  - `System.Exception` をキャッチ
  - `Debug.LogError()` + `Debug.LogException(ex)` で詳細ログ
  - 呼び出し元に null を返す（throw しない）

## 使用例・連携フロー

```
RoslynSchemaCache.Build()
  ↓
RoslynXmlLoader.Load()
  ↓
Resources.Load<TextAsset>("ThirdParty/Roslyn/RoslynSyntax")
  ↓
XDocument.Parse(textAsset.text)
  ↓
SyntaxMetaParser.Parse(xdoc)
  ↓
NCSSyntaxTree → キャッシュ化
```

## エラーハンドリング

| シナリオ | 動作 |
|---|---|
| TextAsset が見つからない | Debug.LogError → return null |
| XML parse エラー | Debug.LogError + Debug.LogException → return null |

## 設計上の注意点
- **リソースパス一元管理**: const で定義し、パス変更時の修正箇所を最小化
- **例外の非 throw**: エラーは Debug ログに記録され、呼び出し元には null を返す
- **Unity エディター専用**: `Resources.Load` は Unity Editor でのみ機能。ビルド済みアプリでは別途リソース配置が必要
- **null 安全性**: 戻り値 null の場合、呼び出し元（RoslynSchemaCache.Build）で NullReferenceException が発生しうる点に注意
