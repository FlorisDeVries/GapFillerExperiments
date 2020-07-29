using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static IFC.EdgeHelper;

namespace IFC
{
    public class GapFiller : MonoBehaviour
    {
        public Button FixButton = default;
        List<MeshInfo> AllMeshes = new List<MeshInfo>();
        List<EdgeData> edges = new List<EdgeData>();
        Bounds meshesBounds = new Bounds();

        List<EdgeData> newEdges = new List<EdgeData>();

        private void Start()
        {
            FixButton.onClick.AddListener(() =>
            {
                FillGaps();
            });
        }

        private void FillGaps()
        {
            OverlapFixer.Instance.FixOverlaps();
            SetMeshes();

            ExtractData();
            SortEdges();

            CreateBoundary();

            FillLoop();
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
                    EdgeData eData = new EdgeData(m.Vertices[eI.v1], m.Vertices[eI.v2], i);

                    min = Vector3.Min(min, m.Vertices[eI.v1]);
                    max = Vector3.Max(max, m.Vertices[eI.v1]);
                    edges.Add(eData);
                }
            }

            meshesBounds.SetMinMax(min, max);
        }

        private void SortEdges()
        {
            edges.Sort((x, y) => x.Length.CompareTo(y.Length));
        }

        private void CreateBoundary()
        {
            List<Vector3> vertices = new List<Vector3>();

            Vector3 p3 = new Vector3(meshesBounds.min.x, 0, meshesBounds.max.z);
            Vector3 p4 = new Vector3(meshesBounds.max.x, 0, meshesBounds.min.z);
            vertices.Add(meshesBounds.min);
            vertices.Add(p3);
            vertices.Add(meshesBounds.max);
            vertices.Add(p4);

            foreach (Vector3 v in vertices)
            {
                InSceneDebugTool.Instance.DrawPoint(v, Color.red, .3f);
            }

            MeshInfo m = new MeshInfo(vertices);
            m.Draw(Color.green);

            AllMeshes.Add(m);
        }

        private void FillLoop()
        {
            newEdges = new List<EdgeData>();

            for (int i = 0; i < edges.Count; i++)
            {
                EdgeData eD = edges[i];
                Vector3 thirdPoint = FindClosestVertex(eD);

                newEdges.Add(new EdgeData(eD.V1, thirdPoint, -1));
                newEdges.Add(new EdgeData(eD.V2, thirdPoint, -1));

                InSceneDebugTool.Instance.DrawLineWithDelay(0, eD.V1, eD.V2, Color.black);
                InSceneDebugTool.Instance.DrawLineWithDelay(0, eD.V1, thirdPoint, Color.black);
                InSceneDebugTool.Instance.DrawLineWithDelay(0, thirdPoint, eD.V2, Color.black);

                InSceneDebugTool.Instance.DrawLineWithDelay(0, eD.Center, eD.Center + eD.Normal, Color.red);

                InSceneDebugTool.Instance.DrawPointWithDelay(0, thirdPoint, Color.green, .4f);
            }
        }

        private Vector3 FindClosestVertex(EdgeData edge)
        {
            Vector3 closest = Vector3.zero;
            float minDist = float.MaxValue;

            for (int i = 0; i < AllMeshes.Count; i++)
            {
                if (edge.MeshIndex == i)
                {
                    continue;
                }
                MeshInfo m = AllMeshes[i];

                foreach (Vector3 v in m.Vertices)
                {
                    Vector3 newPointDirection = (v - edge.Center).normalized;
                    // Same direction/not opposite direction
                    if (Vector3.Dot(newPointDirection, edge.Normal) > 0)
                    {
                        float newDist = Vector3.Distance(v, edge.Center);
                        if (newDist < minDist)
                        {
                            if (!IntersectsAnyEdge(edge.V1, v) && !IntersectsAnyEdge(edge.V2, v))
                            {
                                minDist = newDist;
                                closest = v;
                            }
                        }
                    }
                }
            }
            if (closest == Vector3.zero)
                Debug.Log("This is wrong");
            return closest;
        }

        private bool IntersectsAnyEdge(Vector3 p1, Vector3 p2)
        {
            foreach (EdgeData edge in newEdges)
            {
                Vector3 p3 = edge.V1;
                Vector3 p4 = edge.V2;

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

        protected struct EdgeData
        {
            public Vector3 V1;
            public Vector3 V2;
            public Vector3 Center;
            public Vector3 Normal;
            public float Length;
            public int MeshIndex;

            public EdgeData(Vector3 vec1, Vector3 vec2, int mIndex)
            {
                V1 = vec1;
                V2 = vec2;
                Length = (V1 - V2).magnitude;
                MeshIndex = mIndex;
                Center = (V1 + V2) / 2;

                Vector3 dir = (vec2 - vec1).normalized;

                float theta = Mathf.Deg2Rad * 90f;
                float cos = Mathf.Cos(theta);
                float sin = Mathf.Sin(theta);

                Normal = new Vector3(dir.x * cos - dir.z * sin, 0, dir.x * sin + dir.z * cos);
            }
        }
    }
}