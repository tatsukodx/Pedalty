using UnityEngine;
using System.Collections;

public class IntersectionNode : MonoBehaviour
{
    [Header("ボックスに触れてから実際に曲がるまでの時間（秒）")]
    public float delayTime = 0.5f;

    [Header("この交差点の信号機マネージャー（省略可）")]
    public TrafficLightManager manager;

    [Header("この交差点の車側ノード（省略可、横断歩道のロック状態を確認するために使用）")]
    public CarIntersectionNode carIntersectionNode;

    [Header("横断判定用の前方確認距離")]
    public float crossingProbeDistance = 2.5f;
    public float crossingProbeRadius = 0.5f;

    public enum SignalType { Normal, BicyclePedestrian }

    [Header("信号タイプ（BicyclePedestrianの場合のみ、横断前後に横断歩道の位置まで往復移動する）")]
    public SignalType signalType = SignalType.Normal;

    [Header("横断歩道が角からずれている場合に、曲がる前に外側へ歩く距離")]
    public float crosswalkApproachDistance = 2.4f;

    [Header("BicyclePedestrianモードで、実際に横断歩道を渡りきるまでの距離")]
    public float crosswalkCrossingDistance = 6f;

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
        Transform crosswalk = null;
        bool useOffsetApproach = false;
        bool offsetAlongX = false;
        float cornerLateralCoord = 0f;

        if (willCross)
        {
            bool crossingNSRoad = Mathf.Abs(nextDirection.x) > Mathf.Abs(nextDirection.z);
            // 角の現在位置ではなく、これから進む方向に少し進んだ地点を基準にすることで、
            // 直進・左折・右折のどれを選んでも実際に渡る横断歩道と一致させる
            Vector3 crossingProbePoint = npcTransform.position + nextDirection.normalized * crossingProbeDistance;
            crosswalk = carIntersectionNode != null ? carIntersectionNode.GetNearestCrosswalk(crossingProbePoint) : null;

            // 横断歩道が角からずれて配置されている交差点(自転車歩行者信号)でのみ、
            // 曲がる前に横断方向(nextDirection)に対して垂直な軸だけ横断歩道の入り口位置まで歩かせる。
            // 普通の信号(Normal)ではこの処理自体を行わない。
            useOffsetApproach = signalType == SignalType.BicyclePedestrian && crosswalk != null;

            if (signalType == SignalType.BicyclePedestrian && crosswalk == null)
            {
                Debug.LogWarning($"[IntersectionNode:{name}] BicyclePedestrianモードですが、対応する横断歩道(crosswalk)が見つかりませんでした。" +
                    $"Car Intersection Nodeの割り当て、またはそちら側のCrosswalk North/South/East/Westの設定を確認してください。", this);
            }

            if (useOffsetApproach)
            {
                // 横断歩道の入口までの横移動自体も、歩道の境界を超える動きになるため、
                // ここから isCrossing を true にして ClampToSidewalk のハードクランプを解除しておく
                walker.SetCrossing(true);
            }

            if (useOffsetApproach)
            {
                offsetAlongX = Mathf.Abs(nextDirection.z) > Mathf.Abs(nextDirection.x);
                cornerLateralCoord = offsetAlongX ? transform.position.x : transform.position.z;

                // 「外側(入口側)」の向きは、実際に使う横断歩道(crosswalk)がノードから見て
                // どちら側にあるかで判定する(距離は使わず符号だけを見るので、Crosswalkの
                // Transformの正確な位置に多少ズレがあっても影響しない)。
                float crosswalkCoordRaw = offsetAlongX ? crosswalk.position.x : crosswalk.position.z;
                float outwardSign = Mathf.Sign(crosswalkCoordRaw - cornerLateralCoord);
                float crosswalkLateralCoord = cornerLateralCoord + outwardSign * crosswalkApproachDistance;

                yield return WalkAlongAxisTo(walker, npcTransform, offsetAlongX, crosswalkLateralCoord);
                if (walker == null) yield break;

                // 横方向への移動で向きが変わっているので、横断方向に戻す
                walker.SetDirection(nextDirection);
            }

            // 信号待ちが必要な場合(manager あり)、または横断歩道のロック判定が必要な場合(carIntersectionNode あり)は
            // 両方の条件がそろうまで待機する
            if (manager != null || carIntersectionNode != null)
            {
                walker.SetTrafficStop(true);

                while (walker != null)
                {
                    bool signalOk = manager == null || IsCarLightAllowingCross(crossingNSRoad);

                    // 対応する横断歩道が車の旋回中でロックされている間は、
                    // 信号が青（歩行者側が進んでよいタイミング）でも進ませない
                    bool crosswalkLocked = carIntersectionNode != null && carIntersectionNode.IsCrosswalkLocked(crosswalk);

                    if (signalOk && !crosswalkLocked) break;
                    yield return null;
                }

                if (walker == null) yield break;

                walker.SetTrafficStop(false);
            }
        }

