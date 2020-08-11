using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static IFC.EdgeHelper;

namespace IFC
{
    public class GapFiller : MonoBehaviour
    {
        public Button FixButton = default;
        public Button GenerateButton = default;
        List<MeshInfo> AllMeshes = new List<MeshInfo>();

        List<MeshInfo> AllGaps = new List<MeshInfo>();
        List<EdgeData> edges = new List<EdgeData>();
        List<EdgeData> AllEdges = new List<EdgeData>();

        Bounds meshesBounds = new Bounds();
        List<Vector3> BBVertices = new List<Vector3>();

        List<EdgeData> newEdges = new List<EdgeData>();
        Dictionary<(int, int), List<MeshInfo>> newMeshes = new Dictionary<(int, int), List<MeshInfo>>();

        private void Start()
        {
            FixButton.onClick.AddListener(() =>
            {
                FillGaps();
            });

            GenerateButton.onClick.AddListener(() =>
            {
                CreateMeshes(AllGaps, Random.ColorHSV());
            });
        }

        private void FillGaps()
        {
            OverlapFixer.Instance.FixOverlaps();
            SetMeshes();

            ExtractData();

            CreateBoundary();

            ConnectEdgesLoop();

            CombineNewMeshes();
        }

        public void SetMeshes()
        {
            InSceneDebugTool.Instance.ClearDebug();
            List<MeshFilter> filters = MeshesParent.Instance.GetComponentsInChildren<MeshFilter>().ToList();
            List<Mesh> meshes = new List<Mesh>();
            foreach (MeshFilter f in filters)
            {
                meshes.Add(f.sharedMesh);
                // Destroy(f.gameObject);
            }
            SetMeshes(meshes);
        }

        private void SetMeshes(List<Mesh> meshes)
        {
            Debug.Log(meshes.Count);
            AllMeshes = new List<MeshInfo>();

            foreach (Mesh m in meshes)
            {
                AllMeshes.Add(new MeshInfo(m));
            }
        }

        private void ExtractData()
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            meshesBounds = new Bounds();
            edges = new List<EdgeData>();

            for (int i = 0; i < AllMeshes.Count; i++)
            {
                MeshInfo m = AllMeshes[i];
                foreach (EdgeInfo eI in m.OuterEdges)
                {
                    eI.MeshIndex = i;
                    EdgeData eData = new EdgeData(eI.v1, eI.v2, i, i);

                    min = Vector3.Min(min, m.Vertices[eI.v1]);
                    max = Vector3.Max(max, m.Vertices[eI.v1]);
                    edges.Add(eData);
                    AllEdges.Add(eData);
                }
            }

            meshesBounds.SetMinMax(min, max);
        }

        private void SortEdges()
        {
            edges.Sort((x, y) => y.GetLength(AllMeshes).CompareTo(x.GetLength(AllMeshes)));
        }

        private void CreateBoundary()
        {
            BBVertices = new List<Vector3>();

            Vector3 p3 = new Vector3(meshesBounds.min.x, 0, meshesBounds.max.z);
            Vector3 p4 = new Vector3(meshesBounds.max.x, 0, meshesBounds.min.z);
            BBVertices.Add(meshesBounds.min);
            BBVertices.Add(p3);
            BBVertices.Add(meshesBounds.max);
            BBVertices.Add(p4);

            foreach (Vector3 v in BBVertices)
            {
                InSceneDebugTool.Instance.DrawPoint(v, Color.red, .3f);
            }

            MeshInfo m = new MeshInfo(BBVertices);
            // m.Draw(Color.green);

            // AllMeshes.Add(m);
        }

        private void ConnectEdgesLoop()
        {
            newMeshes = new Dictionary<(int, int), List<MeshInfo>>();

            while (edges.Count > 0)
            {
                SortEdges();
                ConnectEdges();
                edges = newEdges;
            }
        }

