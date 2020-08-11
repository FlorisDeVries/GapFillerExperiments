using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static IFC.EdgeHelper;
using static IFC.OverlapFixer;

namespace IFC
{
    public class MeshCreator : UnitySingleton<MeshCreator>
    {
        private List<Vector3> _vertices = new List<Vector3>();
        private List<GameObject> _vObjects = new List<GameObject>();
        private List<EdgeInfo> _edges = new List<EdgeInfo>();
        private List<LineRenderer> _lines = new List<LineRenderer>();
        private LineRenderer _currentLine = new LineRenderer();

        public GameObject PrefabObject = default;
        public Material MeshMaterial = default;
        public Button FinishButton = default;

        GameObject _drawingHoleIn = null;
        private List<Vector3> _boundaryVertices = new List<Vector3>();
        private List<EdgeInfo> _boundaryEdges = new List<EdgeInfo>();
        private List<Vector3> _outerBoundaryVertices = new List<Vector3>();
        private List<List<Vector2>> _holes = new List<List<Vector2>>();

        private void Start()
        {
            FinishButton.onClick.AddListener(FinishMesh);
            FinishButton.interactable = false;

            _currentLine = gameObject.AddComponent<LineRenderer>();

            _currentLine.material = new Material(Shader.Find("Particles/Standard Unlit"));

            _currentLine.startColor = Color.cyan;
            _currentLine.endColor = Color.cyan;

            _currentLine.startWidth = .1f;
            _currentLine.endWidth = .1f;

            _currentLine.enabled = false;
        }

        private void Update()
        {
            if (_vertices.Count > 0)
            {
                Vector3 worldSpaceMouse = Utils.GetPlaneIntersection(Input.mousePosition);
                _currentLine.SetPosition(1, worldSpaceMouse);

                if (IntersectAnyEdge(worldSpaceMouse, _vertices[_vertices.Count - 1], _edges, _vertices, false)
                || (_drawingHoleIn && IntersectAnyEdge(worldSpaceMouse, _vertices[_vertices.Count - 1], _boundaryEdges, _boundaryVertices, false)))
                {
                    _currentLine.startColor = Color.red;
                    _currentLine.endColor = Color.red;
                }
                else
                {
                    _currentLine.startColor = Color.cyan;
                    _currentLine.endColor = Color.cyan;
                }
            }
        }

        public void AddHoleVertex(Vector3 v, MeshClicker containing)
        {
            if (_vertices.Count == 0)
            {
                Mesh m = containing.GetComponent<MeshFilter>().sharedMesh;
                MeshInfo mI = new MeshInfo(m);
                if (Utils.PointInPolygon(v, mI))
                {
                    _drawingHoleIn = containing.gameObject;
                    _boundaryVertices = m.vertices.ToList();
                    _boundaryEdges = GetEdges(m.triangles).FindBoundary().SortEdges();


                    List<EdgeInfo> outerBoundary;
                    List<List<EdgeInfo>> holes;
                    Utils.ExtractLoopsFromMesh(m, out outerBoundary, out holes);

                    _outerBoundaryVertices = new List<Vector3>();
                    for (int i = 0; i < outerBoundary.Count; i++)
                    {
                        // Construct outer boundary
                        _outerBoundaryVertices.Add(_boundaryVertices[outerBoundary[i].v1]);
                        InSceneDebugTool.Instance.DrawEdge(outerBoundary[i], m, Color.red);
                    }

                    for (int l = 0; l < holes.Count; l++)
                    {
                        List<EdgeInfo> loop = holes[l];
                        foreach (EdgeInfo e in loop)
                        {
                            InSceneDebugTool.Instance.DrawEdge(e, m, Color.blue);
                        }
                        List<Vector2> hole = new List<Vector2>();
                        for (int i = 0; i < loop.Count; i++)
                        {
                            hole.Add(Utils.Vector2FromVector3(_boundaryVertices[loop[i].v1]));
                        }
                        _holes.Add(hole);
                    }
                }
            }
            AddVertex(v);
        }

        public void AddVertex(Vector3 v)
        {
            if (_vertices.Count > 0 && v == _vertices[0])
            {
                FinishMesh();
            }
            else
            {
                if (_vertices.Count > 0 && (IntersectAnyEdge(v, _vertices[_vertices.Count - 1], _edges, _vertices)
                || (_drawingHoleIn && IntersectAnyEdge(v, _vertices[_vertices.Count - 1], _boundaryEdges, _boundaryVertices))))
                    return;
                // Add vertices
                _vertices.Add(v);

                if (_vertices.Count > 1)
                    AddEdge(_vertices.Count - 2, _vertices.Count - 1);
                if (_vertices.Count > 2)
                    FinishButton.interactable = true;

                GameObject gO = Instantiate(PrefabObject);
                gO.GetComponent<VertexNode>().SetLocation(v);
                _vObjects.Add(gO);

                _currentLine.SetPosition(0, v);
                _currentLine.enabled = true;
            }
        }

