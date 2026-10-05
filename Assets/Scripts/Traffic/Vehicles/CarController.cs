using UnityEngine;

public class CarController : MonoBehaviour
{
    public float moveSpeed = 8f;
    public float acceleration = 6f;
    public float turnLerpSpeed = 4f;
    public float laneCorrectionSpeed = 4f;
    public float laneSensorRadius = 1f;
    public float obstacleCheckDistance = 6f;
    public float obstacleCheckRadius = 1.2f;
    public float mass = 1000f;

    [Header("車体・停止余裕の設定")]
    public float vehicleLength = 6f;
    public float brakingSafetyBuffer = 2f;

    [Header("計画的な停止（信号・譲り合い・歩行者待ち）専用の減速度")]
    [Tooltip("あらかじめ止まるとわかっている場合は、障害物回避より強めにブレーキをかけて、手前で止まれるようにする")]
    public float voluntaryStopDeceleration = 14f;

    [Header("左折時に歩行者を待つ場合の減速度")]
    [Tooltip("左折は奥の横断歩道（対向側）を確認するため、通常の歩行者待ちよりさらに強めにブレーキをかけて、より手前で停止させる")]
    public float leftTurnPedestrianStopDeceleration = 22f;

    [Header("曲がる時の速度倍率")]
    [Range(0.1f, 1f)] public float turnSpeedMultiplier = 0.7f;

    [Header("自転車検知（前方の箱判定）")]
    public float bicycleDetectWidth = 2.4f;

    [Header("ウインカー")]
    public float blinkerInterval = 0.4f;
    [Tooltip("オンにすると、車のモデルの大きさから前後左右の角に自動でウインカーを配置する")]
    public bool autoPlaceBlinkers = true;
    [Tooltip("自動配置しない場合の位置（右前。左・後ろは反転して使う）")]
    public Vector3 blinkerLocalOffset = new Vector3(0.9f, 0.8f, 2.2f);
    public float blinkerSize = 0.3f;
    public Color blinkerColor = new Color(1f, 0.5f, 0f);
    [Tooltip("光らせる強さ")]
    public float blinkerEmission = 4f;
    [Tooltip("前のウインカーの位置補正（x=外側へ, y=上へ, z=前へ）[m]。プレイ中に変えるとすぐ反映される")]
    public Vector3 frontBlinkerAdjust = Vector3.zero;
    [Tooltip("後ろのウインカーの位置補正（x=外側へ, y=上へ, z=後ろへ）[m]。プレイ中に変えるとすぐ反映される")]
    public Vector3 rearBlinkerAdjust = Vector3.zero;

    [Header("デバッグ表示")]
    [SerializeField] private string stopReasonDebug = "走行中";

    bool isLightStopped = false;
    bool isYieldStopped = false;
    bool isPedestrianStopped = false;
    bool isLeftTurnPedestrianStop = false;
    bool hasEnteredIntersection = false;

    bool isTurning = false;
    int blinkerChoice = -1;
    GameObject blinkerLeft;
    GameObject blinkerRight;
    // 自動計算した右前の角の位置と後ろのZ（補正前）
    Vector3 blinkerBaseCorner;
    float blinkerBaseRearZ;
    // [0]=右前 [1]=右後 [2]=左前 [3]=左後
    readonly Transform[] blinkerLamps = new Transform[4];
    CarBlinkerOffset modelBlinkerOffset;

    public int PlannedTurnChoice { get; private set; } = -1;
    public bool HasEnteredIntersection => hasEnteredIntersection;

    // 歩行者以外の理由（信号・対向車線）で待機しているか
    public bool IsWaitingForNonPedestrianReason => isLightStopped || isYieldStopped;

    // 信号・対向車・歩行者・自転車のいずれかを待って止まっている（スタック解消の対象判定に使う）
    public bool IsWaiting => isLightStopped || isYieldStopped || isPedestrianStopped || lastObstacleAhead;
    bool lastObstacleAhead = false;

    // 現在の速度から停止するまでに必要な距離（おおよそ）
    public float EstimatedBrakingDistance
    {
        get
        {
            float decel = Mathf.Max(voluntaryStopDeceleration, 0.01f);
            return (currentSpeed * currentSpeed) / (2f * decel);
        }
    }

    Rigidbody rb;
    Vector3 targetDirection;
    float currentSpeed;

    public void SetDirection(Vector3 direction)
    {
        targetDirection = direction.normalized;

        if (targetDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(targetDirection);
        }
    }

    public void SetTrafficStop(bool stop)
    {
        isLightStopped = stop;
    }

    public void SetYieldStop(bool stop)
    {
        isYieldStopped = stop;
    }

    public void SetPedestrianStop(bool stop, bool isLeftTurn = false)
    {
        isPedestrianStopped = stop;
        isLeftTurnPedestrianStop = stop && isLeftTurn;
    }

    public void SetIntersectionEntered(bool entered)
    {
        hasEnteredIntersection = entered;

        if (entered)
        {
            isLightStopped = false;
        }
    }

    public void SetPlannedTurn(int choice)
    {
        PlannedTurnChoice = choice;
    }

