using UnityEngine;

// 曲がる途中の車が止まったまま道をふさいだ時の救済。
// 暗転して、自転車に一番近い交差点の周りで待機中（信号・対向車・歩行者・自転車待ち）の車を消す
public static class StuckCarClearer
{
    // 交差点の中心からこの距離以内の車を対象にする
    const float ClearRadius = 30f;

    public static void ClearNearestIntersection(BicycleController bicycle)
    {
        if (bicycle == null) return;

        BicycleRecovery recovery = bicycle.GetComponent<BicycleRecovery>();
        if (recovery == null)
        {
            Clear(bicycle.transform.position);
            return;
        }

        Vector3 position = bicycle.transform.position;
        recovery.FadeAndRun(() => Clear(position));
    }

    static void Clear(Vector3 bicyclePosition)
    {
        CarIntersectionNode nearest = null;
        float best = float.MaxValue;
        foreach (CarIntersectionNode node in Object.FindObjectsByType<CarIntersectionNode>(FindObjectsSortMode.None))
        {
            float d = FlatDistance(node.transform.position, bicyclePosition);
            if (d < best)
            {
                best = d;
                nearest = node;
            }
        }
        if (nearest == null) return;

        int removed = 0;
        foreach (CarController car in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
        {
            if (!car.IsWaiting) continue;
            if (FlatDistance(car.transform.position, nearest.transform.position) > ClearRadius) continue;

            Object.Destroy(car.gameObject);
            removed++;
        }

        // 消した車は交差点から「出た」ことにならないため、対向車待ちの台数を数え直させる
        // （そのままだと、残った車が居ない車をいつまでも待ってしまう）
        if (nearest.nsYieldManager != null) nearest.nsYieldManager.ResetCounts();
        if (nearest.ewYieldManager != null) nearest.ewYieldManager.ResetCounts();

        Debug.Log($"[StuckCarClearer] {nearest.name} の周りで待機中の車を {removed} 台削除しました");
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
