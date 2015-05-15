using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.ComponentModel;

using UnityEngine;

using UnityBitub.Model;
using UnityBitub.Geometry;
using UnityBitub.CPI.Editor;

namespace UnityBitub.CPI
{
    /// <summary>
    /// CPI reader preferences.
    /// </summary>
    public class CPIPreferences
    {
        public static bool EnableCastShadows = true;
        public static bool EnableReceiveShadows = true;
        public static bool EnableUseLightProbs = true;

        public static float TransparencyOfOpenings = 0.2f;
    }

    /// <summary>
    /// The CPIXML model reader. Reads a CPI XML file into a Unity scene. Uses splitted streaming reader instances
    /// at the same time. Reads properties asynchronous to speed up import.
    /// </summary>
    sealed public class CPIXMLReader
    {
        #region Tag IDs

        public const string TAG_OBJECTS = "objects";

        public const string TAG_OBJECTSECTION = "objectSection";
        public const string TAG_OBJECTDATASECTION = "objectDataSection";


        public const string TAG_PROPERTYSECTION = "propertySection";
        public const string TAG_ROOTCONTAINER = "rootContainer";
        public const string TAG_MATERIALSECTION = "materialSection";

        public const string TAG_CONTAINER = "container";
        public const string TAG_DATA3D = "data3D";
        public const string TAG_OBJECT3D = "object3D";
        public const string TAG_MATERIAL = "material";
        public const string TAG_PROPERTY = "property";

        #endregion

        /// <summary>
        /// Whether the object section has been read.
        /// </summary>
        public bool IsReadObjectSection { private set; get; }

        /// <summary>
        /// Whether the material section has been read.
        /// </summary>
        public bool IsReadMaterialSection { private set; get; }

        /// <summary>
        /// Whether the property section has been read.
        /// </summary>
        public bool IsReadPropertySection { private set; get; }

        /// <summary>
        /// Whether the data section has been read.
        /// </summary>
        public bool IsReadDataSection { private set; get; }

        /// <summary>
        /// The name of the model.
        /// </summary>
        public string Name { private set; get; }

        /// <summary>
        /// The description of the model.
        /// </summary>
        public string Description { private set; get; }

        /// <summary>
        /// Total count of objects read.
        /// </summary>
        public long TotalObjects { private set; get; }

        /// <summary>
        /// The active model.
        /// </summary>
        public CPIBuildingComplex CPIComplex { private set; get; }

        #region Private members

        private XmlReader m_materialSectionReader;
        private XmlReader m_objectSectionReader;
        private XmlReader m_propertySectionReader;
        private XmlReader m_dataSectionReader;

        // Model and scene stack       
        private Stack<GameObject> m_sceneStack;

        // Cache maps 
        private Dictionary<string, MaterialID> m_materialCache;
        private Dictionary<string, List<NamedAttribute>> m_attributeCache;
        private Dictionary<string, CPIBuildingComponent> m_componentCache;
        private HashSet<string> m_attributeToRead;

        private string m_fileName;

        #endregion

        /// <summary>
        /// Initializes the reader.
        /// </summary>
        public CPIXMLReader(CPIBuildingComplex cpiModelComplex, string fileName)
        {
            m_sceneStack = new Stack<GameObject>();
            m_materialCache = new Dictionary<string, MaterialID>();
            m_attributeCache = new Dictionary<string, List<NamedAttribute>>();
            m_componentCache = new Dictionary<string, CPIBuildingComponent>();
            m_attributeToRead = new HashSet<string>();

            CPIComplex = cpiModelComplex;
            FileURI = fileName;
        }

        /// <summary>
        /// Indicates whether the whole file has been read.
        /// </summary>
        public bool IsComplete
        {
            get { return IsReadDataSection && IsReadObjectSection && IsReadPropertySection; } 
        }

        /// <summary>
        /// The file name of CPI XML file.
        /// </summary>
        public string FileURI
        {
            get
            {
                return m_fileName;
            }
            set
            {
                m_fileName = value;
                Initialize(value);
            }
        }

