using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Listを使うために追加

public class NPCSpawner : MonoBehaviour
{
    [Header("コンポーネント設定済みのベースNPC（これ1つだけでOK！）")]
    public GameObject baseNpcPrefab;

    [Header("NPCが出現するポイント（道路の切れ目など）")]
    public Transform[] spawnPoints;

    [Header("NPCが進む方向（各出現ポイントに対応させる）")]
    public Vector3[] moveDirections;

    [Header("何秒ごとにNPCを生成するか")]
    public float spawnInterval = 3f;

    [Header("街に同時に存在できる最大人数")]
    public int maxNPCCount = 15;

    // ★純粋な3Dモデルだけを格納するリストに変更
    private List<GameObject> validNpcModels = new List<GameObject>();
    private float timer;

    void Start()
    {
        // 1. フォルダ内のデータを一旦すべてロード
        GameObject[] allObjects = Resources.LoadAll<GameObject>("NPC_Models");
        
        // 2. アニメーションデータなどを除外して「本物の3Dモデル（体）」だけを選別
        foreach (GameObject obj in allObjects)
        {
            // 体のメッシュ（SkinnedMeshRenderer）を持っている、または子供に持っているものだけを許可
            if (obj.GetComponentInChildren<SkinnedMeshRenderer>() != null)
            {
                validNpcModels.Add(obj);
            }
        }
        
        if (validNpcModels.Count == 0)
        {
            Debug.LogError("【エラー】Assets/Resources/NPC_Models フォルダの中に、有効な3Dモデル（服や体のあるモデル）が見つかりません！");
        }
        else
        {
            Debug.Log($"【選別成功】{validNpcModels.Count}種類の有効なNPCモデルを認識しました！");
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0f;

            int currentNPCCount = GameObject.FindObjectsOfType<NPCWalker>().Length;

            if (currentNPCCount < maxNPCCount && spawnPoints.Length > 0 && baseNpcPrefab != null)
            {
                SpawnNPC();
            }
        }
    }

    void SpawnNPC()
    {
        int randomPointIndex = Random.Range(0, spawnPoints.Length);
        Transform spawnPoint = spawnPoints[randomPointIndex];

        // 3. 当たり判定などがセット済みの「ベースNPC」を生成
        GameObject newNPC = Instantiate(baseNpcPrefab, spawnPoint.position, spawnPoint.rotation);

        // 4. 選別済みのモデルからランダムに1つチョイスしてすり替え
        if (validNpcModels.Count > 0)
        {
            int randomModelIndex = Random.Range(0, validNpcModels.Count);
            GameObject chosenModel = validNpcModels[randomModelIndex];
            
            StartCoroutine(ReplaceVisualCoroutine(newNPC, chosenModel));
        }

        // 5. 移動方向を指示
        NPCWalker walker = newNPC.GetComponent<NPCWalker>();
        if (walker != null && randomPointIndex < moveDirections.Length)
        {
            walker.SetDirection(moveDirections[randomPointIndex]);
        }
    }

    IEnumerator ReplaceVisualCoroutine(GameObject npcObj, GameObject newModelPrefab)
    {
        // 古い見た目や古いボーンを今すぐ完全に破壊
        int childCount = npcObj.transform.childCount;
        GameObject[] childrenToDelete = new GameObject[childCount];
        
        for (int i = 0; i < childCount; i++)
        {
            childrenToDelete[i] = npcObj.transform.GetChild(i).gameObject;
        }

        foreach (GameObject child in childrenToDelete)
        {
            DestroyImmediate(child);
        }

        // 1フレーム待機して完全に消去されたのを確定
        yield return null;

        if (npcObj == null) yield break;

        // 新しい本物の3Dモデルを子供として生成
        GameObject visual = Instantiate(newModelPrefab, npcObj.transform);
        visual.transform.localPosition = Vector3.zero;
        
        // ★重要：モデル固有の初期回転をリセットし、ベースの向き（正面）に強制的に合わせる
        visual.transform.localRotation = Quaternion.identity;

        // アニメーション（Animator）の骨組みを引き継ぐ
        Animator childAnimator = visual.GetComponent<Animator>();
        Animator baseAnimator = npcObj.GetComponent<Animator>();
        
        if (childAnimator != null && baseAnimator != null)
        {
            baseAnimator.avatar = childAnimator.avatar; // 骨構造を上書き
            childAnimator.enabled = false;             // 子供側のAnimatorはオフ
            baseAnimator.Rebind();                     // アニメーション再バインド
        }
    }
}