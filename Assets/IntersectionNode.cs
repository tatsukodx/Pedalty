using UnityEngine;
using System.Collections; // コルーチンを使うために必要

public class IntersectionNode : MonoBehaviour
{
    [Header("ボックスに触れてから実際に曲がるまでの時間（秒）")]
    public float delayTime = 0.5f; // 交差点の大きさに合わせてUnity上で調整してください

    private void OnTriggerEnter(Collider other)
    {
        NPCWalker walker = other.GetComponent<NPCWalker>();
        if (walker != null)
        {
            // コルーチンを起動して、遅延処理を裏でスタートさせる
            StartCoroutine(TurnWithDelay(walker, other.transform));
        }
    }

    // 遅延して曲がるためのコルーチン
    private IEnumerator TurnWithDelay(NPCWalker walker, Transform npcTransform)
    {
        // 1. まず現在の向きをベースに、曲がる方向を「先に計算」しておく
        //（遅延した後に計算すると、すでにズレた向きを基準にしてしまうため）
        Vector3 currentDir = npcTransform.forward;
        Vector3 rightDir = Quaternion.Euler(0, 90, 0) * currentDir;
        Vector3 leftDir = Quaternion.Euler(0, -90, 0) * currentDir;

        Vector3 nextDirection = currentDir; 
        int choice = Random.Range(0, 3);

        switch (choice)
        {
            case 0:
                nextDirection = currentDir; // 直進
                break;
            case 1:
                nextDirection = rightDir;   // 右折
                break;
            case 2:
                nextDirection = leftDir;    // 左折
                break;
        }

        // 2. 指定した秒数（delayTime）だけ、ここで処理を待機する
        yield return new WaitForSeconds(delayTime);

        // 3. 待機が終わった時点で、NPCがまだ存在していれば（途中で消滅していなければ）方向を指示する
        if (walker != null)
        {
            walker.SetDirection(nextDirection);
        }
    }
}