        /// <summary>
        /// Reads the geometry of model stepwise. Does not read properties.
        /// </summary>
        /// <returns></returns>
        public bool ReadGeometryStepwise()
        {
            bool isDone;

            // Read object structure
            if (!IsReadMaterialSection)
            {
                if (ReadMaterials(out isDone))
                {
                    IsReadMaterialSection = isDone;
                    return true;
                }
                else
                {
                    IsReadMaterialSection = true;
                    if(!isDone)
                        Debug.LogWarning("Unexpected end of file.");
                }
            }

            // Read object structure
            if (!IsReadObjectSection)
            {
                if (ReadObjects(out isDone))
                {
                    IsReadObjectSection = isDone;
                    return true;
                }
                else
                {
                    IsReadObjectSection = true;
                    if (!isDone)
                        Debug.LogWarning("Unexpected end of file.");
                }
            }

            // Read geometry data
            if (!IsReadDataSection)
            {
                if (ReadData3Ds(out isDone))
                {
                    IsReadDataSection = isDone;
                    return true;
                } 
                else
                {
                    IsReadDataSection = true;
                    if (!isDone)
                        Debug.LogWarning("Unexpected end of file.");
                }
            }

            m_dataSectionReader.Close();
            m_materialSectionReader.Close();
            m_objectSectionReader.Close();

            CPIComplex.MaterialList = m_materialCache.Values.ToArray<MaterialID>();

            return false;
        }

        public void ReadGeometry()
        {
            while (ReadGeometryStepwise()) ;
        }

        /// <summary>
        /// Reads the properties stepwise.
        /// </summary>
        /// <returns></returns>
        public bool ReadPropertiesStepwise()
        {
            bool isDone;

            // Read object structure
            if (!IsReadPropertySection)
            {
                if (ReadProperties(out isDone))
                {
                    IsReadPropertySection = isDone;
                    return true;
                }
                else
                {
                    IsReadPropertySection = true;
                    if(!isDone)
                        Debug.LogWarning("Unexpected end of file.");
                }
            }

            m_propertySectionReader.Close();

            FinalizesProperties();

            return false;
        }

        public void ReadProperties()
        {
            while (ReadPropertiesStepwise()) ;
        }

        public void ReadPropertiesAsync()
        {
            var bw = new BackgroundWorker();
            bw.DoWork += new DoWorkEventHandler(delegate(object o, DoWorkEventArgs args)
            {
                while (ReadPropertiesStepwise()) ;                
            });

            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(delegate(object o, RunWorkerCompletedEventArgs args)
            {                
                Debug.Log("Read property section of \"" + Name + "\".");
                FinalizesProperties();
            });

            bw.RunWorkerAsync();
        }

        private void FinalizesProperties()
        {
            Debug.Log("Indexing attribute cache.");
            foreach (string key in m_attributeCache.Keys)
            {
                var component = CreateCachedGameObject(key);
                var attributeList = m_attributeCache[key];

                foreach (NamedAttribute a in attributeList)
                {
                    CPIComplex.AddAttribute(component, a);
                }
            }
        }

        #region XML reader stack

        private delegate void AcceptXmlReaderDelegate(XmlReader reader);

        private void runAsyncPositioning(string fileName, string targetTag, AcceptXmlReaderDelegate accept)
        {
            var backgroundReaderThread = new BackgroundWorker();

            backgroundReaderThread.DoWork += new DoWorkEventHandler(delegate(object o, DoWorkEventArgs args)
            {
                var reader = args.Argument as XmlReader;
                reader.ReadToDescendant(targetTag);
                args.Result = reader;
            });

            backgroundReaderThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(delegate(object o, RunWorkerCompletedEventArgs args)
            {
                accept( args.Result as XmlReader );
                Debug.Log("Element \"" + targetTag + "\" has been spotted.");
            });

            backgroundReaderThread.RunWorkerAsync(XmlReader.Create(fileName));            
        }

        private void Initialize(string fileName)
        {
            runAsyncPositioning(fileName, TAG_MATERIALSECTION, (r) => m_materialSectionReader = r);
            runAsyncPositioning(fileName, TAG_ROOTCONTAINER, (r) => m_objectSectionReader = r);
            runAsyncPositioning(fileName, TAG_OBJECTDATASECTION, (r) => m_dataSectionReader = r);
            runAsyncPositioning(fileName, TAG_PROPERTYSECTION, (r) => m_propertySectionReader = r);
            
            var initReader = XmlReader.Create(fileName);
            ReadTo(initReader, TAG_OBJECTS);
            initReader.Close();

            Debug.Log("Reading model \"" + Name + "\" (" + Description + ")");

            m_attributeCache.Clear();
            m_sceneStack.Clear();
            m_materialCache.Clear();
            m_attributeToRead.Clear();
            m_componentCache.Clear();

            foreach (string a in CPIComplex.AttributeList)
            {
                m_attributeToRead.Add(a);
            }

            TotalObjects = 0;
        }

