using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static IFC.EdgeHelper;
using static IFC.OverlapFixer;

namespace IFC
{
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
        public static bool SegmentIntersect(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, out Vector3 intersectionPoint, bool debug = false)
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

            if (debug)
                InSceneDebugTool.Instance.DrawPoint(intersectionPoint, Color.red, .2f);

            // The segments intersect if t1 and t2 are between 0 and 1.
            bool intersect =
                ((t1 >= 0) && (t1 <= 1) &&
                 (t2 >= 0) && (t2 <= 1));

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
        /// Helper function that checks whether a given layer is included in the given layermask
        /// </summary>
        public static bool IsInLayerMask(int layer, LayerMask layermask)
        {
            return layermask == (layermask | (1 << layer));
        }

        public static Vector3 Vector3FromVector2(Vector2 v)
        {
            return new Vector3(v.x, 0, v.y);
        }

        public static Vector2 Vector2FromVector3(Vector3 v)
        {
            return new Vector2(v.x, v.z);
        }

        public static bool PointInPolygon(Vector3 p, MeshInfo polygon, bool debug = false)
        {
            if (!CheckInBounds(p, polygon.Bounds))
            {
                if (debug)
                    InSceneDebugTool.Instance.DrawPoint(p, Color.blue);
                return false;
            }

            // Create an infinite line on the x-axis
            Vector3 p2 = p;
            p2.x += 1000;

            // If this has an even number of intersections with the polygon the point lies outside, otherwise it lies inside
            int i = LineMeshIntersectionCount(p, p2, polygon);

            if (debug)
            {
                InSceneDebugTool.Instance.DrawLine(p, p2, Color.yellow);
                if (i % 2 == 0)
                {
                    InSceneDebugTool.Instance.DrawPoint(p, Color.red);
                }
                else
                {
                    InSceneDebugTool.Instance.DrawPoint(p, Color.green);
                }
            }

            return i % 2 != 0;
        }

        public static bool CheckInBounds(Vector3 p, Bounds bounds)
        {
            return p.x > bounds.min.x && p.x < bounds.max.x
                && p.z > bounds.min.z && p.z < bounds.max.z;
        }

        public static bool ContainedInBounds(Bounds b1, Bounds b2)
        {
            return b1.min.x > b2.min.x && b1.min.z > b2.min.z
                && b1.max.x > b2.max.x && b1.max.z > b2.max.z;
        }

        public static int LineMeshIntersectionCount(Vector3 p1, Vector3 p2, MeshInfo mesh)
        {
            int count = 0;
            foreach (EdgeInfo e in mesh.OuterEdges)
            {
                Vector3 p3 = mesh.Vertices[e.v1];
                Vector3 p4 = mesh.Vertices[e.v2];
                Vector3 intersectionPoint = new Vector3();
                if (Utils.SegmentIntersect(p1, p2, p3, p4, out intersectionPoint))
                {
                    count++;
                }
            }
            return count;
        }

        public static List<List<EdgeInfo>> GetBoundaryLoops(Mesh m)
        {
            List<List<EdgeInfo>> loops = new List<List<EdgeInfo>>();
            List<EdgeInfo> edges = GetEdges(m.triangles).FindBoundary().SortEdges();

            int i = 0;
            while (i < edges.Count)
            {
                List<EdgeInfo> loop = new List<EdgeInfo>();
                int v1 = edges[i].v1;
                while (edges[i].v2 != v1 && i < edges.Count)
                {
                    loop.Add(edges[i]);
                    i++;
                }

                loop.Add(edges[i]);
                loops.Add(loop);
                i++;
            }


            return loops;
        }

        public static int GetIndexLargestLoop(List<List<EdgeInfo>> loops, Mesh m, bool debug = false)
        {
            int index = -1;
            Bounds largestBounds = new Bounds();
            largestBounds.SetMinMax(Vector3.zero, Vector3.zero);

            for (int i = 0; i < loops.Count; i++)
            {
                Bounds newBounds = new Bounds();
                List<Vector3> vertices = new List<Vector3>();
                foreach (EdgeInfo e in loops[i])
                    vertices.Add(m.vertices[e.v1]);

                newBounds = CalculateBounds(vertices);

                // If this is larger
                if (newBounds.extents.magnitude > largestBounds.extents.magnitude)
                {
                    largestBounds = newBounds;
                    index = i;
                }
            }

            return index;
        }

        public static void ExtractLoopsFromMesh(Mesh m, out List<EdgeInfo> outerEdges, out List<List<EdgeInfo>> holes)
        {
            outerEdges = new List<EdgeInfo>();
            holes = new List<List<EdgeInfo>>();

            List<List<EdgeInfo>> loops = Utils.GetBoundaryLoops(m);
            int idx = Utils.GetIndexLargestLoop(loops, m);

            if (idx == -1)
            {
                return;
            }

            outerEdges = loops[idx];

            for (int l = 0; l < loops.Count; l++)
            {
                if (l == idx)
                    continue;

                holes.Add(loops[l]);
            }
        }

        public static Bounds CalculateBounds(List<Vector3> vertices)
        {
            Bounds b = new Bounds();

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (Vector3 v in vertices)
            {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }

            b.SetMinMax(min, max);
            return b;
        }

        public static float CWAngleBetweenVectors(Vector2 p1, Vector2 p2)
        {
            float dirP1 = Mathf.Atan2(p1.x, p1.y);
            float dirP2 = Mathf.Atan2(p2.x, p2.y);
            float angle = dirP1 - dirP2;

            if (angle < 0)
                angle += 2 * Mathf.PI;
            return angle;
        }
    }
}