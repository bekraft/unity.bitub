using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace UnityBitub.Geometry
{
    /// <summary>
    /// A triangle mesher. Calculates mesh, UV and tangents. Works with a predefined threshold of a number
    /// of triangles. If threshold is reached, a gameobject is flushed and emitted via emitter delegate.
    /// <p>
    /// The UV coordinates are computed on base of XY parallel U axis (as often given in building structures).
    /// </p>
    /// </summary>
    public class TriangleMeshBuilder
    {
        #region Private members

        private List<Vector3> m_xyz = new List<Vector3>();
        private Dictionary<int, List<Triangle>> m_triangles = new Dictionary<int, List<Triangle>>();

        private EmitNewGameObject m_emitter;
        private int m_triangleThreshold;
        private GameObject m_template;
        private Buffer m_buffer;
        private string m_objectName = "Unknown";

        #endregion

        /// <summary>
        /// The scale factor for textures. A scale of 2.0 means to expand texture to 2 units.
        /// </summary>
        public float TextureScale = 1.0f;

        /// <summary>
        /// Total triangle count processed.
        /// </summary>
        public long TotalTriangleCount { get; private set; }

        /// <summary>
        /// A triangle defined by its three vertices.
        /// </summary>
        class Triangle
        {
            public int V1 { get; private set; }
            public int V2 { get; private set; }
            public int V3 { get; private set; }
            public IList<Vector3> XYZ {get; private set; }

            public Triangle(IList<Vector3> v, int a, int b, int c)
            {
                XYZ = v;
                V1 = a;
                V2 = b;
                V3 = c;
            }

            public bool IsValid
            {
                get { return V1 != V2 && V2 != V3 && V3 != V1; }
            }

            public Vector3 A
            {
                get { return XYZ[V1];  }
            }

            public Vector3 B
            {
                get { return XYZ[V2]; }
            }

            public Vector3 C
            {
                get { return XYZ[V3]; }
            }

            public Vector3 Normal
            {
                get
                {
                    var normal = Vector3.Cross(B-A, C-A);
                    normal.Normalize();
                    return normal;
                }
            }

            public void ComputeUVs(out Vector3 ex, out Vector3 ey, float tiling)
            {
                // Normalized planar UV vectors
                var normal = Normal;
                if (Math.Abs(normal.y) > Vector3.kEpsilon)
                {
                    // If y is not near zero
                    ex = new Vector3(1, -normal.x / normal.y, 0);
                }
                else if (Math.Abs(normal.x) > Vector3.kEpsilon)
                {
                    // Otherwise
                    ex = new Vector3(-normal.y / normal.x, 1, 0);
                }
                else
                {
                    // Use standard axis (1,0,0) since plane of triangle is parallel to XY
                    ex = Vector3.one;
                }

                ey = Vector3.Cross(normal, ex);
                ex.Normalize();
                ex *= 1 / tiling;
                ey.Normalize();
                ey *= 1 / tiling;
            }

            public void MapBuffer(int baseIdx, Vector2 baseUV, Buffer buffer)
            {

            }

            public void MapTriangle(IDictionary<int, List<Triangle>> map)
            {
                List<Triangle> starOf;
                if (!map.TryGetValue(V1, out starOf))
                {
                    starOf = new List<Triangle>();
                    map.Add(V1, starOf);
                }
                starOf.Add(this);

                if (!map.TryGetValue(V2, out starOf))
                {
                    starOf = new List<Triangle>();
                    map.Add(V2, starOf);
                }
                starOf.Add(this);

                if (!map.TryGetValue(V3, out starOf))
                {
                    starOf = new List<Triangle>();
                    map.Add(V3, starOf);
                }
                starOf.Add(this);
            }
        }

        /// <summary>
        /// A buffer to collect mesh data.
        /// </summary>
        class Buffer
        {
            // Global to local index
            internal Dictionary<int, int> Index = new Dictionary<int, int>();
            // Triangles (sequence of 3 subsequent indizes)
            internal List<int> Triangles = new List<int>();
            // UVs per vertex
            internal List<Vector2> UV = new List<Vector2>();
            // Tangent per vertex
            internal List<Vector4> Tangents = new List<Vector4>();
            // Vertex
            internal List<Vector3> XYZ = new List<Vector3>();
            // Normal per Vertex
            internal List<Vector3> Normal = new List<Vector3>();

            internal Buffer Remap(params int[] idxs)
            {
                var buffer = new Buffer();

                int li = 0;
                foreach(int i in idxs)
                {
                    // Determine old index
                    var oi = Index[i];
                    // Re-map to new
                    buffer.Index.Add(i, li);
                    buffer.UV.Add(UV[oi]);
                    buffer.Tangents.Add(Tangents[oi]);
                    buffer.XYZ.Add(XYZ[oi]);
                    buffer.Normal.Add(Normal[oi]);

                    li++;
                }

                return buffer;
            }

        }

        /// <summary>
        /// Delegates which takes the new game object.
        /// </summary>
        /// <param name="go"></param>
        public delegate void EmitNewGameObject(GameObject go);

        /// <summary>
        /// A triangle mesh builder based on local texture coordinate transformation
        /// </summary>
        /// <param name="maxTriangles">Maximum of triangles per game object.</param>
        /// <param name="templateObject">The template object to clone to carry a mesh.</param>
        public TriangleMeshBuilder(int maxTriangles, GameObject templateObject)
        {
            m_triangleThreshold = maxTriangles;
            m_template = templateObject;
        }

        /// <summary>
        /// Starts meshing.
        /// </summary>
        /// <param name="objName">Some name for logging.</param>
        /// <param name="handler">A delegate taken to emit new GameObjects to.</param>
        public void StartMeshing(string objName, EmitNewGameObject handler)
        {
            m_objectName = objName;
            m_emitter = handler;

            m_triangles.Clear();
            m_xyz.Clear();
            m_buffer = new Buffer();
        }

        public void AppendPoint(Vector3 p)
        {
            m_xyz.Add(p);
        }

        public void StartMeshFace()
        {
            m_triangles.Clear();
            m_buffer.Index.Clear();
        }

        /// <summary>
        /// Flushes the buffer to mesh.
        /// </summary>
        public void EndMeshFace()
        {
            FlushMeshBuffer();
        }

        /// <summary>
        /// Appends a triangle to mesh buffer.
        /// </summary>
        /// <param name="a">Know vertex A.</param>
        /// <param name="b">B</param>
        /// <param name="c">C</param>
        /// <returns>True, if appended; False if invalid.</returns>
        public bool AppendTriangle(int a, int b, int c)
        {
            var t = new Triangle(m_xyz, a, b, c);
            if(!t.IsValid)
            {
                Debug.LogWarning(string.Format("({0}) Detected topologically invalid triangle (index {1},{2},{3}).",m_objectName, a,b,c));
                return false;
            }

            t.MapTriangle(m_triangles);
            return true;
        }

        /// <summary>
        /// Flushes buffer and clears it.
        /// </summary>
        public void EndMeshing()
        {
            FlushMeshBuffer();
            FlushMeshObject();

            m_xyz.Clear();
            m_triangles.Clear();
            m_buffer = null;
        }

        /// <summary>
        /// Flushes the geometry proxy. Adds a MeshFilter to new GameObject.
        /// </summary>
        protected void FlushMeshObject()
        {
            var gameObject = GameObject.Instantiate(m_template) as GameObject;

            // Set mesh
            var meshFilter = gameObject.GetComponent<MeshFilter>();
            if (null == meshFilter)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            var mesh = meshFilter.sharedMesh;
            if (null == mesh)
            {
                mesh = new Mesh();
                gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            mesh.vertices = m_buffer.XYZ.ToArray();
            mesh.uv = m_buffer.UV.ToArray();
            mesh.triangles = m_buffer.Triangles.ToArray();

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            mesh.tangents = m_buffer.Tangents.ToArray();

            // Notify emitter
            if (null != m_emitter)
                m_emitter(gameObject);
        }

        /// <summary>
        /// Maps the given vertex by its index to local buffer.
        /// </summary>
        /// <param name="id">The global (original) index.</param>
        /// <param name="uv">The UV coordinates</param>
        /// <param name="t">The tangent</param>
        /// <returns>True, if not known before.</returns>
        private void MapVertex(int id, Vector3 n, Vector2 uv, Vector4 t)
        {
            int localIndex = m_buffer.XYZ.Count;
            if (!m_buffer.Index.ContainsKey(id))
                m_buffer.Index.Add(id, localIndex);
            else
                return;

            m_buffer.XYZ.Add(m_xyz[id]);
            m_buffer.Normal.Add(n);
            m_buffer.UV.Add(uv);            
            m_buffer.Tangents.Add(t);
        }

        private void MapTriangle(Triangle t)
        {
            m_buffer.Triangles.Add(m_buffer.Index[t.V1]);
            m_buffer.Triangles.Add(m_buffer.Index[t.V2]);
            m_buffer.Triangles.Add(m_buffer.Index[t.V3]);
        }

        protected void FlushMeshBuffer()
        {
            if (0 == m_triangles.Count)
            {
                return;
            }

            HashSet<Triangle> visitedTriangleSet = new HashSet<Triangle>();
            Queue<int> indexQueue = new Queue<int>();
            var idx = m_triangles.Keys.FirstOrDefault<int>();
            indexQueue.Enqueue(idx);
            Queue<Vector2> uvQueue = new Queue<Vector2>();
            // Start by zero origin
            uvQueue.Enqueue(Vector2.zero);

            // Compute initial vertex tangents
            var initialTriangle = m_triangles[idx].First<Triangle>();
            Vector3 ex, ey;
            initialTriangle.ComputeUVs(out ex, out ey, TextureScale);
            MapVertex(idx, initialTriangle.Normal, Vector2.zero, new Vector4(ex.x, ex.y, ex.z, -1));

            while(indexQueue.Count > 0)
            {
                // Poll base index
                idx = indexQueue.Dequeue();
                var starOf = m_triangles[idx];
                // Poll base offset
                var uv0 = uvQueue.Dequeue();

                foreach(Triangle t in starOf)
                {
                    if (visitedTriangleSet.Contains(t))
                    {
                        continue;
                    }
                    visitedTriangleSet.Add(t);

                    // Compute UVs                    
                    t.ComputeUVs(out ex, out ey, TextureScale);

                    // Calculate local texture coordinates
                    int v1, v2;
                    
                    if(idx == t.V1)
                    {
                        v1 = t.V2;
                        v2 = t.V3;
                    }
                    else if (idx == t.V2)
                    {
                        v1 = t.V1;
                        v2 = t.V3;
                    }
                    else if (idx == t.V3)
                    {
                        v1 = t.V1;
                        v2 = t.V2;
                    }
                    else
                    {
                        // Shouldn't happen
                        throw new Exception("Vertex ID does not belong to triangle.");
                    }

                    // Build local coordinates
                    var p0 = m_xyz[idx];
                    var puv1 = m_xyz[v1] - p0;
                    var puv2 = m_xyz[v2] - p0;

                    // Build UV coordinates using planar unit vectors and base offset                    
                    if (!m_buffer.Index.ContainsKey(v1))
                    {
                        var uv1 = new Vector2(Vector3.Dot(puv1, ex), Vector3.Dot(puv1, ey)) + uv0;
                        MapVertex(v1, t.Normal, uv1, new Vector4(ex.x, ex.y, ex.z, -1));
                        indexQueue.Enqueue(v1);
                        uvQueue.Enqueue(uv1);
                    }
                    if (!m_buffer.Index.ContainsKey(v2))
                    {
                        var uv2 = new Vector2(Vector3.Dot(puv2, ex), Vector3.Dot(puv2, ey)) + uv0;
                        MapVertex(v2, t.Normal, uv2, new Vector4(ex.x, ex.y, ex.z, -1));
                        indexQueue.Enqueue(v2);
                        uvQueue.Enqueue(uv2);
                    }

                    // Map triangle
                    MapTriangle(t);

                    // If exceeding threshold
                    if(m_buffer.Triangles.Count / 3 > m_triangleThreshold)
                    {
                        FlushMeshObject();
                        m_buffer = m_buffer.Remap(v1, v2);
                        TotalTriangleCount += m_triangles.Count / 3;
                    }
                }
            }

            // Clear all edges
            TotalTriangleCount += m_triangles.Count / 3;
            m_triangles.Clear();
        }
    }
}