        /// <summary>
        /// Reads a single material element.
        /// </summary>
        /// <param name="isDone">If properly terminated reading.</param>
        /// <returns>If reached end of file.</returns>
        private bool ReadMaterials(out bool isDone)
        {
            if (null == m_materialSectionReader)
            {
                isDone = false;
                return true;
            }

            return ReadNext(m_materialSectionReader, out isDone, null, TAG_MATERIALSECTION);
        }

        /// <summary>
        /// Reads a single object element.
        /// </summary>
        /// <param name="isDone">If properly terminated reading.</param>
        /// <returns>If reached end of file.</returns>
        private bool ReadObjects(out bool isDone)
        {
            if (null == m_objectSectionReader)
            {
                isDone = false;
                return true;
            }

            return ReadNext(m_objectSectionReader, out isDone, null, TAG_ROOTCONTAINER);
        }

        /// <summary>
        /// Reads a single geometry object.
        /// </summary>
        /// <param name="isDone">If properly terminated reading.</param>
        /// <returns>If reached end of file.</returns>
        private bool ReadData3Ds(out bool isDone)
        {
            if (null == m_dataSectionReader) {

                isDone = false;
                return true;
            }

            return ReadNext(m_dataSectionReader, out isDone, null, TAG_OBJECTDATASECTION);
        }

        /// <summary>
        /// Reads a single property object.
        /// </summary>
        /// <param name="isDone">If properly terminated reading.</param>
        /// <returns>If reached end of file.</returns>
        private bool ReadProperties(out bool isDone)
        {
            if(null==m_propertySectionReader)
            {
                isDone = false;
                return true;
            }

            return ReadNext(m_propertySectionReader, out isDone, null, TAG_PROPERTYSECTION);
        }


        /// <summary>
        /// Reads the next object from stream.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="isDone">If start or end tag has been hit.</param>
        /// <param name="startTag">A start tag to hit</param>
        /// <param name="endTag">An end tag to hit</param>
        /// <returns>True, if not reached end</returns>
        private bool ReadNext(XmlReader reader, out bool isDone, string startTag = null, string endTag = null)
        {
            isDone = false;
            
            switch (reader.NodeType) {
                case XmlNodeType.Element:

                    isDone = null != startTag && reader.Name.Equals(startTag);
                    HandleStartElement(reader);
                    break;
                case XmlNodeType.EndElement:

                    isDone = null != endTag && reader.Name.Equals(endTag);
                    HandleEndElement(reader);
                    break;
            }

            return reader.Read() && !reader.EOF;
        }


        private bool ReadTo(XmlReader reader, string startTag = null)
        {
            bool isDone = false;
            while (ReadNext(reader, out isDone, startTag)) {

                // load loop
                if (isDone)
                    break;
            }
            return isDone || (startTag == null && reader.EOF);
        }

        private void HandleStartElement(XmlReader reader)
        {
            switch (reader.Name) {

                case TAG_OBJECTS:
                    ReadMetainfo(reader);
                    break;
                case TAG_ROOTCONTAINER:
                    m_sceneStack.Push(GenerateGameObject(reader).gameObject);
                    break;
                case TAG_CONTAINER:
                    m_sceneStack.Push(GenerateGameObject(reader).gameObject);
                    break;
                case TAG_OBJECT3D:
                    TotalObjects++;
                    GenerateObject3D(reader);
                    break;
                case TAG_DATA3D:
                    ReadData3D(reader);
                    break;
                case TAG_PROPERTY:
                    ReadProperty(reader);
                    break;
                case TAG_MATERIAL:
                    ReadMaterial(reader);
                    break;
            }
        }

        private void HandleEndElement(XmlReader reader)
        {
            switch (reader.Name) {
                case TAG_ROOTCONTAINER:
                    m_sceneStack.Pop();
                    break;
                case TAG_CONTAINER:
                    m_sceneStack.Pop();
                    break;
            }
        }

        private void ReadMetainfo(XmlReader reader)
        {
            Name = reader.GetAttribute("projectID");
            Description = reader.GetAttribute("sourceFileName");
        }

        #endregion

        #region Game object creation

        private CPIBuildingComponent CreateCachedGameObject(string id, string name = "")
        {
            CPIBuildingComponent cpiObject;
            if (!m_componentCache.TryGetValue(id, out cpiObject))
            {
                cpiObject =
                    CPIComplex.GetOrCreateComponent<CPIBuildingComponent>(id.ToString(), CPIComplex.componentTemplate) as CPIBuildingComponent;
                m_componentCache.Add(id, cpiObject);

                cpiObject.name = name;
                int.TryParse(id, out cpiObject.cpiID);
            }

            return cpiObject;
        }

