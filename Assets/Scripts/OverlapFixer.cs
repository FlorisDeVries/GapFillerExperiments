using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

using static IFC.EdgeHelper;
using System.Linq;

namespace IFC
{
    public class OverlapFixer : UnitySingleton<OverlapFixer>
    {
        private enum OverlapType
        {
            NotOverlapping,
            FullyContainedIn1,
            FullyContainedIn2,
            Overlapping
        }

        public Button FixButton = default;
        public Button DrawDebugButton = default;
        public Button ClearButton = default;
        public Button SetButton = default;
        List<MeshInfo> AllMeshes = new List<MeshInfo>();

        // Start is called before the first frame update
        void Start()
        {
            FixButton.onClick.AddListener(() =>
            {
                FixOverlaps();
            });

            DrawDebugButton.onClick.AddListener(() =>
            {
                InSceneDebugTool.Instance.ClearDebug();
                SetMeshes();
                DrawDebug();
            });

            ClearButton.onClick.AddListener(InSceneDebugTool.Instance.ClearDebug);
        }

        private void Update()
        {
            if (MeshesParent.Instance.GetChildCount() > 1 || AllMeshes.Count > 1)
            {
                FixButton.interactable = true;
            }
            else
            {
                FixButton.interactable = false;
            }
        }

        public void SetMeshes()
        {
            InSceneDebugTool.Instance.ClearDebug();
            List<MeshFilter> filters = MeshesParent.Instance.GetComponentsInChildren<MeshFilter>().ToList();
            List<Mesh> meshes = new List<Mesh>();
            foreach (MeshFilter f in filters)
            {
                meshes.Add(f.sharedMesh);
            }

            SetMeshes(meshes);
        }

        public void FixOverlaps()
        {
            SetMeshes();

            List<MeshFilter> filters = MeshesParent.Instance.GetComponentsInChildren<MeshFilter>().ToList();
            foreach (MeshFilter f in filters)
            {
                f.gameObject.transform.SetParent(this.transform);
                Destroy(f.gameObject);
            }

            PreProcessMeshes();
            MeshCreator.Instance.CreateMeshes(AllMeshes);
        }

        private void SetMeshes(List<Mesh> meshes)
        {
            List<MeshInfo> meshInfo = new List<MeshInfo>();

            foreach (Mesh m in meshes)
                meshInfo.Add(new MeshInfo(m));

            AllMeshes = meshInfo;
        }

        public List<MeshInfo> FixOverlapsFor(List<MeshInfo> meshes)
        {
            AllMeshes = meshes;
            PreProcessMeshes();
            return AllMeshes;
        }

        public void PreProcessMeshes()
        {
            Debug.Log($"Preprocessing... {AllMeshes.Count}");
            AllMeshes.Sort((x1, x2) => x2.Bounds.extents.magnitude.CompareTo(x1.Bounds.extents.magnitude));
            for (int i = 0; i < AllMeshes.Count; i++)
            {
                MeshInfo m = AllMeshes[i];
                if (CombineFor(m, AllMeshes, i))
                {
                    PreProcessMeshes();
                    break;
                }
            }
        }

        private bool CombineFor(MeshInfo meshInfo, List<MeshInfo> meshes, int i)
        {
            MeshInfo mesh1 = meshInfo;
            Vector3 randomPoint = mesh1.Vertices[Random.Range(0, mesh1.Vertices.Count - 1)];
            for (int j = i + 1; j < meshes.Count; j++)
            {
                MeshInfo mesh2 = meshes[j];

                OverlapType type = DetectOverlap(mesh1, mesh2);

                if (type == OverlapType.NotOverlapping)
                    continue;

                if (type == OverlapType.FullyContainedIn1)
                {
                    AllMeshes.Remove(meshes[j]);
                    return true;
                }

                if (type == OverlapType.FullyContainedIn2)
                {
                    AllMeshes.Remove(meshes[i]);
                    return true;
                }

                if (type == OverlapType.Overlapping)
                {
                    if (mesh1.Bounds.extents.magnitude > mesh2.Bounds.extents.magnitude)
                        FixOverlap(mesh1, mesh2);
                    else
                        FixOverlap(mesh2, mesh1);

                    return true;
                }
            }
            return false;
        }

