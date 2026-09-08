# リポジトリ構成ルール

## このリポジトリに入っているもの

| パス | 入っているもの |
| --- | --- |
| `Assets/` | Unityで使用するシーン、スクリプト、Prefab、画像、音声、モデル、設定、第三者アセット |
| `arduino_program/` | 実物自転車のボタンとセンサーを扱うArduinoプログラム、端末ごとの設定雛形 |
| `Docs/` | 開発者向けの構成ルール、調査結果、運用資料 |
| `Packages/` | Unity Package Managerで導入するパッケージの一覧と固定バージョン |
| `ProjectSettings/` | タグ、レイヤー、物理、描画、Build SettingsなどUnityプロジェクト全体の設定 |
| `settings.sample.json` | Arduino接続とハンドル校正値の設定例。個人用の`settings.json`はGit管理外 |
| `.gitignore` | `Library`、`Temp`、`Logs`、個人設定などをGitへ含めないための規則 |
| `.gitattributes` | Git上でのファイルの扱いに関する設定 |
| `.vsconfig` | Visual Studioへ導入するUnity開発コンポーネントの設定 |
| `README.md` | プロジェクトを初めて開く人向けの入口 |

## Assetsに入っているもの

| パス | 現在の内容 |
| --- | --- |
| `Assets/Animations/NPC/` | `NPC_Animator.controller`、予備の`LOL_Dance.controller` |
| `Assets/Art/Models/Bicycle Models/` | プレイヤー用の自転車モデル、Prefab、マテリアル、テクスチャ、配布元サンプルシーン |
| `Assets/Art/Models/Road/` | アスファルト・歩道・自転車レーン用マテリアルと道路Prefabの編集元 |
| `Assets/Art/Textures/RoadSurfaces/` | 車道、自転車レーン、歩道の路面テクスチャ |
| `Assets/Audio/` | 自転車ベルの効果音 |
| `Assets/Fonts/DotGothic16/` | ゲーム画面用フォント、TextMesh Pro用フォントアセット、ライセンス |
| `Assets/Images/` | 自転車マーク、矢羽根、ゴールピンの元画像 |
| `Assets/Materials/` | 自転車マーク、矢羽根、ゴールピン、信号ランプの自作マテリアル |
| `Assets/Prefabs/BicycleLane/` | 自転車レーン模様を連続配置するPrefab |
| `Assets/Prefabs/NPC/` | NPCの基本Prefab |
| `Assets/Prefabs/Road/` | 本編シーンで使用中の交差点と道路区間Prefab |
| `Assets/Prefabs/Cars.prefab` | 一般車両のPrefab |
| `Assets/Prefabs/Traffic ramp/` | 道路用ランプのPrefab |
| `Assets/Resources/NPC_Models/` | `NPCSpawner`が実行時にランダム読み込みする6種類のNPC Prefab |
| `Assets/Resources/Data/` | 上位30件のプレイ記録を保持するランキングDBの初期データ |
| `Assets/Resources/UI/` | ベル、ブレーキ、ルール説明用マップの実行時読み込み画像 |
| `Assets/Resources/Violations/` | 違反名、説明、罰金額を定義する`violations.json` |
| `Assets/Scenes/SampleScene.unity` | 現在のゲーム本編シーン。Build Settingsに登録済み |
| `Assets/Scripts/` | Pedaltyで作成したC#コード。詳細は次節に記載 |
| `Assets/Settings/Input/` | Input Systemのアクション設定 |
| `Assets/Settings/`直下 | URP、Renderer、Volume Profileなど描画設定 |

## 導入済み第三者アセット

| パス | 内容 |
| --- | --- |
| `Assets/Kevin Iglesias/` | 人型キャラクター用アニメーション、デモ、配布資料 |
| `Assets/npc_casual_set_00/` | NPCモデル一式と配布元デモ |
| `Assets/SimplePoly City - Low Poly Assets/` | 街並み、建物、道路、小物の3Dアセット |
| `Assets/TextMesh Pro/` | TextMesh Pro標準リソース |
| `Assets/UnityRoadMaster/` | 道路作成用アセットとサンプルシーン |
| `Assets/TutorialInfo/` | UnityテンプレートのReadme表示機能 |

## 自作スクリプトの内容

