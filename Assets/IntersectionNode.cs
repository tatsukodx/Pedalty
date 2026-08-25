using UnityEngine;
using System.Collections;

public class IntersectionNode : MonoBehaviour
{
    [Header("ボックスに触れてから実際に曲がるまでの時間（秒）")]
    public float delayTime = 0.5f;

    [Header("この交差点の信号機マネージャー（省略可）")]
    public TrafficLightManager manager;

    [Header("横断判定用の前方確認距離")]
    public float crossingProbeDistance = 2.5f;
    public float crossingProbeRadius = 0.5f;

    private void OnTriggerEnter(Collider other)
    {
        NPCWalker walker = other.GetComponent<NPCWalker>();
        if (walker != null)
        {
            StartCoroutine(TurnWithDelay(walker, other.transform));
        }
    }

    private IEnumerator TurnWithDelay(NPCWalker walker, Transform npcTransform)
    {
        // 遅延後に計算するとズレた向きが基準になるので、曲がる方向は先に決めておく
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

        yield return new WaitForSeconds(delayTime);

        // 待機中に消滅している可能性があるので確認する
        if (walker == null) yield break;

        walker.SetCrossing(false);

        bool willCross = !IsSidewalkAhead(npcTransform.position, nextDirection);

        if (willCross && manager != null)
        {
            bool crossingNSRoad = Mathf.Abs(nextDirection.x) > Mathf.Abs(nextDirection.z);

            walker.SetTrafficStop(true);

            while (walker != null && (crossingNSRoad ? manager.IsNS_CarGreen : manager.IsEW_CarGreen))
            {
                yield return null;
            }

            if (walker == null) yield break;

            walker.SetTrafficStop(false);
        }

        walker.SetDirection(nextDirection);

        if (willCross)
        {
            walker.SetCrossing(true);
        }
    }

    bool IsSidewalkAhead(Vector3 origin, Vector3 direction)
    {
        Vector3 probePoint = origin + direction.normalized * crossingProbeDistance;
        Collider[] hits = Physics.OverlapSphere(probePoint, crossingProbeRadius, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Sidewalk_L") || hit.CompareTag("Sidewalk_R"))
            {
                return true;
            }
        }

        return false;
    }
}