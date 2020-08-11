using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static IFC.EdgeHelper;

namespace IFC
{
    public class MeshInfo
    {
        private List<Vector2> _outerLoop = new List<Vector2>();
        private List<List<Vector2>> _holes = new List<List<Vector2>>();

        public List<EdgeInfo> OuterEdges = new List<EdgeInfo>();
        public List<List<EdgeInfo>> HolesEdges = new List<List<EdgeInfo>>();
        public List<Vector3> Vertices = new List<Vector3>();

        private Bounds _bounds = new Bounds();
        public Bounds Bounds { get { RecalculateBounds(); return _bounds; } }

        public MeshInfo(Mesh m)
        {
            Utils.ExtractLoopsFromMesh(m, out OuterEdges, out HolesEdges);
            // HolesEdges = new List<List<EdgeInfo>>();

            // TODO: Handle "HolesEdges", these are not holes most of the time, but more extenstions

            Vertices = m.vertices.ToList();
            RecalculateBounds();
        }

        public MeshInfo(List<Vector3> vertices)
        {
            SetVertices(vertices);

            HolesEdges = new List<List<EdgeInfo>>();

            RecalculateBounds();
        }

        public MeshInfo(List<Vector3> vertices, List<List<Vector3>> holes)
        {
            SetVertices(vertices);

            HolesEdges = new List<List<EdgeInfo>>();

            // Do something with the holes
            foreach (List<Vector3> list in holes)
            {
                List<EdgeInfo> holeLoop = new List<EdgeInfo>();
                int loopBeginIdx = vertices.Count;
                for (int i = 0; i < list.Count - 1; i++)
                {
                    holeLoop.Add(new EdgeInfo(loopBeginIdx + i, loopBeginIdx + i + 1));
                    vertices.Add(list[i]);
                }
                vertices.Add(list[list.Count - 1]);
                holeLoop.Add(new EdgeInfo(vertices.Count - 1, loopBeginIdx));
                HolesEdges.Add(holeLoop);
            }
        }

        private void SetVertices(List<Vector3> vertices)
        {
            Vertices = vertices;

            OuterEdges = new List<EdgeInfo>();
            for (int i = 0; i < vertices.Count - 1; i++)
            {
                OuterEdges.Add(new EdgeInfo(i, i + 1));
            }
            OuterEdges.Add(new EdgeInfo(vertices.Count - 1, 0));
        }

        public void RecalculateBounds()
        {
            _bounds = Utils.CalculateBounds(Vertices);
        }

        public List<Vector2> GetOuterLoop()
        {
            _outerLoop = new List<Vector2>();
            for (int e = 0; e < OuterEdges.Count; e++)
            {
                _outerLoop.Add(Utils.Vector2FromVector3(Vertices[OuterEdges[e].v1]));
            }
            return _outerLoop;
        }

        public List<Vector3> GetOuterLoopVector3()
        {
            List<Vector3> outerLoop = new List<Vector3>();
            for (int e = 0; e < OuterEdges.Count; e++)
            {
                outerLoop.Add(Vertices[OuterEdges[e].v1]);
            }
            return outerLoop;
        }

        public List<List<Vector2>> GetHoles()
        {
            _holes = new List<List<Vector2>>();
            List<Vector2> holeLoop = new List<Vector2>();
            for (int l = 0; l < HolesEdges.Count; l++)
            {
                holeLoop = new List<Vector2>();
                for (int h = 0; h < HolesEdges[l].Count; h++)
                {
                    holeLoop.Add(Utils.Vector2FromVector3(Vertices[HolesEdges[l][h].v1]));
                }
                _holes.Add(holeLoop);
            }
            return _holes;
        }

        public void Draw(Color color, float offset = 0)
        {
            foreach (EdgeInfo eI in OuterEdges)
            {
                InSceneDebugTool.Instance.DrawEdge(eI, Vertices, color, offset);
            }

            for (int i = 0; i < HolesEdges.Count; i++)
            {
                Color holeColor = Random.ColorHSV(0, 1, 1, 1, 1, 1);
                List<EdgeInfo> hole = HolesEdges[i];
                foreach (EdgeInfo eI in hole)
                    InSceneDebugTool.Instance.DrawEdge(eI, Vertices, holeColor, offset);
            }

            // InSceneDebugTool.Instance.DrawBounds(Bounds);
        }
    }
}