| パス | 入っているファイルと役割 |
| --- | --- |
| `Assets/Scripts/Core/` | `AppSettings.cs`: 外部設定の読み込み、`GameDebugMode.cs`: 違反判定なしのデバッグ状態 |
| `Assets/Scripts/Input/` | `ArduinoConnection.cs`: Arduino通信、`InputManager.cs`: ボタンとキーボード入力の統合、`SteeringCalibrator.cs`: ハンドル角度の校正 |
| `Assets/Scripts/Gameplay/` | `GameTimer.cs`: タイム計測、`GoalTrigger.cs`: ゴール到着判定、`Ranking/`: ランキング記録と上位30件の保存 |
| `Assets/Scripts/Player/` | `BicycleController.cs`: 自転車移動、`CameraController.cs`: カメラ追従、`BellController.cs`: ベル、`HandleAngleConverter.cs`: ハンドル入力変換、`WheelSpeedConverter.cs`: 車輪速度変換、`PlayerLaneDetector.cs`: 走行場所判定、`TrafficViolationDetector.cs`: 違反判定 |
| `Assets/Scripts/Traffic/Vehicles/` | `CarController.cs`: 一般車両移動、`CarSpawner.cs`: 車両生成、`CarIntersectionNode.cs`: 交差点進路、`CarYieldManager.cs`: 対向車との譲り合い |
| `Assets/Scripts/Traffic/Pedestrians/` | `NPCWalker.cs`: NPC歩行、`NPCSpawner.cs`: NPC生成、`IntersectionNode.cs`: 歩行者の交差点進路、`PedestrianStopZone.cs`: 歩行者停止ゾーン候補 |
| `Assets/Scripts/Traffic/Signals/` | `TrafficLight.cs`: ランプ表示、`TrafficLightManager.cs`: 信号サイクル、`TrafficLightPhase.cs`: 信号状態定義、`TrafficStopZone.cs`: 車両停止ゾーン |
| `Assets/Scripts/UI/` | `GameFlowUI.cs`: 開始・ルール・終了画面、`FineDisplayUI.cs`: 罰金総額、`PenaltyController.cs`: 違反画面と加算、`BellIndicatorUI.cs`: ベル表示、`BrakeIndicatorUI.cs`: ブレーキ表示、`GoalDirectionIndicator.cs`: ゴール方向と距離、`GoalMarkerAnimation.cs`: ゴールピン表示、`DigitalDisplayFont.cs`: HUDフォント |
| `Assets/Scripts/World/` | `BuildingCollisionSetup.cs`: 建物コライダー生成、`RoadEndBoundarySystem.cs`: 道路端の壁と警告、`RoadEndBoundaryTrigger.cs`: 境界接触判定 |

## Arduinoプログラムの内容

| パス | 内容 |
| --- | --- |
| `arduino_program/ard_sinmple-button.cpp/ard_sinmple-button.cpp.ino` | ボタンとセンサー入力をUnityへ送るArduinoスケッチ |
| `arduino_program/ard_sinmple-button.cpp/config.h` | Arduino側のピンや動作設定 |

## Assets直下のルール

| フォルダ | 用途 |
| --- | --- |
| `Animations/` | 自作Animator Controllerとアニメーション設定 |
| `Art/` | ゲーム用モデル、テクスチャ、モデル付属マテリアル |
| `Audio/` | BGMと効果音 |
| `Fonts/` | ゲームへ同梱するフォントとライセンス |
| `Images/` | UI画像と路面表示などの2D画像 |
| `Materials/` | 自作マテリアル |
| `Prefabs/` | ゲームで使用する自作Prefab |
| `Resources/` | `Resources.Load`で実行時に読み込むファイル。読み込みパスを確認せず移動しない |
| `Scenes/` | ゲーム本編のシーン |
| `Scripts/` | 自作C#コード |
| `Settings/` | Input SystemやURPなどのプロジェクト設定アセット |

## Scriptsのルール