        private void ConnectEdges(bool debug = false)
        {
            newEdges = new List<EdgeData>();

            for (int i = 0; i < edges.Count; i++)
            {
                EdgeData eD = edges[i];
                int thirdPoint;
                int connectedIndex;
                (thirdPoint, connectedIndex) = FindClosestVertex(eD);

                // If no thirdPoint was found
                if (connectedIndex == -1)
                    continue;

                // Make sure every edge has the smallest meshIndex, while connected to the larger
                EdgeData edge1 = new EdgeData(eD.V1, thirdPoint, eD.M1, connectedIndex);
                EdgeData edge2 = new EdgeData(thirdPoint, eD.V2, connectedIndex, eD.M2);

                // If the reverse exists we remove the edge instead of adding, if an edge exists twice it is enclosed and thus won't be necassary for the next stap
                AddEdge(edge1);
                AddEdge(edge2);


                ConstructNewMesh(edge1, edge2);

                if (debug)
                {
                    InSceneDebugTool.Instance.DrawLineWithDelay(0, eD.GetCenter(AllMeshes), eD.GetCenter(AllMeshes) + eD.GetNormal(AllMeshes), Color.red);
                    InSceneDebugTool.Instance.DrawPointWithDelay(i / 4f, AllMeshes[connectedIndex].Vertices[thirdPoint], Color.green, .4f);
                }
            }
        }

        private void AddEdge(EdgeData edge)
        {
            EdgeData reversedEdge = new EdgeData(edge.V2, edge.V1, edge.M2, edge.M1);
            if (newEdges.Contains(reversedEdge))
            {
                newEdges.Remove(reversedEdge);

            }
            else if (newEdges.Contains(edge))
            {
                newEdges.Remove(edge);
            }
            else if (!(AllEdges.Contains(edge) || AllEdges.Contains(reversedEdge)))
            {
                newEdges.Add(edge);
                AllEdges.Add(edge);
            }
        }

        private (int, int) FindClosestVertex(EdgeData edge)
        {
            Vector3 closest = Vector3.zero;
            int closestIdx = -1;
            float minDist = float.MaxValue;
            int mIndex = -1;
            Vector3 v1 = AllMeshes[edge.M1].Vertices[edge.V1];
            Vector3 v2 = AllMeshes[edge.M2].Vertices[edge.V2];

            for (int i = 0; i < AllMeshes.Count; i++)
            {
                MeshInfo m = AllMeshes[i];

                // Walking over outerEdges so we ignore inner loops, those need to be solved seperately
                for (int eIdx = 0; eIdx < m.OuterEdges.Count; eIdx++)
                {
                    Vector3 v = m.Vertices[m.OuterEdges[eIdx].v1];
                    if (v == v1 || v == v2)
                        continue;

                    Vector3 newPointDirection = (v - edge.GetCenter(AllMeshes));
                    float dot = Vector3.Dot(newPointDirection.normalized, edge.GetNormal(AllMeshes));
                    float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
                    // Same direction/not opposite direction
                    if (dot > 0 && angle < 87.5 && !Utils.PointInPolygon(edge.GetCenter(AllMeshes) + newPointDirection * .5f, m))
                    {
                        float newDist = Vector3.Distance(v, edge.GetCenter(AllMeshes));
                        if (newDist * (Mathf.PI - dot) < minDist)
                        {
                            if (!IntersectsAnyEdge(v1, v) && !IntersectsAnyEdge(v2, v))
                            {
                                minDist = newDist * (Mathf.PI - dot);
                                closest = v;
                                closestIdx = m.OuterEdges[eIdx].v1;
                                mIndex = i;
                            }
                        }
                    }
                }
            }

            return (closestIdx, mIndex);
        }

        private bool IntersectsAnyEdge(Vector3 p1, Vector3 p2)
        {
            // Check for new edges
            foreach (EdgeData edge in AllEdges)
            {
                Vector3 p3 = AllMeshes[edge.M1].Vertices[edge.V1];
                Vector3 p4 = AllMeshes[edge.M2].Vertices[edge.V2];

                if (p1 == p3 && p2 == p4 || p2 == p3 && p1 == p4)
                    continue;

                Vector3 intersectionPoint = new Vector3();
                if (Utils.SegmentIntersect(p1, p2, p3, p4, out intersectionPoint, false))
                {
                    if (intersectionPoint != p1 && intersectionPoint != p2)
                        return true;
                }
            }
            return false;
        }