        private void FixOverlap(MeshInfo m1, MeshInfo m2)
        {
            // Get all intersections
            List<EdgeIntersect> intersections = new List<EdgeIntersect>();
            Dictionary<MeshInfo, List<int>> sharedPointsDict = new Dictionary<MeshInfo, List<int>>();
            List<int> m1Shared = new List<int>();
            List<int> m2Shared = new List<int>();
            (intersections, m1Shared, m2Shared) = GetAllEdgeIntersections(m1.Vertices, m2.Vertices, ref m1.OuterEdges, ref m2.OuterEdges, false);
            sharedPointsDict.Add(m1, m1Shared);
            sharedPointsDict.Add(m2, m2Shared);

            List<Vector3> vertices = GetOuterVertices(m1, m2, ref intersections, ref sharedPointsDict);

            // Get existing hole gaps        
            List<List<Vector3>> holeVertices = GetExistingHoleLoopVertices(m1, m2);

            // Solving the remaining intersection gives the gaps in between meshes as hole loops
            int depth = 2;
            List<Vector3> usedVertices = new List<Vector3>();
            while (intersections.Count > 0)
            {
                depth--;
                if (depth < 0)
                    break;

                List<Vector3> holeLoop = GetHoleLoop(m1, m2, vertices, ref usedVertices, ref intersections);
                if (holeLoop.Count < 3)
                    break;
                holeVertices.Add(holeLoop);
            }

            AllMeshes.Remove(m1);
            AllMeshes.Remove(m2);

            MeshInfo newMesh = new MeshInfo(vertices, holeVertices);
            AllMeshes.Add(newMesh);
        }

        #region HelperFunctions
        private List<Vector3> GetOuterVertices(MeshInfo m1, MeshInfo m2, ref List<EdgeIntersect> intersections, ref Dictionary<MeshInfo, List<int>> sharedPointsDict)
        {
            // Find a starting point, only requirement is that it lies outside of m2
            int startIndex = 0;
            while (Utils.PointInPolygon(m1.Vertices[startIndex], m2))
            {
                startIndex++;
                if (startIndex >= m1.Vertices.Count)
                {
                    AllMeshes.Remove(m1);
                    return new List<Vector3>();
                }
            }

            // Initialiazing variable
            bool onFirstMesh = true;
            EdgeInfo edge = m1.OuterEdges.Find(x => x.v1 == startIndex);
            int edgeIdx = m1.OuterEdges.FindIndex(x => x == edge);
            List<Vector3> vertices = new List<Vector3>();

            // Add first point
            vertices.Add(m1.Vertices[startIndex]);

            EdgeInfo lastEdge = m1.OuterEdges.Find(x => x.v2 == startIndex);
            int lastEdgeIdx = m1.OuterEdges.FindIndex(x => x == lastEdge);
            MeshInfo m = onFirstMesh ? m1 : m2;

            // If the first point is a shared point, we need to select the correct edge before entering the while loop
            if (sharedPointsDict[m1].Contains(startIndex))
            {
                (onFirstMesh, edgeIdx) = ClosestEdgeFromSharedPoint(m1.OuterEdges, m2.OuterEdges, m1.OuterEdges.Find(x => x.v2 == startIndex), m1, m2, onFirstMesh);
                m = onFirstMesh ? m1 : m2;
                edge = m.OuterEdges[edgeIdx];
            }

            // Walk over the edges untill an intersection is found, in which case we swap mesh. Keep going till we are back
            bool done = false;
            Vector2 prevDir = Vector2.zero;
            while (!done)
            {
                Vector3 newV = Vector3.zero;
                EdgeIntersect intersect = new EdgeIntersect();


                if (sharedPointsDict[m].Contains(edge.v2))
                {
                    newV = m.Vertices[edge.v2];

                    (onFirstMesh, edgeIdx) = ClosestEdgeFromSharedPoint(m1.OuterEdges, m2.OuterEdges, edge, m1, m2, onFirstMesh);

                    m = onFirstMesh ? m1 : m2;
                    edge = m.OuterEdges[edgeIdx];
                }
                else
                {
                    if (GetClosestIntersect(vertices[vertices.Count - 1], intersections, edge, m1, m2, onFirstMesh, out intersect))
                    {
                        newV = intersect.intersectionPoint;
                        intersections.Remove(intersect);

                        // Flip
                        onFirstMesh = !onFirstMesh;
                        edgeIdx = onFirstMesh ? intersect.edge1 : intersect.edge2;
                        m = onFirstMesh ? m1 : m2;
                        edge = m.OuterEdges[edgeIdx];
                    }
                    else
                    {
                        if (m.Vertices[edge.v2] != vertices[0])
                            newV = m.Vertices[edge.v2];

                        edgeIdx++;
                        if (edgeIdx >= m.OuterEdges.Count)
                            edgeIdx = 0;

                        edge = m.OuterEdges[edgeIdx];
                    }
                }

                if (newV != Vector3.zero)
                {
                    Vector2 sndPoint = Utils.Vector2FromVector3(m.Vertices[edge.v2]);
                    Vector2 newDir = (Utils.Vector2FromVector3(newV) - sndPoint).normalized;

                    if (prevDir == Vector2.zero)
                        prevDir = (Utils.Vector2FromVector3(vertices.Last()) - Utils.Vector2FromVector3(newV)).normalized;

                    if (prevDir != newDir)
                        if (newV != m1.Vertices[startIndex])
                            vertices.Add(newV);
                        else
                            done = true;
                    prevDir = newDir;
                }

                if (!done)
                    done = (edge.v1 == startIndex && onFirstMesh && !intersections.Exists(x => x.edge1 == edgeIdx || x.edge1 == lastEdgeIdx)) || (vertices.Last() == m1.Vertices[startIndex] && vertices.Count != 1);
            }
            return vertices;
        }

