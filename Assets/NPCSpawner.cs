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
        
        // アニメーションのみのFBXを除外し、体のメッシュを持つモデルだけを使う
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

        // 見た目のモデルだけをランダムに差し替える
        if (validNpcModels.Count > 0)
        {
            int randomModelIndex = Random.Range(0, validNpcModels.Count);
            GameObject chosenModel = validNpcModels[randomModelIndex];
            
            StartCoroutine(ReplaceVisualCoroutine(newNPC, chosenModel));
        }

        NPCWalker walker = newNPC.GetComponent<NPCWalker>();
        if (walker != null && randomPointIndex < moveDirections.Length)
        {
            walker.SetDirection(moveDirections[randomPointIndex]);
        }
    }

    IEnumerator ReplaceVisualCoroutine(GameObject npcObj, GameObject newModelPrefab)
    {
        // 古い見た目とボーンを破棄する
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

        // 破棄が完了するまで1フレーム待つ
        yield return null;

        if (npcObj == null) yield break;

        GameObject visual = Instantiate(newModelPrefab, npcObj.transform);
        visual.transform.localPosition = Vector3.zero;
        
        // モデル固有の初期回転を捨て、ベースの正面に合わせる
        visual.transform.localRotation = Quaternion.identity;

        Animator childAnimator = visual.GetComponent<Animator>();
        Animator baseAnimator = npcObj.GetComponent<Animator>();
        
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
