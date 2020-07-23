using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utils
{
    /// <summary>
    /// Checks whether two segments defined as [p1, p2] and [p3, p4] intersect
    /// </summary>
    /// <param name="p1"></param>
    /// <param name="p2"></param>
    /// <param name="p3"></param>
    /// <param name="p4"></param>
    /// <returns></returns>
    public static bool SegmentIntersect(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, out Vector3 intersectionPoint)
    {
        intersectionPoint = Vector3.zero;

        // Get the segments' parameters.
        float dx12 = p2.x - p1.x;
        float dy12 = p2.z - p1.z;
        float dx34 = p4.x - p3.x;
        float dy34 = p4.z - p3.z;

        // Solve for t1 and t2
        float denominator = (dy12 * dx34 - dx12 * dy34);
        float t1 =
            ((p1.x - p3.x) * dy34 + (p3.z - p1.z) * dx34)
                / denominator;

        if (float.IsInfinity(t1))
        {
            return false;
        }

        float t2 =
            ((p3.x - p1.x) * dy12 + (p1.z - p3.z) * dx12)
                / -denominator;

        // Find the point of intersection.
        intersectionPoint = new Vector3(p1.x + dx12 * t1, 0, p1.z + dy12 * t1);

        // The segments intersect if t1 and t2 are between 0 and 1.
        bool intersect =
            ((t1 >= 0) && (t1 <= 1) &&
             (t2 >= 0) && (t2 <= 1));

        // Exclude point intersections
        if (intersect)
            if (intersectionPoint == p1 || intersectionPoint == p2 || intersectionPoint == p3 || intersectionPoint == p4)
                intersect = false;

        return intersect;
    }

    /// <summary>
    /// Returns a random point in the given bounds
    /// </summary>
    public static Vector3 RandomPointInBounds(Bounds bounds)
    {
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }

    public static Vector3 RandomPointInBoundsOnZeroPlane(Bounds bounds)
    {
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            0,
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }

    /// <summary>
    /// Helper function that returns the worldposition on an y-plane as "seen" from an input position with the main camera
    /// </summary>
    /// <param name="touchPos">The input position</param>
    /// <param name="yPos">The y-plane to project the touch on</param>
    /// <returns>The 3D world coordinates</returns>
    public static Vector3 GetPlaneIntersection(Vector2 mousePos, float yPos = 0)
    {
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        float delta = ray.origin.y - yPos;
        Vector3 dirNorm = ray.direction / ray.direction.y;
        return ray.origin - dirNorm * delta;
    }

    /// <summary>
    /// Helper function that returns the in world click position, or if there is no intersection the point clicked on the plane at the given ypos
    /// </summary>
    /// <param name="ypos">What plane the function should default to if nu intersection is found</param>
    /// <returns></returns>
    // public static Vector3 GetInWorldClickPosition(LayerMask mask, float ypos = 0)
    // {
    //     Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
    //     RaycastHit hit;

    //     if (Physics.Raycast(ray, out hit, 1000000, mask))
    //     {
    //         return hit.point;
    //     }
    //     else
    //     {
    //         return (GetPlaneIntersection(0));
    //     }
    // }

    /// <summary>
    /// Helper function that checks whether a given layer is included in the given layermask
    /// </summary>
    public static bool IsInLayerMask(int layer, LayerMask layermask)
    {
        return layermask == (layermask | (1 << layer));
    }
}