    public void ClearPlannedTurn()
    {
        PlannedTurnChoice = -1;
    }

    // 旋回中は速度を落とす
    public void SetTurning(bool turning)
    {
        isTurning = turning;
    }

    // ウインカー: 1=右, 2=左, それ以外=消灯
    public void SetBlinker(int choice)
    {
        blinkerChoice = choice;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = mass;
        targetDirection = transform.forward;
        CreateBlinkers();
    }

    void CreateBlinkers()
    {
        // 右前の角の位置（ローカル座標）。左側・後ろ側は反転して使う
        Vector3 corner = blinkerLocalOffset;
        float rearZ = -blinkerLocalOffset.z;

        if (autoPlaceBlinkers && TryGetModelLocalBounds(out Bounds b))
        {
            // 車体の角から少し外側に出して、車体に埋もれないようにする
            float half = blinkerSize * 0.5f;
            corner = new Vector3(b.max.x - half * 0.5f, b.center.y + b.extents.y * 0.1f, b.max.z + half * 0.3f);
            rearZ = b.min.z - half * 0.3f;
        }

        blinkerBaseCorner = corner;
        blinkerBaseRearZ = rearZ;
        modelBlinkerOffset = GetComponentInChildren<CarBlinkerOffset>();

        blinkerRight = CreateBlinkerSide("Blinker_R", 0);
        blinkerLeft = CreateBlinkerSide("Blinker_L", 2);
        ApplyBlinkerPositions();
    }

    // 自動計算した位置に、全車共通の補正と車種ごとの補正（CarBlinkerOffset）を足して配置する
    void ApplyBlinkerPositions()
    {
        Vector3 front = frontBlinkerAdjust;
        Vector3 rear = rearBlinkerAdjust;
        if (modelBlinkerOffset != null)
        {
            front += modelBlinkerOffset.frontAdjust;
            rear += modelBlinkerOffset.rearAdjust;
        }

        float frontX = blinkerBaseCorner.x + front.x;
        float rearX = blinkerBaseCorner.x + rear.x;
        Vector3 rightFront = new Vector3(frontX, blinkerBaseCorner.y + front.y, blinkerBaseCorner.z + front.z);
        Vector3 rightRear = new Vector3(rearX, blinkerBaseCorner.y + rear.y, blinkerBaseRearZ - rear.z);

        SetLamp(0, rightFront);
        SetLamp(1, rightRear);
        SetLamp(2, new Vector3(-rightFront.x, rightFront.y, rightFront.z));
        SetLamp(3, new Vector3(-rightRear.x, rightRear.y, rightRear.z));

        float size = blinkerSize;
        foreach (Transform lamp in blinkerLamps) if (lamp != null) lamp.localScale = Vector3.one * size;
    }

    void SetLamp(int index, Vector3 localPos)
    {
        // ランプは片側ごとの親の下にあるが、親は車の原点にあるので車のローカル座標をそのまま使える
        if (blinkerLamps[index] != null) blinkerLamps[index].localPosition = localPos;
    }