        private List<Vector3> GetHoleLoop(MeshInfo m1, MeshInfo m2, List<Vector3> vertices, ref List<Vector3> usedVertices, ref List<EdgeIntersect> intersections)
        {
            List<Vector3> holeLoop = new List<Vector3>();

            EdgeInfo e = m1.OuterEdges[intersections[0].edge1];
            Vector3 firstV = m1.Vertices[e.v1];
            int startIdx = e.v1;
            int edgeIdx = intersections[0].edge1;
            if (vertices.Contains(firstV) || usedVertices.Contains(firstV))
            {
                firstV = m1.Vertices[e.v2];
                startIdx = e.v2;
            }

            holeLoop.Add(firstV);
            usedVertices.Add(firstV);

            bool done = false;
            bool onFirstMesh = true;
            MeshInfo m = onFirstMesh ? m1 : m2;

            EdgeInfo lastEdge = m1.OuterEdges.Find(x => x.v1 == startIdx);
            int lastEdgeIdx = m1.OuterEdges.FindIndex(x => x == lastEdge);


            while (!done)
            {
                Vector3 newV = Vector3.zero;
                EdgeIntersect intersect = new EdgeIntersect();

                if (GetClosestIntersect(holeLoop[holeLoop.Count - 1], intersections, e, m1, m2, onFirstMesh, out intersect, false, true))
                {
                    newV = intersect.intersectionPoint;
                    intersections.Remove(intersect);

                    // Flip
                    onFirstMesh = !onFirstMesh;
                    edgeIdx = onFirstMesh ? intersect.edge1 : intersect.edge2;
                    m = onFirstMesh ? m1 : m2;
                    e = m.OuterEdges[edgeIdx];
                }
                else
                {
                    if (m.Vertices[e.v1] != holeLoop[0])
                        newV = m.Vertices[e.v1];

                    edgeIdx--;
                    if (edgeIdx < 0)
                        edgeIdx = m.OuterEdges.Count - 1;

                    e = m.OuterEdges[edgeIdx];
                }

                holeLoop.Add(newV);
                usedVertices.Add(newV);

                if (!done)
                    done = (e.v2 == startIdx && onFirstMesh && !intersections.Exists(x => x.edge1 == edgeIdx || x.edge1 == lastEdgeIdx)) || (holeLoop.Last() == m1.Vertices[startIdx] && holeLoop.Count != 1);
            }
            return holeLoop;
        }

