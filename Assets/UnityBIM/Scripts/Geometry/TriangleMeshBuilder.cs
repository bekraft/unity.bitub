using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace UnityBitub.Geometry
{
    /// <summary>
    /// A triangle defined by its three vertices.
    /// </summary>
    public class Triangle
    {
        public enum Orientation
        {
            Unconnected, Aligned, Opposed
        }

        public int V1 { private set; get; }
        public int V2 { private set; get; }
        public int V3 { private set; get; }

        internal Triangle(int a, int b, int c)
        {
            V1 = a;
            V2 = b;
            V3 = c;
        }

        public bool IsValid
        {
            get { return V1 != V2 && V2 != V3 && V3 != V1; }
        }

        public int this[int index]
        {
            get {
                switch((index % 3 + 3) % 3)
                {
                    case 0:
                        return V1;
                    case 1:
                        return V2;
                    case 2:
                        return V3;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public int Find(int v)
        {
            if (v == V1)
                return 0;
            else if (v == V2)
                return 1;
            else if (v == V3)
                return 2;
            else
                return -1;
        }

        public void flip()
        {
            int tmp = V1;
            V1 = V2;
            V2 = V1;
        }

        private bool IsAligned(Triangle other, int thisIndex, int otherIndex)
        {
            return this[thisIndex + 1] == other[otherIndex - 1] || this[thisIndex - 1] == other[otherIndex + 1];
        }

        private bool IsOpposed(Triangle other, int thisIndex, int otherIndex)
        {
            return this[thisIndex + 1] == other[otherIndex + 1] || this[thisIndex - 1] == other[otherIndex - 1];
        }

        public Orientation GetOrientationAt(Triangle other, int v)
        {
            int thisIndex = Find(v);
            int otherIndex = other.Find(v);

            if (0 > thisIndex || 0 > otherIndex)
            {
                return Orientation.Unconnected;
            }
            else if (IsAligned(other, thisIndex, otherIndex))
            {
                return Orientation.Aligned;
            }
            else if (IsOpposed(other, thisIndex, otherIndex))
            {
                return Orientation.Opposed;
            } 
            else
            {
                throw new Exception();
            }
        }
    }

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
        private GameObject m_singletonObject;

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
            // Repeats given indexed vertices
            internal Buffer Repeat(params int[] indexes)
            {
                var buffer = new Buffer();

                int newIndex = 0;
                foreach(int i in indexes)
                {
                    var oi = Index[i];
                    // Add mapping from i-th to a new vertex at the end of the buffer
                    buffer.Index.Add(i, newIndex);
                    // Append a new vertex (repeating i-th)
                    buffer.UV.Add(UV[oi]);
                    buffer.Tangents.Add(Tangents[oi]);
                    buffer.XYZ.Add(XYZ[oi]);
                    buffer.Normal.Add(Normal[oi]);

                    newIndex++;
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
        /// <param name="singleton">Some singleton game object as initial container</param>
        /// <param name="handler">A delegate taken to emit new GameObjects to.</param>
        public void StartMeshing(string objName, GameObject singleton, EmitNewGameObject handler)
        {
            Debug.Log(string.Format("Start meshing {0} ...", objName));
            m_objectName = objName;
            m_emitter = handler;

            m_triangles.Clear();
            m_xyz.Clear();
            m_buffer = new Buffer();
            m_singletonObject = singleton;
        }

        /// <summary>
        /// Appends a face's point.
        /// </summary>
        /// <param name="p">The point as localized vector</param>
        public void AppendPoint(Vector3 p)
        {
            m_xyz.Add(p);
        }

        /// <summary>
        /// Clears current face buffer and starts meshing a new face.
        /// </summary>
        public void StartMeshFace()
        {
            m_triangles.Clear();
            // Force overwriting of uvs and normals (new face)
            m_buffer.Index.Clear();
        }

        /// <summary>
        /// Flushes the face buffer to mesh.
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
            var t = new Triangle(a, b, c);
            if(!t.IsValid)
            {
                Debug.LogWarning(string.Format("({0}) Detected topologically invalid triangle (index {1},{2},{3}).",m_objectName, a,b,c));
                return false;
            }

            StoreTriangle(t);
            return true;
        }

        /// <summary>
        /// Flushes buffer and clears it.
        /// </summary>
        public void EndMeshing()
        {
            FlushMeshBuffer();
            EmitGameObject();
            // Clear face's buffering
            m_xyz.Clear();
            m_triangles.Clear();
            m_buffer = null;
        }

        /// <summary>
        /// Flushes the geometry proxy. Adds a MeshFilter to new GameObject.
        /// </summary>
        private void EmitGameObject()
        {
            if (0 == m_buffer.Triangles.Count)
            {
                Debug.Log(string.Format("Skipping empty mesh object ({0}).", m_objectName));
                return;
            }

            // If there's a singleton, use it, otherwise create a new game object
            var gameObject = (null!=m_singletonObject? m_singletonObject : GameObject.Instantiate(m_template) as GameObject);

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

            // Notify emitter (if not singleton)
            if (null != m_emitter)
            {
                m_emitter(gameObject);
                m_singletonObject = null;
            }
        }

        /// <summary>
        /// Maps the given vertex by its index to local buffer.
        /// </summary>
        /// <param name="id">The global (original) index.</param>
        /// <param name="uv">The UV coordinates</param>
        /// <param name="t">The tangent</param>
        /// <returns>True, if not known before.</returns>
        private void BufferVertex(int id, Vector3 n, Vector2 uv, Vector4 t)
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

        private void BufferTriangle(Triangle t)
        {
            m_buffer.Triangles.Add(m_buffer.Index[t.V1]);
            m_buffer.Triangles.Add(m_buffer.Index[t.V2]);
            m_buffer.Triangles.Add(m_buffer.Index[t.V3]);
        }

        private void CorrectOrientation()
        {
            var triangles = new HashSet<Triangle>();
            foreach(int index in m_triangles.Keys)
            {
                var star = m_triangles[index];
                var correct = star.Find(t => triangles.Contains(t));

            }
        }

        protected void FlushMeshBuffer()
        {
            if (0 == m_triangles.Count)
                return;

            HashSet<Triangle> triangleSet = new HashSet<Triangle>();
            Queue<int> indexQueue = new Queue<int>();
            var idx = m_triangles.Keys.FirstOrDefault<int>();
            indexQueue.Enqueue(idx);
            Queue<Vector2> uvQueue = new Queue<Vector2>();
            // Start by zero origin
            uvQueue.Enqueue(Vector2.zero);

            // Compute initial vertex tangents
            var initialTriangle = m_triangles[idx].First<Triangle>();
            Vector3 ex, ey;
            ComputeUVs(initialTriangle, out ex, out ey, TextureScale);
            BufferVertex(idx, Normal(initialTriangle), Vector2.zero, new Vector4(ex.x, ex.y, ex.z, -1));

            while(indexQueue.Count > 0)
            {
                // Poll base index
                idx = indexQueue.Dequeue();
                var starOf = m_triangles[idx];
                // Poll base offset
                var uv0 = uvQueue.Dequeue();

                foreach(Triangle t in starOf)
                {
                    if (triangleSet.Contains(t))
                    {
                        continue;
                    }
                    triangleSet.Add(t);

                    // Compute UVs                    
                    ComputeUVs(t, out ex, out ey, TextureScale);

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
                    var n = Normal(t);

                    // Build UV coordinates using planar unit vectors and base offset                    
                    if (!m_buffer.Index.ContainsKey(v1))
                    {
                        var uv1 = new Vector2(Vector3.Dot(puv1, ex), Vector3.Dot(puv1, ey)) + uv0;
                        BufferVertex(v1, n, uv1, new Vector4(ex.x, ex.y, ex.z, -1));
                        indexQueue.Enqueue(v1);
                        uvQueue.Enqueue(uv1);
                    }
                    if (!m_buffer.Index.ContainsKey(v2))
                    {
                        var uv2 = new Vector2(Vector3.Dot(puv2, ex), Vector3.Dot(puv2, ey)) + uv0;
                        BufferVertex(v2, n, uv2, new Vector4(ex.x, ex.y, ex.z, -1));
                        indexQueue.Enqueue(v2);
                        uvQueue.Enqueue(uv2);
                    }

                    // Map triangle
                    BufferTriangle(t);

                    // If exceeding threshold
                    if(m_buffer.Triangles.Count / 3 > m_triangleThreshold)
                    {
                        // Don't use singleton, rather create new mesh filter children
                        m_singletonObject = null;
                        EmitGameObject();
                        m_buffer = m_buffer.Repeat(v1, v2);
                        TotalTriangleCount += m_triangles.Count / 3;
                    }
                }
            }

            // Clear all edges
            TotalTriangleCount += m_triangles.Count / 3;
            m_triangles.Clear();
        }

        private Vector3 Normal(Triangle t)
        {
            var normal = Vector3.Cross(m_xyz[t.V2] - m_xyz[t.V1], m_xyz[t.V3] - m_xyz[t.V1]);
            normal.Normalize();
            normal *= -1.0f;
            return normal;
        }

        private void ComputeUVs(Triangle t, out Vector3 ex, out Vector3 ey, float tiling)
        {
            // Normalized planar UV vectors
            var normal = Normal(t);
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

        private void StoreTriangle(Triangle t)
        {
            List<Triangle> starOf;
            if (!m_triangles.TryGetValue(t.V1, out starOf))
            {
                starOf = new List<Triangle>();
                m_triangles.Add(t.V1, starOf);
            }
            starOf.Add(t);

            if (!m_triangles.TryGetValue(t.V2, out starOf))
            {
                starOf = new List<Triangle>();
                m_triangles.Add(t.V2, starOf);
            }
            starOf.Add(t);

            if (!m_triangles.TryGetValue(t.V3, out starOf))
            {
                starOf = new List<Triangle>();
                m_triangles.Add(t.V3, starOf);
            }
            starOf.Add(t);
        }

    }
}
