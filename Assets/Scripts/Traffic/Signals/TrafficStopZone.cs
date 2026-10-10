using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrafficStopZone : MonoBehaviour
{
    [Header("この停止ゾーンを管理する交差点マネージャー（信号、省略可）")]
    public TrafficLightManager manager;

    [Header("この停止ゾーンの方向（信号用）")]
    [Tooltip("true = 南北方向の車が通る停止線 / false = 東西方向の車が通る停止線")]
    public bool isNSDirection = true;

    [Header("対向車線との譲り合い（省略可）")]
    public CarYieldManager yieldManager;

    [Header("直交方向の車の通過待ち")]
    [Tooltip("この停止ゾーンの先にある交差点。空なら一番近い CarIntersectionNode を自動で使う")]
    public CarIntersectionNode intersectionNode;

    [Header("停止線")]
    [Tooltip("車の先端がゾーンの出口側の端（停止線）をこの距離[m]以上越えていたら、走っている車は止めずに通過させる。越えた後に止めると、横断歩道や交差点にはみ出した位置で止まってしまうため")]
    public float commitDistance = 1.5f;

    private readonly List<CarController> carsInZone = new List<CarController>();

    private readonly Dictionary<CarController, Coroutine> waitingCars = new Dictionary<CarController, Coroutine>();

    private Collider zoneCollider;
    private bool nodeSearched;

    // 一番近い交差点（停止ゾーンは交差点のすぐ手前にあるので、40m以内に限る）
    private CarIntersectionNode Node
    {
        get
        {
            if (intersectionNode == null && !nodeSearched)
            {
                nodeSearched = true;
                float best = 40f;
                foreach (CarIntersectionNode node in FindObjectsByType<CarIntersectionNode>(FindObjectsSortMode.None))
                {
                    Vector3 d = node.transform.position - transform.position;
                    d.y = 0f;
                    if (d.magnitude < best)
                    {
                        best = d.magnitude;
                        intersectionNode = node;
                    }
                }
            }
            return intersectionNode;
        }
    }

    private bool ComputeIsLaneA(Vector3 dir)
    {
        bool isNSAxis = Mathf.Abs(dir.x) < Mathf.Abs(dir.z);
        return isNSAxis ? dir.z >= 0f : dir.x >= 0f;
    }

    void Update()
    {
        if (manager == null) return;

        // 黄・赤のときは止める
        bool notGreen = isNSDirection ? !manager.IsNS_CarGreen : !manager.IsEW_CarGreen;
        CarIntersectionNode node = Node;

        for (int i = carsInZone.Count - 1; i >= 0; i--)
        {
            CarController car = carsInZone[i];
            if (car == null)
            {
                carsInZone.RemoveAt(i);
                continue;
            }

            // ゾーンから離れたのに退出が検出されなかった車を外す（外さないと、遠くにいる車をこの信号で止めてしまう）
            if (!IsInsideZone(car.transform.position, 4f))
            {
                ReleaseCar(car);
                continue;
            }

            // 既に交差点への進入を開始した車は、ここでは止めない
            if (car.HasEnteredIntersection) continue;

            // 信号が青でも、直交方向の車がまだ交差点を通過中なら、通過し終わるまで待つ
            Vector3 carForward = car.transform.forward;
            bool carIsNS = Mathf.Abs(carForward.z) >= Mathf.Abs(carForward.x);
            bool crossTraffic = node != null && node.HasCrossTraffic(carIsNS);
            bool wantStop = notGreen || crossTraffic;

            // 停止線を越えて走っている車には、新しく停止を指示しない。
            // （車体の後ろがゾーンに残っている間に止めると、横断歩道や交差点にはみ出して止まる）
            // すでに停止を指示している車と、止まっている車には指示してよい。
            bool pastCommitPoint = FrontDistancePastLine(car) > commitDistance;
            bool canOrderStop = car.IsTrafficStopped || !pastCommitPoint || car.CurrentSpeed < 0.5f;

            if (wantStop && canOrderStop)
            {
                car.SetTrafficStop(true);
                if (node != null) node.CancelCommitted(car);
            }
            else
            {
                car.SetTrafficStop(false);

                // 停止線を越えた車は、交差点に入る前から「通過中」として知らせる（直交方向の車が待てるように）
                if (pastCommitPoint && node != null) node.ReportCommitted(car, carIsNS);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        CarController car = other.GetComponentInParent<CarController>();
        if (car == null) return;
        if (carsInZone.Contains(car)) return;

        // 交差点へ向かってくる車だけを対象にする。
        // 交差点から出ていく車がセンターライン寄りを走ると、対向側のこのゾーンに車体の端が触れることがある。
        // それを止めてしまうと、出口の車線で止まって後続が交差点内に詰まる。
        if (!IsApproaching(car)) return;

        carsInZone.Add(car);

        if (yieldManager != null)
        {
            bool isLaneA = ComputeIsLaneA(car.transform.forward);
            yieldManager.ReportStopZoneEnter(isLaneA, car);

            if (!waitingCars.ContainsKey(car))
            {
                Coroutine c = StartCoroutine(WaitUntilCanEnter(car, isLaneA));
                waitingCars[car] = c;
            }
        }
    }

    private bool IsApproaching(CarController car)
    {
        CarIntersectionNode node = Node;
        if (node == null) return true;

        Vector3 toCenter = node.transform.position - car.transform.position;
        toCenter.y = 0f;
        return Vector3.Dot(car.transform.forward, toCenter) > 0f;
    }

    private IEnumerator WaitUntilCanEnter(CarController car, bool isLaneA)
    {
        car.SetYieldStop(true);

        while (car != null && !yieldManager.CanEnter(isLaneA))
        {
            yield return null;
        }

        if (car != null)
        {
            car.SetYieldStop(false);
        }

        waitingCars.Remove(car);
    }

    void OnTriggerExit(Collider other)
    {
        CarController car = other.GetComponentInParent<CarController>();
        if (car == null) return;

        // 対象にしていない車（交差点から出ていく車など）には何もしない
        if (!carsInZone.Contains(car)) return;

        // 車の中心がまだゾーン内にあるうちは退出扱いにしない
        if (IsInsideZone(car.transform.position, 0f)) return;

        ReleaseCar(car);
    }

    private void ReleaseCar(CarController car)
    {
        car.SetTrafficStop(false);
        carsInZone.Remove(car);

        if (waitingCars.TryGetValue(car, out Coroutine c))
        {
            waitingCars.Remove(car);

            if (c != null)
            {
                StopCoroutine(c);
            }

            car.SetYieldStop(false);
        }
    }

    private Bounds ZoneBounds
    {
        get
        {
            if (zoneCollider == null) zoneCollider = GetComponent<Collider>();
            return zoneCollider != null ? zoneCollider.bounds : new Bounds(transform.position, Vector3.zero);
        }
    }

    private bool IsInsideZone(Vector3 position, float margin)
    {
        Bounds b = ZoneBounds;
        b.Expand(new Vector3(margin * 2f, 100f, margin * 2f));
        return b.Contains(position);
    }

    // 車の先端が、ゾーンの出口側の端（停止線）をどれだけ越えているか[m]。手前にあるときはマイナス
    private float FrontDistancePastLine(CarController car)
    {
        Bounds b = ZoneBounds;
        Vector3 f = car.transform.forward;
        Vector3 front = car.transform.position + f * (car.vehicleLength * 0.5f);

        if (Mathf.Abs(f.x) > Mathf.Abs(f.z))
        {
            return f.x > 0f ? front.x - b.max.x : b.min.x - front.x;
        }
        return f.z > 0f ? front.z - b.max.z : b.min.z - front.z;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = isNSDirection ? new Color(0f, 0.5f, 1f, 0.3f)
                                     : new Color(1f, 0.5f, 0f, 0.3f);
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(bc.center, bc.size);
        }
    }
#endif
}