        private void ConstructNewMesh(EdgeData edge1, EdgeData edge2)
        {
            List<Vector3> vertices = new List<Vector3>();
            vertices.Add(AllMeshes[edge1.M1].Vertices[edge1.V1]);
            vertices.Add(AllMeshes[edge1.M2].Vertices[edge1.V2]);
            vertices.Add(AllMeshes[edge2.M2].Vertices[edge2.V2]);
            MeshInfo m = new MeshInfo(vertices);

            int min = Mathf.Min(edge1.M1, edge1.M2);
            int max = Mathf.Max(edge1.M1, edge1.M2);
            (int, int) connection = (-1, -1);
            // If it connects two meshes we keep their indices
            if ((edge1.M1 == edge2.M1 || edge1.M1 == edge2.M2) && (edge1.M2 == edge2.M1 || edge1.M2 == edge2.M2))
            {
                connection = (min, max);
            }

            if (!newMeshes.ContainsKey(connection))
                newMeshes.Add(connection, new List<MeshInfo>());
            newMeshes[connection].Add(m);
        }

        private void CombineNewMeshes()
        {
            foreach (KeyValuePair<(int, int), List<MeshInfo>> pair in newMeshes)
            {
                if (pair.Key.Item1 == -1 || pair.Key.Item1 == -1)
                    continue;

                List<MeshInfo> overlapped = OverlapFixer.Instance.FixOverlapsFor(pair.Value);
                CreateMeshes(overlapped, Color.grey);
                foreach (MeshInfo m in overlapped)
                {
                    AllGaps.Add(m);
                    m.Draw(Random.ColorHSV(0f, 1f, 1f, 1f, 1f, 1f));
                }
            }
        }

        public void CreateMeshes(List<MeshInfo> meshes, Color color, bool debug = false)
        {
            foreach (MeshInfo mI in meshes)
                CreateMesh(mI.GetOuterLoop(), new List<List<Vector2>>(), color);
        }

        public void CreateMesh(List<Vector2> vertices2D, List<List<Vector2>> holes, Color color, bool debug = false)
        {
            // Prepare gameObject
            GameObject meshObject = new GameObject("MeshObject");
            meshObject.transform.SetParent(GapsParent.Instance.transform);

            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material.color = color;

            // Generate Mesh
            meshFilter.sharedMesh = MeshGenerator.GenerateMeshTriangulator(vertices2D, holes);

            if (meshFilter.sharedMesh.vertexCount == 0)
                Destroy(meshObject);

            meshObject.AddComponent<GapSelector>();
            meshObject.GetComponent<MeshCollider>().convex = true;
        }

        protected struct EdgeData
        {
            public int V1;
            public int V2;
            public int M1;
            public int M2;

            public EdgeData(int v1, int v2, int m1, int m2)
            {
                V1 = v1;
                V2 = v2;
                M1 = m1;
                M2 = m2;
            }

            public void Draw(Color color, List<MeshInfo> AllMeshes)
            {
                Vector3 p1 = AllMeshes[M1].Vertices[V1];
                Vector3 p2 = AllMeshes[M2].Vertices[V2];
                InSceneDebugTool.Instance.DrawPoint(p1, Color.red, .2f);
                InSceneDebugTool.Instance.DrawPoint(p2, Color.green, .2f);
                InSceneDebugTool.Instance.DrawLine(p1, p2, color);
            }

            public void DrawWithDelay(float delay, Color color, List<MeshInfo> AllMeshes)
            {
                Vector3 p1 = AllMeshes[M1].Vertices[V1];
                Vector3 p2 = AllMeshes[M2].Vertices[V2];

                InSceneDebugTool.Instance.DrawLineWithDelay(delay, p1, p2, color);
            }

            public Vector3 GetCenter(List<MeshInfo> AllMeshes)
            {
                Vector3 p1 = AllMeshes[M1].Vertices[V1];
                Vector3 p2 = AllMeshes[M2].Vertices[V2];

                return (p1 + p2) / 2;
            }

            public Vector3 GetNormal(List<MeshInfo> AllMeshes)
            {
                Vector3 p2 = AllMeshes[M1].Vertices[V1];
                Vector3 p1 = AllMeshes[M2].Vertices[V2];

                Vector3 dir = (p1 - p2).normalized;

                float theta = Mathf.Deg2Rad * 90f;
                float cos = Mathf.Cos(theta);
                float sin = Mathf.Sin(theta);

                return new Vector3(dir.x * cos - dir.z * sin, 0, dir.x * sin + dir.z * cos);
            }

            public float GetLength(List<MeshInfo> AllMeshes)
            {
                Vector3 p1 = AllMeshes[M1].Vertices[V1];
                Vector3 p2 = AllMeshes[M2].Vertices[V2];

                return (p1 - p2).magnitude;
            }
        }
    }
}