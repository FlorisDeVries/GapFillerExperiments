using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static IFC.EdgeHelper;

namespace IFC
{
    public class InSceneDebugTool : UnitySingleton<InSceneDebugTool>
    {
        private List<GameObject> _debugObjects = new List<GameObject>();
        public GameObject NodePrefab = default;


        public void DrawPoint(Vector3 p, Color color, float size = 1)
        {
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z))
                return;
            GameObject gO = Instantiate(NodePrefab, p, Quaternion.identity);
            gO.GetComponent<DebugNode>().SetColor(color);
            gO.GetComponent<DebugNode>().SetSize(size);
            gO.transform.SetParent(this.transform);
            _debugObjects.Add(gO);
        }

        public void DrawLine(Vector3 p1, Vector3 p2, Color color)
        {
            GameObject lineGo = new GameObject("Line");
            lineGo.transform.SetParent(this.transform);

            LineRenderer lr = lineGo.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Particles/Standard Unlit"));

            lr.startColor = color;
            lr.endColor = color;

            lr.startWidth = .05f;
            lr.endWidth = .05f;

            lr.SetPosition(0, p1);
            lr.SetPosition(1, p2);

            _debugObjects.Add(lineGo);
        }

        public void ClearDebug()
        {
            List<MeshFilter> filters = GapsParent.Instance.GetComponentsInChildren<MeshFilter>().ToList();
            foreach (MeshFilter f in filters)
                Destroy(f.gameObject);

            foreach (GameObject line in _debugObjects)
            {
                Destroy(line);
            }
            _debugObjects = new List<GameObject>();
        }

        public void DrawBoundary(Mesh mesh, bool outerOnly = false)
        {
            List<EdgeInfo> edges = GetEdges(mesh.triangles).FindBoundary().SortEdges();
            int v1 = edges[0].v1;
            foreach (EdgeInfo e in edges)
            {
                DrawLine(mesh.vertices[e.v1], mesh.vertices[e.v2], Color.black);
                if (outerOnly && e.v2 == v1)
                    break;
            }
        }

        public void DrawBounds(Bounds bounds)
        {
            Vector3 p1 = new Vector3(bounds.min.x, 0, bounds.min.z);
            Vector3 p2 = new Vector3(bounds.min.x, 0, bounds.max.z);
            Vector3 p3 = new Vector3(bounds.max.x, 0, bounds.max.z);
            Vector3 p4 = new Vector3(bounds.max.x, 0, bounds.min.z);

            DrawLine(p1, p2, Color.green);
            DrawLine(p2, p3, Color.green);
            DrawLine(p3, p4, Color.green);
            DrawLine(p4, p1, Color.green);
        }

        public void DrawPointWithDelay(float wait, Vector3 p, Color color, float size)
        {
            StartCoroutine(PointWithDelay(wait, p, color, size));
        }

        private IEnumerator PointWithDelay(float wait, Vector3 p, Color color, float size)
        {
            yield return new WaitForSeconds(wait);
            DrawPoint(p, color, size);
        }

        public IEnumerator DrawEdgeWithDelay(float wait, EdgeInfo e, Mesh m, Color color)
        {
            yield return new WaitForSeconds(wait);
            Vector3 p1 = m.vertices[e.v1];
            Vector3 p2 = m.vertices[e.v2];
            DrawLine(p1, p2, color);
        }

        public void DrawEdge(EdgeInfo e, Mesh m, Color color)
        {
            Vector3 p1 = m.vertices[e.v1];
            Vector3 p2 = m.vertices[e.v2];

            DrawLine(p1, p2, color);
        }

        
        public void DrawLineWithDelay(float wait, Vector3 p1, Vector3 p2, Color color)
        {
            StartCoroutine(LineWithDelay(wait, p1, p2, color));
        }

        public IEnumerator LineWithDelay(float wait, Vector3 p1, Vector3 p2, Color color)
        {
            yield return new WaitForSeconds(wait);
            DrawLine(p1, p2, color);
        }

        public void DrawEdge(EdgeInfo e, List<Vector3> vertices, Color color, float offset = 0)
        {
            Vector3 p1 = vertices[e.v1] + new Vector3(offset, 0, 0);
            Vector3 p2 = vertices[e.v2] + new Vector3(offset, 0, 0);

            DrawLine(p1, p2, color);
        }
    }
}