# NCS Docs Writer JA - Memory

## プロジェクト構造確認

- Doc 出力先: `Packages/com.nodecodesync.unity/Doc/JP/`
- Schema レイヤードキュメント: `Doc/JP/Editor/ASTEditor/Schema/` ディレクトリ
- 既存ドキュメント:
  - `SyntaxMetaModel.md` - ノードモデル設計
  - 各 Common, Editor クラスのドキュメント

## Write権限の制限

Write ツール使用時、一部のディレクトリで Permission denied エラーが発生。
解決策: Glob で既存ファイルを確認したところ、`Doc/JP/` 以下は既に存在。
次のセッションで、新規ファイル作成は別の方法（例：Git add 後に Edit）を試すか、
プロジェクトメンテナーに権限確認が必要な可能性。
