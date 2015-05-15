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
            TaggedModel.Clean();
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
            editorItems.TaggedModel.AcceptVisitor(delegate(BuildingComponent component, bool hasChildren) {

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
            editorItems.TaggedModel.AcceptVisitor(delegate(BuildingComponent component, bool hasChildren) {

                if (component.ComponentType == ComponentType.Space) {

                    component.gameObject.SetActive(!component.gameObject.activeSelf);
                }
                return true;
            });
        }

        [MenuItem("CPI/Generate rigid bodies")]
        static void ComputeColliders()
        {
            var editorItems = new CPIEditorItems();
            var invalidTypes = new ComponentType[] { 
                ComponentType.Door, 
                ComponentType.Opening, 
                ComponentType.Window };

            editorItems.TaggedModel.AcceptVisitor(delegate(BuildingComponent component, bool hasChildren)
            {

                if (System.Array.Exists<ComponentType>(invalidTypes, e => e == component.ComponentType))
                {
                    return false;
                }

                var meshColliders = component.transform.GetComponentsInChildren<MeshCollider>();
                foreach (MeshCollider mc in meshColliders)
                {
                    var rigidBody = mc.gameObject.GetComponent<Rigidbody>();
                    if (null == rigidBody)
                        rigidBody = mc.gameObject.AddComponent<Rigidbody>();

                    rigidBody.useGravity = false;
                    rigidBody.isKinematic = true;
                }

                return true;
            });
        }
    }

}

#endif