        private List<List<Vector3>> GetExistingHoleLoopVertices(MeshInfo m1, MeshInfo m2)
        {
            List<List<Vector3>> holeVertices = new List<List<Vector3>>();
            List<List<Vector2>> list = m1.GetHoles();
            for (int i = 0; i < list.Count; i++)
            {
                List<Vector3> converted = new List<Vector3>();
                for (int v = 0; v < list[i].Count; v++)
                {
                    converted.Add(Utils.Vector3FromVector2(list[i][v]));
                }
                holeVertices.Add(converted);
            }
            list = m2.GetHoles();
            for (int i = 0; i < list.Count; i++)
            {
                List<Vector3> converted = new List<Vector3>();
                for (int v = 0; v < list[i].Count; v++)
                {
                    converted.Add(Utils.Vector3FromVector2(list[i][v]));
                }
                holeVertices.Add(converted);
            }
            return holeVertices;
        }

        private OverlapType DetectOverlap(MeshInfo m1, MeshInfo m2)
        {
            // If the bounds are not overlapping the polygons won't overlap
            // Doing this check first speeds the algorithm up
            if (!BoundsOverlap(m1.Bounds, m2.Bounds))
                return OverlapType.NotOverlapping;

            // If any edge is overlapping it does overlap
            if (AnyEdgeOverlap(m1, m2))
            {
                return OverlapType.Overlapping;
            }
            else
            {
                // No edges overlap, so now we need to know if a random point is inside the other polygon
                Vector3 randomPoint1 = m1.Vertices[Random.Range(0, m1.Vertices.Count - 1)];
                Vector3 randomPoint2 = m2.Vertices[Random.Range(0, m2.Vertices.Count - 1)];

                // Checking tuple so the second loop can become shorter every loop
                if (Utils.PointInPolygon(randomPoint2, m1))
                    return OverlapType.FullyContainedIn1;

                if (Utils.PointInPolygon(randomPoint1, m2))
                    return OverlapType.FullyContainedIn2;
            }

            return OverlapType.NotOverlapping;
        }

        private (bool, int) ClosestEdgeFromSharedPoint(List<EdgeInfo> edges1, List<EdgeInfo> edges2, EdgeInfo e, MeshInfo m1, MeshInfo m2, bool onFirstMesh)
        {

            MeshInfo m = m1;
            if (onFirstMesh)
            {
                m = m1;
            }
            else
            {
                m = m2;
            }
            Vector3 sharedPoint = m.Vertices[e.v2];

            int idx = -1;
            float angle = float.MinValue;
            Vector2 dirP1 = new Vector2(m.Vertices[e.v1].x - sharedPoint.x, m.Vertices[e.v1].z - sharedPoint.z);

            // Check for m1
            List<int> otherEdges = GetEdgesConnectedToV(m1, sharedPoint);
            foreach (int i in otherEdges)
            {
                Vector3 v = m1.Vertices[m1.OuterEdges[i].v2];
                float a = Utils.CWAngleBetweenVectors(dirP1, new Vector2(v.x - sharedPoint.x, v.z - sharedPoint.z));
                if (a > angle)
                {
                    angle = a;
                    idx = i;
                    onFirstMesh = true;
                }
            }

            // Check for m2
            otherEdges = GetEdgesConnectedToV(m2, sharedPoint);
            foreach (int i in otherEdges)
            {
                Vector3 v = m2.Vertices[m2.OuterEdges[i].v2];
                float a = Utils.CWAngleBetweenVectors(dirP1, new Vector2(v.x - sharedPoint.x, v.z - sharedPoint.z));
                if (a > angle)
                {
                    angle = a;
                    idx = i;
                    onFirstMesh = false;
                }
            }
            return (onFirstMesh, idx);
        }