| フォルダ | 用途 | 主な内容 |
| --- | --- | --- |
| `Core/` | ゲーム全体で共有する状態・設定 | アプリ設定、デバッグモード |
| `Input/` | Arduinoとキーボード入力 | 接続、入力変換、ハンドル校正 |
| `Gameplay/` | 開始からゴールまでの進行 | タイマー、ゴール判定 |
| `Player/` | 自転車本体とプレイヤー検出 | 移動、カメラ、レーン・違反検出 |
| `Traffic/Vehicles/` | 一般車両 | 車の走行、生成、交差点制御 |
| `Traffic/Pedestrians/` | 歩行者 | NPCの生成、歩行、交差点制御 |
| `Traffic/Signals/` | 信号設備 | 信号状態、停止ゾーン |
| `UI/` | ゲーム画面の表示 | HUD、開始・終了画面、違反表示 |
| `World/` | マップ上の仕組み | 建物コライダー、道路端境界 |

## ランキングDBの運用

- Unity Editorでは`Assets/Resources/Data/ranking_database.json`へ記録する。このファイルはGit管理対象で、必要な記録をGitHubへ共有できる。
- ビルド版では書き込み権限のある`Application.persistentDataPath`へ記録する。
- 保存対象はクリアタイム順の上位30件。順位が同じ場合は罰金額、違反回数、プレイ日時の順で比較する。
- DBファイルを手動編集するときはUnityを停止し、JSON形式と`records`配列を崩さない。

## 命名ルール

- 自作フォルダとC#ファイルは英語のPascalCaseを使う。
- C#のファイル名と主要クラス名を一致させる。
- 画像や音声は用途が分かる英語名を使い、同じ名前の別ファイルを複数の場所に置かない。
- Unityの予約フォルダである `Resources`、`Editor`、`StreamingAssets`、`Plugins` は用途を理解して使用する。
- 第三者アセットの名前は変更しない。

## 現在の未使用・重複候補

| 候補 | 調査結果 | 削除前に確認すること |
| --- | --- | --- |
| `Assets/Images/BicycleLane/bicycle_lane_chevron.png` | `Assets/Images/bicycle_lane_chevron.png`と内容が完全一致し、前者は参照0件 | 自転車レーン作成時の予備画像として必要か |
| `Assets/Images/BicycleLane/bicycle_lane_symbol.png` | 参照0件。使用中の同名画像とは内容が異なる | 別デザインの予備として必要か |
| `Assets/Animations/NPC/LOL_Dance.controller` | シーン・Prefabから参照0件 | NPCのデバッグ用ダンスとして必要か |
| `Assets/Scripts/Traffic/Pedestrians/PedestrianStopZone.cs` | シーン・Prefabから参照0件で、コードからの動的生成もない | 今後、歩行者信号の停止判定に使う予定があるか |
| `Assets/Art/Models/Road/Prefabs/Intersection_01.prefab` | 参照0件。現在のシーンは`Assets/Prefabs/Road/Intersection_01.prefab`を使用 | 道路Prefabの編集元として残すか |
| `Assets/Art/Models/Road/Prefabs/RoadSection_01.prefab` | 参照0件。現在のシーンは`Assets/Prefabs/Road/RoadSection_01.prefab`を使用 | 道路Prefabの編集元として残すか |
| `Assets/_Recovery/*.unity` | Build Settingsに含まれない復旧用シーンが5件 | 必要な復旧履歴を別途保存済みか |
| `Assets/Readme.asset`と`Assets/TutorialInfo/` | Unityテンプレートの案内機能。ゲーム本編からは未使用 | 初参加者向けのUnity内案内を残すか |
| 第三者アセット内の`.zip`、`.rar`、デモシーン、説明PDF | 実行時には不要 | 再編集用原本やライセンス・説明資料をリポジトリ外へ保管するか |

## 今回実施した整理

- `Assets`直下にあった車両、NPC、信号のスクリプトを`Assets/Scripts/Traffic/`以下へ分類。
- `Maneger`フォルダを廃止し、`Core`、`Input`、`Gameplay`へ役割別に分類。
- `Playerlanedetector.cs`をクラス名と同じ`PlayerLaneDetector.cs`へ修正し、`Scripts/Player/`へ移動。
- 建物コライダーと道路端境界を`Scripts/World/`へ移動。
- NPCのPrefabとAnimator Controllerを、それぞれ`Prefabs/NPC/`と`Animations/NPC/`へ移動。
- 道路Prefabを`Prefabs/Road/`へ移動。
- Input System設定を`Settings/Input/`へ移動。
- `audio`を命名ルールに合わせて`Audio`へ変更。
