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

    private readonly List<CarController> carsInZone = new List<CarController>();

    private readonly Dictionary<CarController, Coroutine> waitingCars = new Dictionary<CarController, Coroutine>();

    // ゾーンに入った時点で既に青だった車。信号が黄/赤に変わってもそのまま通す。
    private readonly HashSet<CarController> carsEnteredOnGreen = new HashSet<CarController>();

    [Header("プレイヤーの信号無視判定")]
    [Tooltip("停止ゾーンは車線しか覆っていないため、自転車レーンや歩道を走るプレイヤーも判定できるよう道路を横切る向きに広げる量")]
    public float playerLateralMargin = 5f;

    private BoxCollider boxCollider;
    private BicycleController player;
    private bool playerWasInZone;

    private bool ComputeIsLaneA(Vector3 dir)
    {
        bool isNSAxis = Mathf.Abs(dir.x) < Mathf.Abs(dir.z);
        return isNSAxis ? dir.z >= 0f : dir.x >= 0f;
    }

    void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    void Update()
    {
        if (manager == null) return;

        UpdatePlayerSignalCheck();

        bool shouldStop = isNSDirection ? !manager.IsNS_CarGreen : !manager.IsEW_CarGreen;

        for (int i = carsInZone.Count - 1; i >= 0; i--)
        {
            if (carsInZone[i] == null)
            {
                carsInZone.RemoveAt(i);
                carsEnteredOnGreen.RemoveWhere(c => c == null);
                continue;
            }

            // 既に交差点への進入を開始した車は、信号が変わってもここで止めない
            if (carsInZone[i].HasEnteredIntersection) continue;

            // 青のうちにゾーンへ入った車は、その後黄/赤に変わってもそのまま通す
            if (carsEnteredOnGreen.Contains(carsInZone[i])) continue;

            carsInZone[i].SetTrafficStop(shouldStop);
        }
    }

    private void UpdatePlayerSignalCheck()
    {
        if (boxCollider == null) return;

        if (player == null)
        {
            player = FindAnyObjectByType<BicycleController>();
            if (player == null) return;
        }

        bool inZone = IsPlayerInZone();

        // ゾーンを抜ける＝停止線を越えて交差点に進入した瞬間。その時点の信号で判定する
        if (!inZone && playerWasInZone && IsRedForThisZone())
        {
            TrafficViolationDetector.Instance?.ReportViolationById("traffic_light");
        }

        playerWasInZone = inZone;
    }

    private bool IsPlayerInZone()
    {
        Bounds bounds = boxCollider.bounds;
        bounds.Expand(isNSDirection
            ? new Vector3(playerLateralMargin * 2f, 0f, 0f)
            : new Vector3(0f, 0f, playerLateralMargin * 2f));

        Vector3 p = player.transform.position;
        return p.x >= bounds.min.x && p.x <= bounds.max.x
            && p.z >= bounds.min.z && p.z <= bounds.max.z;
    }

    void OnTriggerEnter(Collider other)
    {
        CarController car = other.GetComponentInParent<CarController>();
        if (car == null) return;

        if (!carsInZone.Contains(car))
        {
            carsInZone.Add(car);

            if (manager != null)
            {
                bool greenNow = isNSDirection ? manager.IsNS_CarGreen : manager.IsEW_CarGreen;
                if (greenNow)
                {
                    carsEnteredOnGreen.Add(car);
                }
            }
        }

        if (yieldManager != null)
        {
            bool isLaneA = ComputeIsLaneA(car.transform.forward);
            yieldManager.ReportStopZoneEnter(isLaneA);

            if (!waitingCars.ContainsKey(car))
            {
                Coroutine c = StartCoroutine(WaitUntilCanEnter(car, isLaneA));
                waitingCars[car] = c;
            }
        }
    }

    private bool IsRedForThisZone()
    {
        if (manager == null) return false;

        return isNSDirection ? manager.IsNS_CarRed : manager.IsEW_CarRed;
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
        if (car != null)
        {
            car.SetTrafficStop(false);
            carsInZone.Remove(car);
            carsEnteredOnGreen.Remove(car);

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