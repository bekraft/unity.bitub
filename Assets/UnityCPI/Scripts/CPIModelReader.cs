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

        private XmlReader m_reader;

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
                this.m_fileName = value;
                TouchModel(value);
            }
        }

        public void ReadModel()
        {
            bool isComplete;
            while(ReadNext(m_reader, out isComplete, null, TAG_OBJECTS))
            {
                if (isComplete)
                    Debug.Log("Model \"" + Name + "\" read completely.");
            }

            Debug.Log("Finalizing model ...");
            FinalizeData3D();
            FinalizeProperties();
        }

        private void FinalizeData3D()
        {
            // Overwrite material list
            CPIComplex.MaterialList = m_materialCache.Values.ToArray<MaterialID>();
            // Update material settings
            CPIComplex.AcceptVisitor((CPIBuildingComponent component, bool hasChildren) =>
            {
                var meshRenderer = component.GetComponent<MeshRenderer>();
                if(null!=component.Material && null!=meshRenderer)
                {
                    meshRenderer.material = component.Material.Material;
                }
                return true;
            });
        }

        private void FinalizeProperties()
        {
            // Post process attributes
            Debug.Log("Transfering attribute cache ...");
            foreach (string key in m_attributeCache.Keys)
            {
                var component = CreateOrGetComponent(key);
                var attributeList = m_attributeCache[key];

                foreach (NamedAttribute a in attributeList)
                {
                    component.Attribute.Add(a);
                }
            }

            var nonConstructives = new ComponentType[] { ComponentType.Opening, ComponentType.Door, ComponentType.Window };
            // Post process openings and isConstructive
            Dictionary<string, Material> namedMaterials = new Dictionary<string,Material>();
            CPIComplex.AcceptVisitor((CPIBuildingComponent component, bool hasChildren) =>
            {
                if (System.Array.Exists<ComponentType>(nonConstructives, c => c == component.ComponentType))
                {
                    // Set non-constructive
                    component.IsConstructive = false;
                    component.gameObject.tag = BuildingComponent.TAG_ISNONCONSTRUCTIVE;

                    var childComponents = component.gameObject.GetComponentsInChildren<CPIBuildingComponent>();
                    foreach(CPIBuildingComponent childComponent in childComponents)
                    {
                        childComponent.IsConstructive = false;
                        childComponent.gameObject.tag = BuildingComponent.TAG_ISNONCONSTRUCTIVE;
                        childComponent.cpiComponentType = component.cpiComponentType;
                        childComponent.ComponentType = component.ComponentType;
                    }

                    // Clone matertials => use dictionary => adapt transparency
                    var meshRenderers = component.gameObject.GetComponentsInChildren<MeshRenderer>();
                    foreach(MeshRenderer mr in meshRenderers) 
                    {
                        var name = mr.sharedMaterial.name;
                        Material newMaterial;
                        if(!namedMaterials.ContainsKey(name))
                        {
                            newMaterial = new Material(CPIComplex.ShaderTransparent);
                            var oldColor = mr.sharedMaterial.color;
                            var newColor = new Color(oldColor.r, oldColor.g, oldColor.b);

                            newColor.a = 1.0f - CPIPreferences.TransparencyOfOpenings;
                            newMaterial.color = newColor;

                            newMaterial.name = "Transparent " + name;
                            namedMaterials.Add(name, newMaterial);
                        }
                        else
                        {
                            newMaterial = namedMaterials[name];
                        }

                        mr.sharedMaterial = newMaterial;

                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                    }
                    // Don't investigate deeper (since its done by this handler)
                    return false;
                }

                // Otherwise is constructive
                component.IsConstructive = true;
                component.gameObject.tag = BuildingComponent.TAG_ISCONSTRUCTIVE;

                return true;
            });
            
        }

        #region XML reader stack

        private void TouchModel(string fileName)
        {
            Debug.Log("Start reading \"" + fileName + "\".");

            this.m_reader = XmlReader.Create(fileName);
            ReadTo(m_reader, TAG_OBJECTS);
            
            Debug.Log("Reading model \"" + Name + "\" (" + Description + ")");

            m_attributeCache.Clear();
            m_sceneStack.Clear();
            m_materialCache.Clear();
            m_attributeToRead.Clear();
            m_componentCache.Clear();

            CPIComplex.Name = Name;
            CPIComplex.Description = Description;

            foreach (string a in CPIComplex.AttributeList)
            {
                m_attributeToRead.Add(a);
            }

            TotalObjects = 0;
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
                    IsReadObjectSection = true;
                    var component1 = ReadComponent(reader);
                    m_sceneStack.Push(component1.gameObject);
                    component1.ComponentType = ComponentType.Container;
                    component1.IsConstructive = false;
                    break;
                case TAG_CONTAINER:
                    var component2 = ReadComponent(reader);
                    m_sceneStack.Push(component2.gameObject);
                    component2.ComponentType = ComponentType.Container;
                    component2.IsConstructive = false;
                    break;
                case TAG_OBJECT3D:
                    TotalObjects++;
                    ReadObject3D(reader);
                    break;
                case "objectDataSection":
                    IsReadDataSection = true;
                    break;
                case TAG_DATA3D:
                    ReadData3D(reader);
                    break;
                case "propertySection":
                    IsReadPropertySection = true;
                    break;
                case TAG_PROPERTY:
                    ReadProperty(reader);
                    break;
                case "materialSection":
                    IsReadMaterialSection = true;
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

        private CPIBuildingComponent CreateOrGetComponent(string id, string name = "")
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

        private CPIBuildingComponent ReadComponent(XmlReader reader)
        {
            string id = reader.GetAttribute("ID").Trim();
            string name = reader.GetAttribute("name");

            CPIBuildingComponent cpiObject = CreateOrGetComponent(id, name);

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
                    CPIBuildingComponent parentCpiObject = CreateOrGetComponent(refIdStr);
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
        /// Creates the object3D game object including its material reference.
        /// </summary>
        /// <returns>The object3D game object.</returns>
        private CPIBuildingComponent ReadObject3D(XmlReader reader)
        {
            var component = ReadComponent(reader);
            MaterialID material = CreateOrGetMaterial(reader.GetAttribute("matID"));
            component.Material = material;
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
            CPIBuildingComponent component = CreateOrGetComponent(strRefID);

			// 65000 predefined total number of triangles allowed
            TriangleMeshBuilder meshBuilder = new TriangleMeshBuilder(65000, CPIComplex.componentTemplate);
            meshBuilder.StartMeshing(component.name, component.gameObject, delegate(GameObject o)
            {
                // Set transformation
                if (o != component.gameObject)
                {
                    o.name = "Mesh of " + component.name;
                    o.transform.position = component.gameObject.transform.position;
                    o.transform.rotation = component.gameObject.transform.rotation;

                    o.transform.parent = component.gameObject.transform;
                }

                o.GetComponent<MeshFilter>().name = component.name;

                // Set material
                var material = component.Material.Material;
                var meshRenderer = o.GetComponent<MeshRenderer>();
                if (null == meshRenderer) {
                    meshRenderer = o.AddComponent<MeshRenderer>();
                }
                meshRenderer.material = material;

                if (CPIPreferences.EnableUseLightProbs)
                    meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
                else
                    meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

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
                            // return
                            return;
                    }
                }
            }
        }

        /// <summary>
        /// Focuses the "p" element. Reads a single  point.
        /// </summary>
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
            MaterialID material = CreateOrGetMaterial(reader.GetAttribute("ID"));
            material.ID = reader.GetAttribute("name");
            material.Material.name = material.ID;

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

        private MaterialID CreateOrGetMaterial(string matID)
        {
            MaterialID material;
            if(!m_materialCache.TryGetValue(matID, out material))
            {
                material = new MaterialID{ ID=matID, Material = new Material(CPIComplex.ShaderOpaque) };
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
            if (!m_attributeToRead.Contains(propertyName) && !propertyName.Equals("cpiComponentType")) {
                // Skip if only material properties
                return;
            }

            string refID = reader.GetAttribute("refID").Trim();
            CPIBuildingComponent component = CreateOrGetComponent(refID);
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