        public void FinishMesh()
        {
            AddEdge(_vertices.Count - 1, 0);

            // We need at least 3 points to create a mesh
            if (_vertices.Count < 3)
                return;

            if (_drawingHoleIn)
            {
                List<Vector2> vertices2D = new List<Vector2>();
                for (int i = 0; i < _vertices.Count; i++)
                {
                    vertices2D.Add(Utils.Vector2FromVector3(_vertices[i]));
                }
                _holes.Add(vertices2D);
                _drawingHoleIn.SetActive(false);

                _vertices = _outerBoundaryVertices;
            }
            CreateMesh(_vertices, _holes, MeshMaterial.color);
        }

        public void CreateMeshes(List<MeshInfo> meshes, bool debug = false)
        {
            CreateMeshes(meshes, MeshMaterial.color, debug);
        }

        public void CreateMeshes(List<MeshInfo> meshes, Color color, bool debug = false)
        {
            foreach (MeshInfo mI in meshes)
                MeshCreator.Instance.CreateMesh(mI.GetOuterLoopVector3(), mI.GetHoles(), color);
        }

        public void CreateMesh(List<Vector3> vertices, List<List<Vector2>> holes, Color color, bool debug = false)
        {
            List<Vector2> vertices2D = new List<Vector2>();
            _vertices = vertices;
            for (int i = 0; i < vertices.Count; i++)
            {
                if (debug)
                    InSceneDebugTool.Instance.DrawPointWithDelay(i / 4f, vertices[i], Color.black, 0.2f + .01f * (i + 1));
                vertices2D.Add(new Vector2(vertices[i].x, vertices[i].z));
            }

            // Prepare gameObject
            GameObject meshObject = new GameObject("MeshObject");
            meshObject.transform.SetParent(MeshesParent.Instance.transform);

            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material.color = color;

            // Generate Mesh
            meshFilter.sharedMesh = MeshGenerator.GenerateMeshTriangulator(vertices2D, holes);

            if (meshFilter.sharedMesh.vertexCount == 0)
                Destroy(meshObject);

            meshObject.AddComponent<MeshClicker>();
            meshObject.GetComponent<MeshCollider>().convex = true;
            Reset();
        }

        public void Reset()
        {
            // Cleanup Step
            foreach (GameObject gO in _vObjects)
                Destroy(gO);
            _vObjects = new List<GameObject>();

            foreach (LineRenderer line in _lines)
                Destroy(line.gameObject);
            _lines = new List<LineRenderer>();

            _vertices = new List<Vector3>();
            _holes = new List<List<Vector2>>();
            _edges = new List<EdgeInfo>();
            _boundaryEdges = new List<EdgeInfo>();

            _currentLine.enabled = false;
            _drawingHoleIn = null;
            FinishButton.interactable = false;
        }

        private bool IntersectAnyEdge(Vector3 v1, Vector3 v2, List<EdgeInfo> edges, List<Vector3> vertices, bool debug = false)
        {
            if (v1 == v2)
                return false;

            foreach (EdgeInfo edge in edges)
            {
                Vector3 v3 = vertices[(int)edge.v1];
                Vector3 v4 = vertices[(int)edge.v2];

                if (v2 == v3 || v2 == v4)
                    return false;

                Vector3 intersectionPoint = new Vector3();
                if (Utils.SegmentIntersect(v1, v2, v3, v4, out intersectionPoint, debug))
                {
                    return true;
                }
            }
            return false;
        }

        private bool AddEdge(int idx1, int idx2)
        {
            if (idx1 == idx2)
                return false;

            int min = Mathf.Min(idx1, idx2);
            int max = Mathf.Max(idx1, idx2);
            if (_edges.Contains(new EdgeInfo(min, max)))
            {
                return false;
            }
            else
            {
                GameObject edge = new GameObject("Edge");
                LineRenderer line = edge.AddComponent<LineRenderer>();

                line.material = new Material(Shader.Find("Particles/Standard Unlit"));

                line.startColor = Color.blue;
                line.endColor = Color.blue;

                line.startWidth = .1f;
                line.endWidth = .1f;

                line.SetPosition(0, _vertices[idx1]);
                line.SetPosition(1, _vertices[idx2]);

                _lines.Add(line);

                _edges.Add(new EdgeInfo(min, max));
                return true;
            }
        }
    }
}