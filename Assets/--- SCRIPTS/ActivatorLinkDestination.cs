using UnityEngine;

public static class ActivatorLinkDestination
{
    public const string ObjectName = "Activator Link Destination";
    public const string LegacyObjectName = "Activator Link End";

    public static Transform Find(Transform deviceRoot)
    {
        if (deviceRoot == null)
            return null;

        Transform destination = FindByName(deviceRoot, ObjectName);
        if (destination != null)
            return destination;

        return FindByName(deviceRoot, LegacyObjectName);
    }

    public static bool TryGetWorldPosition(GameObject device, out Vector3 position)
    {
        position = default;
        if (device == null)
            return false;

        Transform destination = Find(device.transform);
        if (destination == null)
            return false;

        position = destination.position;
        return true;
    }

    private static Transform FindByName(Transform root, string objectName)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.Trim() == objectName)
                return child;

            Transform nested = FindByName(child, objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
