using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    private List<GameObject> validNpcModels = new List<GameObject>();
    private float timer;

    void Start()
    {
        GameObject[] allObjects = Resources.LoadAll<GameObject>("NPC_Models");
        
        foreach (GameObject obj in allObjects)
        {
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

            int currentNPCCount = FindObjectsByType<NPCWalker>().Length;

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

        GameObject newNPC = Instantiate(baseNpcPrefab, spawnPoint.position, spawnPoint.rotation);

        // NPCWalker（＝Rigidbody/Collider/Animatorも一緒に乗っている本体）は
        // ルート直下ではなく子オブジェクトにあるため、GetComponentInChildrenで探す
        NPCWalker walker = newNPC.GetComponentInChildren<NPCWalker>();

        // 見た目のモデルだけをランダムに差し替える
        if (validNpcModels.Count > 0 && walker != null)
        {
            int randomModelIndex = Random.Range(0, validNpcModels.Count);
            GameObject chosenModel = validNpcModels[randomModelIndex];

            // 差し替え対象は「NPCWalkerが付いている本体オブジェクト」自身ではなく、
            // その中の見た目（メッシュ・ボーン）だけにする
            StartCoroutine(ReplaceVisualCoroutine(walker.gameObject, chosenModel));
        }

        if (walker != null && randomPointIndex < moveDirections.Length)
        {
            walker.SetDirection(moveDirections[randomPointIndex]);
        }
    }

    IEnumerator ReplaceVisualCoroutine(GameObject bodyObj, GameObject newModelPrefab)
    {
        // bodyObj自体（NPCWalker/Rigidbody/Collider/Animatorが乗っているオブジェクト）は消さず、
        // その中の古い見た目とボーンだけを破棄する
        int childCount = bodyObj.transform.childCount;
        GameObject[] childrenToDelete = new GameObject[childCount];
        
        for (int i = 0; i < childCount; i++)
        {
            childrenToDelete[i] = bodyObj.transform.GetChild(i).gameObject;
        }

        foreach (GameObject child in childrenToDelete)
        {
            DestroyImmediate(child);
        }

        // 破棄が完了するまで1フレーム待つ
        yield return null;

        if (bodyObj == null) yield break;

        GameObject visual = Instantiate(newModelPrefab, bodyObj.transform);
        visual.transform.localPosition = Vector3.zero;
        
        // モデル固有の初期回転を捨て、ベースの正面に合わせる
        visual.transform.localRotation = Quaternion.identity;

        Animator childAnimator = visual.GetComponent<Animator>();
        Animator baseAnimator = bodyObj.GetComponent<Animator>();
        
        if (childAnimator != null && baseAnimator != null)
        {
            // 差し替えたモデルの骨構造をベース側のAnimatorへ引き継ぐ
            baseAnimator.avatar = childAnimator.avatar;
            childAnimator.enabled = false;
            baseAnimator.Rebind();
        }
    }

    public void ResetSpawnedNPCs()
    {
        timer = 0f;
        StopAllCoroutines();

        NPCWalker[] spawnedNPCs = FindObjectsByType<NPCWalker>();
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc != null)
            {
                Destroy(npc.gameObject);
            }
        }
    }
}