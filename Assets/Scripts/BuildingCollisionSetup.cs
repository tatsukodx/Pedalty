using System;
using UnityEngine;

/// <summary>
/// シーン内に配置された建物へ、メッシュ外形に合うBox Colliderを自動設定する。
/// 建物プレハブを今後追加した場合も、名前が「Building」で始まれば自動的に対象になる。
/// </summary>
public static class BuildingCollisionSetup
{
    const string BuildingNamePrefix = "Building";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AddMissingBuildingColliders()
    {
        MeshFilter[] meshFilters = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude);
        int addedCount = 0;

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null ||
                !meshFilter.gameObject.name.StartsWith(BuildingNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (HasSolidCollider(meshFilter.gameObject))
            {
                continue;
            }

            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            if (meshBounds.size.sqrMagnitude <= 0.0001f)
            {
                Debug.LogWarning($"[BuildingCollisionSetup] {meshFilter.gameObject.name} のメッシュ外形を取得できませんでした。");
                continue;
            }

            BoxCollider buildingCollider = meshFilter.gameObject.AddComponent<BoxCollider>();
            buildingCollider.center = meshBounds.center;
            buildingCollider.size = meshBounds.size;
            buildingCollider.isTrigger = false;
            addedCount++;
        }

        Debug.Log($"[BuildingCollisionSetup] 建物{addedCount}棟に当たり判定を追加しました。");
    }

    static bool HasSolidCollider(GameObject target)
    {
        Collider[] colliders = target.GetComponents<Collider>();
        foreach (Collider collider in colliders)
        {
            if (collider != null && collider.enabled && !collider.isTrigger)
            {
                return true;
            }
        }

        return false;
    }
}