        private List<int> GetEdgesConnectedToV(MeshInfo m, Vector3 v)
        {
            List<int> indices = new List<int>();
            for (int i = 0; i < m.OuterEdges.Count; i++)
            {
                EdgeInfo eI = m.OuterEdges[i];
                // Only want the second vertices, guaranteeing order of execution
                if (m.Vertices[eI.v1] == v)
                {
                    indices.Add(i);
                }
            }
            return indices;
        }

        private bool GetClosestIntersect(Vector3 lastPoint, List<EdgeIntersect> intersections, EdgeInfo e, MeshInfo m1, MeshInfo m2, bool onFirstMesh, out EdgeIntersect intersect, bool debug = false, bool reverse = false)
        {
            intersect = new EdgeIntersect();
            List<EdgeIntersect> intersects = new List<EdgeIntersect>();
            MeshInfo m = m1;
            if (onFirstMesh)
            {
                m = m1;
                intersects = intersections.FindAll(x => m1.OuterEdges[x.edge1] == e);
            }
            else
            {
                m = m2;
                intersects = intersections.FindAll(x => m2.OuterEdges[x.edge2] == e);
            }

            if (intersects.Count == 0)
            {
                return false;
            }

            // Get direction of the edge
            Vector3 edge_p1 = m.Vertices[e.v1];
            Vector3 edge_p2 = m.Vertices[e.v2];
            Vector3 edgeDirection = Vector3.zero;
            if (reverse)
                edgeDirection = (edge_p1 - edge_p2).normalized;
            else
                edgeDirection = (edge_p2 - edge_p1).normalized;

            if (debug)
            {
                InSceneDebugTool.Instance.DrawPoint(edge_p1, Color.red, .3f);
                InSceneDebugTool.Instance.DrawPoint(edge_p2, Color.blue, .4f);
            }

            float minDist = float.MaxValue;
            if (intersects.Count == 1)
            {
                intersect = intersects[0];

                Vector3 newPointDirection = (intersect.intersectionPoint - lastPoint).normalized;
                // Same direction/not opposite direction
                if (Vector3.Dot(newPointDirection, edgeDirection) > 0)
                    return true;
                else
                    return false;
            }
            else
            {
                foreach (EdgeIntersect eI in intersects)
                {
                    Vector3 intersectionPoint = new Vector3();
                    Vector3 p1 = m1.Vertices[m1.OuterEdges[eI.edge1].v1];
                    Vector3 p2 = m1.Vertices[m1.OuterEdges[eI.edge1].v2];
                    Vector3 p3 = m2.Vertices[m2.OuterEdges[eI.edge2].v1];
                    Vector3 p4 = m2.Vertices[m2.OuterEdges[eI.edge2].v2];

                    Utils.SegmentIntersect(p1, p2, p3, p4, out intersectionPoint);

                    // Check if in correct direction
                    Vector3 newPointDirection = (intersectionPoint - lastPoint).normalized;
                    if (Vector3.Dot(newPointDirection, edgeDirection) > 0)
                    { // Same direction if dot is positive
                        if (Vector3.Distance(intersectionPoint, lastPoint) < minDist)
                        {
                            minDist = Vector3.Distance(intersectionPoint, lastPoint);
                            intersect = eI;

                        }
                    }
                }
            }

            if (minDist == float.MaxValue)
                return false;

            return true;
        }

        private bool BoundsOverlap(Bounds b1, Bounds b2)
        {
            return b1.min.x < b2.max.x && b1.max.x > b2.min.x && b1.max.z > b2.min.z && b1.min.z < b2.max.z;
        }

        private void DrawDebug()
        {
            foreach (MeshInfo mI in AllMeshes)
            {
                mI.Draw(Random.ColorHSV());
            }
        }