        // 前後軸(横断方向に平行な軸)の補正。
        // BicyclePedestrianモード(useOffsetApproach)では、横方向は既にWalkAlongAxisToで
        // 正しい位置(横断歩道の入り口)に合わせてあるため、SnapAcrossPathの汎用ロジック
        // (currentDir基準＋横方向への微小ジッター)を使うと、その横方向オフセットを
        // 上書きして角に戻してしまう。そのため、その場合は前後軸だけを個別に角の座標へ合わせる。
        if (useOffsetApproach)
        {
            bool forwardAlongX = !offsetAlongX;
            Vector3 pos = npcTransform.position;
            if (forwardAlongX) pos.x = transform.position.x; else pos.z = transform.position.z;
            npcTransform.position = pos;
        }
        else
        {
            walker.SnapAcrossPath(transform.position, currentDir, nextDirection);
        }
        walker.SetDirection(nextDirection);

        if (willCross && !useOffsetApproach)
        {
            walker.SetCrossing(true);
        }

        if (useOffsetApproach)
        {
            // 固定時間ではなく、実際に横断歩道を渡りきる(前後軸で一定距離進む)まで待つ。
            // これにより、渡りきる前に南北(戻り)移動が始まってしまうことを防ぐ。
            bool forwardAlongX = !offsetAlongX;
            float startForward = forwardAlongX ? npcTransform.position.x : npcTransform.position.z;
            float dirSign = Mathf.Sign(forwardAlongX ? nextDirection.x : nextDirection.z);
            float crossingTarget = startForward + dirSign * crosswalkCrossingDistance;

            yield return WalkAlongAxisTo(walker, npcTransform, forwardAlongX, crossingTarget, maxWaitTime: 10f);
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        if (walker != null && useOffsetApproach)
        {
            // 横断歩道が角からずれていた場合、渡り終えた後に本来の角のライン(次の角のノードと合うライン)まで戻る
            yield return WalkAlongAxisTo(walker, npcTransform, offsetAlongX, cornerLateralCoord);

            // 戻り移動で向きが変わっているので、再度もとの進行方向に戻す
            if (walker != null)
            {
                walker.SetDirection(nextDirection);
            }
        }

        if (walker != null)
        {
            walker.SetAtIntersection(false);
        }
    }

    bool IsCarLightAllowingCross(bool crossingNSRoad)
    {
        bool parallelGreen = (crossingNSRoad ? manager.IsEW_CarGreen : manager.IsNS_CarGreen) && manager.IsPedestrianGreen;
        bool dedicatedPedPhase = manager.CurrentPhase == TrafficLightPhase.Pedestrian_Green;
        return parallelGreen || dedicatedPedPhase;
    }

    // 横断方向(nextDirection)に対して垂直な軸(alongX ? x : z)だけを対象に、
    // targetCoordに到達する(通過する)まで、その軸方向へ明示的に歩かせる。
    // 横断歩道と角のズレ(自転車歩行者信号の入り口までの横移動、渡り終えた後の角への戻り)に使う。
    // 差がほぼ無い(＝通常の交差点)場合は何もせず即終了する。
    IEnumerator WalkAlongAxisTo(NPCWalker walker, Transform npcTransform, bool alongX, float targetCoord, float maxWaitTime = 5f)
    {
        float current = alongX ? npcTransform.position.x : npcTransform.position.z;
        if (Mathf.Abs(targetCoord - current) < 0.05f) yield break;

        float sign = Mathf.Sign(targetCoord - current);
        walker.SetDirection(alongX ? new Vector3(sign, 0f, 0f) : new Vector3(0f, 0f, sign));

        float elapsed = 0f;
        while (walker != null && elapsed < maxWaitTime)
        {
            current = alongX ? npcTransform.position.x : npcTransform.position.z;
            bool reached = sign >= 0f ? current >= targetCoord : current <= targetCoord;
            if (reached) yield break;

            elapsed += Time.deltaTime;
            yield return null;
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