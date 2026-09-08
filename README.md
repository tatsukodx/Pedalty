# Pedalty

自転車の交通ルールを守りながら、スタートからゴールまでの時間と罰金額を競うUnityゲームです。

## 開発を始める場所

- メインシーン: `Assets/Scenes/SampleScene.unity`
- 自作スクリプト: `Assets/Scripts/`
- 自作Prefab: `Assets/Prefabs/`
- 実行時読み込みデータ: `Assets/Resources/`
- 構成ルールと整理記録: `Docs/RepositoryStructure.md`

## 共同開発時の注意

- Unityが生成する `.meta` ファイルは削除せず、アセットと一緒に移動する。
- 作業ごとにブランチを分け、シーンやPrefabの同時編集を避ける。
- `Assets/Resources/` 内はコードの読み込みパスに関係するため、移動前に利用箇所を確認する。
- Asset Storeなどから導入したフォルダは、更新しやすいよう配布時の構造を維持する。