        private bool AnyEdgeOverlap(MeshInfo mesh1, MeshInfo mesh2)
        {
            foreach (EdgeInfo e1 in mesh1.OuterEdges)
            {
                Vector3 p1 = mesh1.Vertices[e1.v1];
                Vector3 p2 = mesh1.Vertices[e1.v2];

                foreach (EdgeInfo e2 in mesh2.OuterEdges)
                {
                    Vector3 p3 = mesh2.Vertices[e2.v1];
                    Vector3 p4 = mesh2.Vertices[e2.v2];
                    Vector3 intersectionPoint = new Vector3();
                    if (Utils.SegmentIntersect(p1, p2, p3, p4, out intersectionPoint))
                        return true;
                }
            }

            return false;
        }

        private (List<EdgeIntersect>, List<int>, List<int>) GetAllEdgeIntersections(List<Vector3> vertices1, List<Vector3> vertices2, ref List<EdgeInfo> edges1, ref List<EdgeInfo> edges2, bool debug = false)
        {
            List<EdgeIntersect> intersections = new List<EdgeIntersect>();

            List<Vector3> sharedPoints = new List<Vector3>();
            List<int> m1Shared = new List<int>();
            List<int> m2Shared = new List<int>();

            for (int i = 0; i < edges1.Count; i++)
            {
                EdgeInfo e1 = edges1[i];
                Vector3 p1 = vertices1[e1.v1];
                Vector3 p2 = vertices1[e1.v2];


                List<EdgeIntersect> tempIntersections = new List<EdgeIntersect>();
                for (int j = 0; j < edges2.Count; j++)
                {
                    EdgeInfo e2 = edges2[j];
                    Vector3 p3 = vertices2[e2.v1];
                    Vector3 p4 = vertices2[e2.v2];
                    Vector3 intersectionPoint = new Vector3();

                    int commonVertexCount = 0;
                    commonVertexCount = p1 == p3 ? commonVertexCount + 1 : commonVertexCount;
                    commonVertexCount = p1 == p4 ? commonVertexCount + 1 : commonVertexCount;
                    commonVertexCount = p2 == p3 ? commonVertexCount + 1 : commonVertexCount;
                    commonVertexCount = p2 == p4 ? commonVertexCount + 1 : commonVertexCount;

                    if (Utils.SegmentIntersect(p1, p2, p3, p4, out intersectionPoint))
                    {
                        if (debug)
                        {
                            InSceneDebugTool.Instance.DrawLine(p1, p2, Color.red);
                            InSceneDebugTool.Instance.DrawLine(p3, p4, Color.red);

                            InSceneDebugTool.Instance.DrawPoint(intersectionPoint, Color.red, .2f);
                            InSceneDebugTool.Instance.DrawPoint(p1, Color.blue, .2f);
                            InSceneDebugTool.Instance.DrawPoint(p2, Color.green, .2f);
                            InSceneDebugTool.Instance.DrawPoint(p3, Color.blue, .2f);
                            InSceneDebugTool.Instance.DrawPoint(p4, Color.green, .2f);
                        }

                        if (commonVertexCount == 0)
                            intersections.Add(new EdgeIntersect(i, j, intersectionPoint));

                        else if (commonVertexCount == 1)
                        {
                            // One shared vertex
                            // Check for m1
                            if (p1 == p3 || p1 == p4)
                                if (!m1Shared.Contains(e1.v1))
                                    m1Shared.Add(e1.v1);

                            if (p2 == p3 || p2 == p4)
                                if (!m1Shared.Contains(e1.v2))
                                    m1Shared.Add(e1.v2);

                            // Check for m2
                            if (p3 == p1 || p3 == p2)
                                if (!m2Shared.Contains(e2.v1))
                                    m2Shared.Add(e2.v1);

                            if (p4 == p1 || p4 == p2)
                                if (!m2Shared.Contains(e2.v2))
                                    m2Shared.Add(e2.v2);
                        }
                    }
                }
            }

            return (intersections, m1Shared, m2Shared);
        }

        private struct EdgeIntersect
        {
            public int edge1;
            public int edge2;
            public Vector3 intersectionPoint;

            public EdgeIntersect(int e1, int e2, Vector3 point)
            {
                edge1 = e1;
                edge2 = e2;
                intersectionPoint = point;
            }
        }
        #endregion
    }
}