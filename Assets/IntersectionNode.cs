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
        NPCWalker walker = other.GetComponentInParent<NPCWalker>();
        if (walker != null && !walker.IsAtIntersection)
        {
            walker.SetAtIntersection(true);
            StartCoroutine(TurnWithDelay(walker, walker.transform));
        }
    }

    private IEnumerator TurnWithDelay(NPCWalker walker, Transform npcTransform)
    {
        Vector3 currentDir = npcTransform.forward;
        Vector3 rightDir = Quaternion.Euler(0, 90, 0) * currentDir;
        Vector3 leftDir = Quaternion.Euler(0, -90, 0) * currentDir;

        Vector3 nextDirection = currentDir;
        int choice = Random.Range(0, 3);
        switch (choice)
        {
            case 0: nextDirection = currentDir; break;
            case 1: nextDirection = rightDir;   break;
            case 2: nextDirection = leftDir;    break;
        }

        yield return new WaitForSeconds(delayTime);

        if (walker == null) yield break;

        walker.SetCrossing(false);

        bool willCross = !IsSidewalkAhead(npcTransform.position, nextDirection);

        if (willCross && manager != null)
        {
            bool crossingNSRoad = Mathf.Abs(nextDirection.x) > Mathf.Abs(nextDirection.z);

            walker.SetTrafficStop(true);

            // 許可が出るのは次の2パターンのみ：
            // ① 進行方向と平行な車道の信号が実際に「青」になったとき
            // ② 歩車分離モードの歩行者専用フェーズ（Pedestrian_Green/Blink）になったとき
            // 黄信号中・全赤バッファ中はどちらにも該当しないため、絶対に渡らせない
            while (walker != null)
            {
                bool parallelGreen = crossingNSRoad ? manager.IsEW_CarGreen : manager.IsNS_CarGreen;
                bool dedicatedPedPhase = manager.CurrentPhase == TrafficLightPhase.Pedestrian_Green
                                       || manager.CurrentPhase == TrafficLightPhase.Pedestrian_Blink;

                if (parallelGreen || dedicatedPedPhase) break;
                yield return null;
            }

            if (walker == null) yield break;

            walker.SetTrafficStop(false);
        }

        walker.SnapAcrossPath(transform.position, currentDir, nextDirection);
        walker.SetDirection(nextDirection);

        if (willCross)
        {
            walker.SetCrossing(true);
        }

        yield return new WaitForSeconds(1.5f);

        if (walker != null)
        {
            walker.SetAtIntersection(false);
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