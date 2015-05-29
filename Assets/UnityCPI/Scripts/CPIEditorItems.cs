using UnityEngine;
using System.Collections;
using UnityBitub.Model;

#if UNITY_EDITOR

using UnityEditor;

namespace UnityBitub.CPI.Editor
{

    /// <summary>
    /// Unity Editor Hook into main menu.
    /// </summary>
    sealed public class CPIEditorItems
    {
        public CPIBuildingComplex TaggedModel { private set; get; }

        /// <summary>
        /// Initializes. Finds the CPI building complex root by its tag.
        /// </summary>
        public CPIEditorItems()
        {
            var modelRoot = GameObject.FindGameObjectWithTag(CPIBuildingComplex.TAG);
            if (null == modelRoot) {
                Debug.LogError("CPI model root game object not found. Use \"" + CPIBuildingComplex.TAG + "\" tag.");
            }
            else {
                TaggedModel = modelRoot.GetComponent<CPIBuildingComplex>();
                if (null == TaggedModel) {
                    Debug.LogWarning("CPI model script missing. Adding new one.");
                    TaggedModel = modelRoot.AddComponent<CPIBuildingComplex>();
                }
            }
        }

        private void Import(string fileName)
        {
            var modelReader = new CPIXMLReader(TaggedModel, fileName);

            Debug.Log("Start reading geometry of " + modelReader.Name);
            modelReader.ReadGeometry();
            Debug.Log(string.Format("Read {0} objects in total.", modelReader.TotalObjects));

            Debug.Log("Start reading properties of " + modelReader.Name);
            modelReader.ReadProperties();
            Debug.Log("Reader terminated normally.");
        }


        [MenuItem("CPI/Import CPIXML model")]
        static void Import()
        {
            string path = EditorUtility.OpenFilePanel("RIB CPI Model Import", "%USERPROFILE%", "cpixml");
            Debug.Log("Start importing file \"" + path + "\".");

            var importHelper = new CPIEditorItems();
            importHelper.Import(path);
        }

        [MenuItem("CPI/Hide or show openings")]
        static void HideShowOpenings()
        {
            var editorItems = new CPIEditorItems();
            editorItems.TaggedModel.AcceptVisitor((BuildingComponent component, bool hasChildren) => 
            {
                if (component.ComponentType == ComponentType.Opening) {

                    component.gameObject.SetActive(!component.gameObject.activeSelf);
                }
                return true;
            });
        }

        [MenuItem("CPI/Hide or show spaces")]
        static void HideShowSpaces()
        {
            var editorItems = new CPIEditorItems();
            editorItems.TaggedModel.AcceptVisitor((BuildingComponent component, bool hasChildren) => 
            {
                if (component.ComponentType == ComponentType.Space) {

                    component.gameObject.SetActive(!component.gameObject.activeSelf);
                }
                return true;
            });
        }

        [MenuItem("CPI/Generate physics")]
        static void ComputeColliders()
        {
            var editorItems = new CPIEditorItems();

            editorItems.TaggedModel.AcceptVisitor((BuildingComponent component, bool hasChildren) =>
            {
                if (!component.IsConstructive)
                {
                    return true;
                }

                // Search direct children for mesh filters                
                foreach(Transform t in component.gameObject.transform)
                {
                    var mf = t.gameObject.GetComponent<MeshFilter>();
                    if(null!=mf)
                    {
                        var meshCollider = t.gameObject.GetComponent<MeshCollider>();
                        if (null == meshCollider)
                            meshCollider = t.gameObject.AddComponent<MeshCollider>();

                        meshCollider.sharedMesh = mf.sharedMesh;
                        meshCollider.name = "Collider of " + t.gameObject.name;

                        var rigidBody = t.gameObject.GetComponent<Rigidbody>();
                        if (null == rigidBody)
                            rigidBody = t.gameObject.AddComponent<Rigidbody>();

                        rigidBody.useGravity = false;
                        rigidBody.isKinematic = true;
                    }
                }

                return true;
            });
        }
    }

}

#endif