        private CPIBuildingComponent GenerateGameObject(XmlReader reader)
        {
            string id = reader.GetAttribute("ID").Trim();
            string name = reader.GetAttribute("name");

            CPIBuildingComponent cpiObject = CreateCachedGameObject(id, name);

            Transform parent = null;

            if (reader.Name.Equals(TAG_CONTAINER)) {

                parent = m_sceneStack.Peek().transform;
            }
            else if (reader.Name.Equals(TAG_ROOTCONTAINER)) {

                parent = CPIComplex.gameObject.transform;
            }
            else if (reader.Name.Equals(TAG_OBJECT3D)) {

                // Find parent object
                string refIdStr = reader.GetAttribute("refID");
                if (null != refIdStr && refIdStr.Trim().Length > 0) {

                    // Parent is another object
                    CPIBuildingComponent parentCpiObject = CreateCachedGameObject(refIdStr);
                    parent = parentCpiObject.gameObject.transform;
                }
                else {

                    // Parent is container
                    parent = m_sceneStack.Peek().transform;
                }
            }

            // Set parent
            if (null != parent) {

                cpiObject.gameObject.transform.rotation = parent.rotation;
                cpiObject.gameObject.transform.position = parent.position;

                cpiObject.gameObject.transform.parent = parent;
            }

            return cpiObject;
        }


        /// <summary>
        /// Creates the object3D game object.
        /// </summary>
        /// <returns>The object3D game object.</returns>
        private CPIBuildingComponent GenerateObject3D(XmlReader reader)
        {
            var component = GenerateGameObject(reader);            
            MaterialID material;

            if(m_materialCache.TryGetValue(reader.GetAttribute("matID"), out material))
            {
                component.Material = material;
            }
            
            return component;
        }

        #endregion

        #region Geometry parsing

        /// <summary>
        /// Reads the data3d tag.
        /// </summary>
        private void ReadData3D(XmlReader reader)
        {
            // Find object3D
            string strRefID = reader.GetAttribute("refID");
            CPIBuildingComponent component = CreateCachedGameObject(strRefID);

			// 65000 predefined total number of triangles allowed
            TriangleMeshBuilder meshBuilder = new TriangleMeshBuilder(65000, CPIComplex.componentTemplate);
            meshBuilder.StartMeshing(component.name, delegate(GameObject o)
            {
                o.name = "Mesh of " + component.name;

                // Set transformation            
                o.transform.position = component.gameObject.transform.position;
                o.transform.rotation = component.gameObject.transform.rotation;

                o.transform.parent = component.gameObject.transform;

                o.GetComponent<MeshFilter>().name = o.name;

                // Set material
                var material = component.Material.Material;
                var meshRenderer = o.GetComponent<MeshRenderer>();
                if (null == meshRenderer) {
                    meshRenderer = o.AddComponent<MeshRenderer>();
                }
                meshRenderer.material = material;

                meshRenderer.useLightProbes = CPIPreferences.EnableUseLightProbs;
				meshRenderer.shadowCastingMode = (CPIPreferences.EnableCastShadows?
				           UnityEngine.Rendering.ShadowCastingMode.On: UnityEngine.Rendering.ShadowCastingMode.Off);
                meshRenderer.receiveShadows = CPIPreferences.EnableReceiveShadows;
            });

            // Reader loop
            while (reader.Read() && !reader.EOF) {

                if (reader.NodeType == XmlNodeType.Element) {

                    switch (reader.Name) {
                        case "p":
                            // A single point
                            var p = ReadPoint(reader);
                            meshBuilder.AppendPoint(p);
                            break;

                        case "face":
                            // Starts reading face
                            meshBuilder.StartMeshFace();
                            break;

                        case "t":
                            // Read a single triangle
                            var triangle = ReadTriangle(reader);
                            meshBuilder.AppendTriangle(triangle[0], triangle[1], triangle[2]);
                            break;
                    }
                }

                if (reader.NodeType == XmlNodeType.EndElement) {

                    switch (reader.Name) {

                        case "face":

                            // Stop meshing face
                            meshBuilder.EndMeshFace();
                            break;

                        case TAG_DATA3D:

                            meshBuilder.EndMeshing();
                            Debug.Log(string.Format("Read {0} triangles in total.", meshBuilder.TotalTriangleCount));

                            // return
                            return;
                    }
                }
            }
        }


