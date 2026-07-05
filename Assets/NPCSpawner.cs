using UnityEngine;

public class NPCSpawner : MonoBehaviour
{
    [Header("生成するNPCのプレハブ")]
    public GameObject npcPrefab;

    [Header("NPCが出現するポイント（道路の切れ目など）")]
    public Transform[] spawnPoints;

    [Header("NPCが進む方向（各出現ポイントに対応させる）")]
    public Vector3[] moveDirections;

    [Header("何秒ごとにNPCを生成するか")]
    public float spawnInterval = 3f;

    [Header("街に同時に存在できる最大人数")]
    public int maxNPCCount = 15;

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0f;

            // 現在街にいるNPCの数を数える
            int currentNPCCount = GameObject.FindObjectsOfType<NPCWalker>().Length;

            // 最大人数未満なら生成する
            if (currentNPCCount < maxNPCCount && spawnPoints.Length > 0)
            {
                SpawnNPC();
            }
        }
    }

    void SpawnNPC()
    {
        // 出現ポイントをランダムに1つ選ぶ
        int randomIndex = Random.Range(0, spawnPoints.Length);
        Transform spawnPoint = spawnPoints[randomIndex];

        // NPCを生成
        GameObject newNPC = Instantiate(npcPrefab, spawnPoint.position, spawnPoint.rotation);

        // 移動スクリプトを取得して、進む方向を教えてあげる
        NPCWalker walker = newNPC.GetComponent<NPCWalker>();
        if (walker != null && randomIndex < moveDirections.Length)
        {
            walker.SetDirection(moveDirections[randomIndex]);
        }
    }
}