    // 車のモデル（子のRenderer）全体を、この車のローカル座標での範囲として求める
    bool TryGetModelLocalBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            Bounds wb = r.bounds;
            Vector3 c = wb.center, e = wb.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 world = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 local = transform.InverseTransformPoint(world);
                if (!found) { bounds = new Bounds(local, Vector3.zero); found = true; }
                else bounds.Encapsulate(local);
            }
        }
        return found;
    }

    // 片側（前・後ろ）のウインカーをまとめた親を作る。親を表示/非表示にして点滅させる
    GameObject CreateBlinkerSide(string name, int firstLampIndex)
    {
        GameObject side = new GameObject(name);
        side.transform.SetParent(transform, false);
        blinkerLamps[firstLampIndex] = CreateBlinkerLamp(side.transform, Vector3.zero);
        blinkerLamps[firstLampIndex + 1] = CreateBlinkerLamp(side.transform, Vector3.zero);
        side.SetActive(false);
        return side;
    }

    Transform CreateBlinkerLamp(Transform parent, Vector3 localPos)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.name = "Lamp";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * blinkerSize;

        Renderer r = go.GetComponent<Renderer>();
        Material m = r.material;
        m.color = blinkerColor;
        // 昼間でも目立つように発光させる
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", blinkerColor * blinkerEmission);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    void UpdateBlinkers()
    {
        if (blinkerLeft == null || blinkerRight == null) return;

        int choice = blinkerChoice != -1 ? blinkerChoice : PlannedTurnChoice;
        bool on = Mathf.Repeat(Time.time, blinkerInterval * 2f) < blinkerInterval;

#if UNITY_EDITOR
        // プレイ中にInspectorで補正値を変えた時にすぐ反映させる（調整用）
        ApplyBlinkerPositions();
#endif
        blinkerRight.SetActive(on && choice == 1);
        blinkerLeft.SetActive(on && choice == 2);
    }

    void FixedUpdate()
    {

        Quaternion lookRot = Quaternion.LookRotation(targetDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.fixedDeltaTime * turnLerpSpeed);

        bool obstacleAhead = HasObstacleAhead();
        lastObstacleAhead = obstacleAhead;
        bool voluntaryStop = isLightStopped || isYieldStopped || isPedestrianStopped;

        float cruiseSpeed = isTurning ? moveSpeed * turnSpeedMultiplier : moveSpeed;
        float target = (obstacleAhead || voluntaryStop) ? 0f : cruiseSpeed;
        float voluntaryDecel = isLeftTurnPedestrianStop ? leftTurnPedestrianStopDeceleration : voluntaryStopDeceleration;
        float decelRate = (!obstacleAhead && voluntaryStop) ? voluntaryDecel : acceleration;

        currentSpeed = Mathf.MoveTowards(currentSpeed, target, decelRate * Time.fixedDeltaTime);

        Vector3 forwardVel = transform.forward * currentSpeed;

        // 車線補正は走行中のみ効かせる。
        // 停止中も横に押し続けると、信号待ちの間に少しずつ横滑りして
        // 縁石やポールに噛み込み、二度と動けなくなることがある。
        float lateralScale = Mathf.Clamp01(currentSpeed / Mathf.Max(moveSpeed, 0.01f));
        Vector3 lateralVel = ComputeLaneCorrection() * lateralScale;

        Vector3 totalVel = forwardVel + lateralVel;

        rb.linearVelocity = new Vector3(totalVel.x, rb.linearVelocity.y, totalVel.z);
        rb.angularVelocity = Vector3.zero;

        UpdateStopReasonDebug(obstacleAhead);
    }

    void UpdateStopReasonDebug(bool obstacleAhead)
    {
        string reason = "";

        if (isLightStopped) reason += "信号待ち / ";
        if (isYieldStopped) reason += "対向車線を待機 / ";
        if (isPedestrianStopped) reason += "歩行者の横断を待機 / ";
        if (obstacleAhead) reason += "障害物 / ";

        stopReasonDebug = reason == "" ? "走行中" : reason.Substring(0, reason.Length - 3);
    }

    Vector3 ComputeLaneCorrection()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, laneSensorRadius, ~0, QueryTriggerInteraction.Collide);
        Vector3 correction = Vector3.zero;
        bool onCorrectSideRoad = false;
        bool onWrongSideRoad = false;

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("BIkeLane_L") || hit.CompareTag("Sidewalk_L"))
            {
                correction += transform.right;
            }
            else if (hit.CompareTag("BikeLane_R") || hit.CompareTag("Sidewalk_R"))
            {
                correction -= transform.right;
            }
            else if (hit.CompareTag("Road_L") || hit.CompareTag("Road_R"))
            {
                bool travelingWithRoadForward = Vector3.Dot(transform.forward, hit.transform.forward) >= 0f;
                bool isLeftTag = hit.CompareTag("Road_L");
                bool correctLane = travelingWithRoadForward ? isLeftTag : !isLeftTag;

                if (correctLane)
                {
                    onCorrectSideRoad = true;
                }
                else
                {
                    onWrongSideRoad = true;
                }
            }
        }

        if (onWrongSideRoad && !onCorrectSideRoad)
        {
            correction -= transform.right;
        }

        if (correction == Vector3.zero) return Vector3.zero;
        return correction.normalized * laneCorrectionSpeed;
    }


    bool HasObstacleAhead()
    {
        float frontOffset = vehicleLength * 0.5f;
        Vector3 origin = transform.position + Vector3.up * 0.5f + transform.forward * frontOffset;

        float brakingDistance = (currentSpeed * currentSpeed) / (2f * Mathf.Max(acceleration, 0.01f));
        float checkDistance = Mathf.Max(obstacleCheckDistance, brakingDistance + brakingSafetyBuffer);

        RaycastHit[] hits = Physics.SphereCastAll(origin, obstacleCheckRadius, transform.forward, checkDistance, ~0, QueryTriggerInteraction.Ignore);

        if (hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<CarController>() != null) return true;
                if (hit.collider.GetComponentInParent<BicycleController>() != null) return true;

                NPCWalker walker = hit.collider.GetComponentInParent<NPCWalker>();
                if (walker != null && walker.IsCrossing) return true;
            }
        }

        // SphereCastは開始位置で重なっている相手や、旋回中に斜め前にいる相手を取りこぼすことがあるため、
        // 自転車だけは進行方向（現在の向きと目標方向の中間）の箱で追加判定する
        Vector3 lookDir = (transform.forward + targetDirection).normalized;
        if (lookDir.sqrMagnitude < 0.001f) lookDir = transform.forward;
        Vector3 boxCenter = transform.position + Vector3.up * 1f + transform.forward * frontOffset + lookDir * (checkDistance * 0.5f);
        Vector3 halfExtents = new Vector3(bicycleDetectWidth * 0.5f, 1.5f, checkDistance * 0.5f + frontOffset * 0.5f);
        Collider[] boxHits = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.LookRotation(lookDir), ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider c in boxHits)
        {
            if (c.GetComponentInParent<BicycleController>() != null) return true;
        }

        return false;
    }

    void Update()
    {
        UpdateBlinkers();

        if (transform.position.y < -10f)
        {
            Destroy(gameObject);
        }
    }
}