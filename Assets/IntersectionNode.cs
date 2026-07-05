using UnityEngine;

public class IntersectionNode : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        NPCWalker walker = other.GetComponent<NPCWalker>();
        if (walker != null)
        {
            // 1. NPCが現在進んでいる「正面の向き」を取得
            Vector3 currentDir = other.transform.forward;

            // 2. 現在の向きを基準に、右折ベクトルと左折ベクトルを作る（Y軸回転）
            Vector3 rightDir = Quaternion.Euler(0, 90, 0) * currentDir;
            Vector3 leftDir = Quaternion.Euler(0, -90, 0) * currentDir;

            // 3. 0, 1, 2 の3つの数字をランダムで選ぶ（確率1/3ずつ）
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

            // 4. NPCに新しく計算した方向を指示する
            walker.SetDirection(nextDirection);
        }
    }
}