using UnityEngine;
using System.Collections.Generic;

public class CarSpawner : MonoBehaviour
{
    public GameObject baseCarPrefab;
    public GameObject[] carModels;
    public Transform[] spawnPoints;
    public Vector3[] moveDirections;
    public float spawnInterval = 4f;
    public int maxCarCount = 10;

    [Header("スポーン地点の占有チェック半径")]
    public float spawnCheckRadius = 4f;

    float timer;
    private List<CarController> spawnedCars = new List<CarController>();

    void Start()
    {
        // 起動時に carModels の null チェック
        for (int i = 0; i < carModels.Length; i++)
        {
            if (carModels[i] == null)
                Debug.LogWarning($"[CarSpawner] carModels[{i}] が null です！インスペクターを確認してください。");
        }
    }

    void Update()
    {
        spawnedCars.RemoveAll(car => car == null);

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;

            if (spawnedCars.Count < maxCarCount
                && spawnPoints.Length > 0
                && baseCarPrefab != null
                && carModels.Length > 0)
            {
                SpawnCar();
            }
        }
    }

    void SpawnCar()
    {
        // 空いているスポーンポイントを探す
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (!IsSpawnPointOccupied(spawnPoints[i].position))
                availableIndices.Add(i);
        }

        if (availableIndices.Count == 0)
        {
            Debug.Log("[CarSpawner] 空きスポーンポイントなし");
            return;
        }

        int index = availableIndices[Random.Range(0, availableIndices.Count)];
        Transform spawnPoint = spawnPoints[index];

        // ── baseCarPrefab を生成 ──
        GameObject newCar = Instantiate(baseCarPrefab, spawnPoint.position, spawnPoint.rotation);

        // ── 車モデルを選択（null を除外） ──
        GameObject model = GetRandomValidModel();
        if (model == null)
        {
            Debug.LogError("[CarSpawner] 有効な carModel がありません！インスペクターを確認してください。");
            Destroy(newCar); // ← 幽霊車にならないよう即削除
            return;
        }

        GameObject visual = Instantiate(model, newCar.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        // ── CarController に方向をセット ──
        CarController controller = newCar.GetComponent<CarController>();
        if (controller != null)
        {
            spawnedCars.Add(controller);

            if (index < moveDirections.Length)
                controller.SetDirection(moveDirections[index]);
            else
                Debug.LogWarning($"[CarSpawner] moveDirections[{index}] がありません。spawnPoints と同じ数だけ設定してください。");
        }

        Debug.Log($"[CarSpawner] スポーン成功 → 現在 {spawnedCars.Count} 台");
    }

    /// <summary>carModels からnullを除いてランダムに返す</summary>
    GameObject GetRandomValidModel()
    {
        List<GameObject> validModels = new List<GameObject>();
        foreach (var m in carModels)
        {
            if (m != null) validModels.Add(m);
        }
        if (validModels.Count == 0) return null;
        return validModels[Random.Range(0, validModels.Count)];
    }

    bool IsSpawnPointOccupied(Vector3 position)
    {
        Collider[] hits = Physics.OverlapSphere(position, spawnCheckRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit.GetComponentInParent<CarController>() != null)
                return true;
        }
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (spawnPoints == null) return;
        foreach (var sp in spawnPoints)
        {
            if (sp == null) continue;
            bool occupied = Application.isPlaying && IsSpawnPointOccupied(sp.position);
            Gizmos.color = occupied ? new Color(1f, 0f, 0f, 0.4f)
                                    : new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(sp.position, spawnCheckRadius);
        }
    }
#endif
}