        /// <summary>
        /// Focuses the "p" element. Reads a single  point.
        /// </summary>
        /// <param name="expectedNr">The expected index of point</param>
        private Vector3 ReadPoint(XmlReader reader)
        {
            float x, y, z;
            float.TryParse(reader.GetAttribute("x"), out x);
            float.TryParse(reader.GetAttribute("y"), out y);
            float.TryParse(reader.GetAttribute("z"), out z);
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Focuses the "t" element. Reads a single triangle.
        /// </summary>
        /// <param name="triangles">Triangle indices list</param>
        private int[] ReadTriangle(XmlReader reader)
        {
            int p1, p2, p3;
            int.TryParse(reader.GetAttribute("p1"), out p1);
            int.TryParse(reader.GetAttribute("p2"), out p2);
            int.TryParse(reader.GetAttribute("p3"), out p3);
            return new int[] { p1, p2, p3 };
        }


        #endregion

        #region Material parsing

        private void ReadMaterialColor(XmlReader reader, Material material)
        {
            bool succeeded = true;
            float r, g, b;
            succeeded &= float.TryParse(reader.GetAttribute("r"), out r);
            succeeded &= float.TryParse(reader.GetAttribute("g"), out g);
            succeeded &= float.TryParse(reader.GetAttribute("b"), out b);

            material.color = new Color(r / 255.0f, g / 255.0f, b / 255.0f, 1.0f);

            if (!succeeded)
                Debug.LogWarning("Some color not read properly.");
        }

        private void ReadMaterialTransparency(XmlReader reader, Material material)
        {
            float v;
            if (float.TryParse(reader.GetAttribute("v"), out v))
            {
                Color diffuse = material.color;
                diffuse.a = 1.0f - v;
                material.color = diffuse;
            }
        }

        private void ReadMaterial(XmlReader reader)
        {
            MaterialID material = CreateCachedMaterialID(reader.GetAttribute("ID"));
            material.ID = reader.GetAttribute("name");

            while (reader.Read() && !reader.EOF) {

                if (reader.IsStartElement()) {

                    switch (reader.Name) {

                        case "diff":
                            ReadMaterialColor(reader, material.Material);
                            break;
                        case "trans":
                            ReadMaterialTransparency(reader, material.Material);
                            break;
                    }

                }

                if (reader.NodeType == XmlNodeType.EndElement && reader.Name.Equals(TAG_MATERIAL)) {
                    break;
                }
            }
        }


        private MaterialID CreateCachedMaterialID(string matID)
        {
            MaterialID material;
            if(!m_materialCache.TryGetValue(matID, out material))
            {
                material = new MaterialID{ ID=matID, Material = new Material(CPIComplex.ShaderOnNewMaterial) };
                m_materialCache.Add(matID, material);
            }

            return material;
        }

        #endregion

        #region Property parsing

        private CPIComponentType ReadParseComponentType(string value)
        {
            CPIComponentType type;
            if (null == value || 0 == value.Length)
            {
                type = CPIComponentType.Attribute;
            }
            else
            {
                try
                {
                    type = (CPIComponentType)Enum.Parse(typeof(CPIComponentType), value);
                }
                catch (Exception)
                {
                    type = CPIComponentType.Attribute;
                    Debug.LogWarning(string.Format("Unknown component type {0}.", value));
                }
            }

            return type;
        }

        private void ReadProperty(XmlReader reader)
        {
            string propertyName = reader.GetAttribute("name").Trim();
            if (m_attributeToRead.Contains(propertyName) && !propertyName.Equals("cpiComponentType")) {
                // Skip if only material properties
                return;
            }

            string refID = reader.GetAttribute("refID").Trim();
            CPIBuildingComponent component = CreateCachedGameObject(refID);
            string content = reader.ReadString().Trim();

            // Recognize and map component type
            if ("cpiComponentType".Equals(propertyName)) {

                component.cpiComponentType = ReadParseComponentType(content);
                component.ComponentType = CPIBuildingComponent.ComponentTypeSwitch(component.cpiComponentType);
            }

            List<NamedAttribute> attributeList;
            if(!m_attributeCache.TryGetValue(refID, out attributeList))
            {
                attributeList = new List<NamedAttribute>();
                m_attributeCache.Add(refID, attributeList);
            }

            attributeList.Add(new NamedAttribute { ID = propertyName, Value = new UnityBitub.Model.Attribute { Serial = content } });
        }

        #endregion
    }

}