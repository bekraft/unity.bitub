using UnityEngine;
using System.Collections;
using UnityBitub.Model;

#if UNITY_EDITOR

using UnityEditor;

namespace UnityBitub.CPI.Editor
{
    sealed internal class Loader
    {
        static internal CPIBuildingComplex FindRootComponent()
        {
            var modelRoot = GameObject.FindGameObjectWithTag(CPIBuildingComplex.TAG);
            if (null == modelRoot)
            {
                Debug.LogError("CPI model root game object not found. Use \"" + CPIBuildingComplex.TAG + "\" tag.");
                return null;
            }
            else
            {
                var complexTemplate = modelRoot.GetComponent<CPIBuildingComplex>();
                if (null == complexTemplate)
                {
                    Debug.LogWarning("CPI model script missing. Adding new one.");
                    complexTemplate = modelRoot.AddComponent<CPIBuildingComplex>();
                }
                return complexTemplate;
            }
        }

        static internal void runImport(CPIBuildingComplex complexTemplate, string fileName)
        {
            var modelReader = new CPIXMLReader(complexTemplate, fileName);
            Debug.Log("Start reading model of " + modelReader.Name);
            modelReader.ReadModel();
            Debug.Log(string.Format("Read {0} objects in total.", modelReader.TotalObjects));
        }
    }

    /// <summary>
    /// Unity Editor Hook into main menu.
    /// </summary>
    sealed public class CPIEditorItems
    {
        [MenuItem("CPI/Import CPIXML model")]
        static void Import()
        {
            var complexTemplate = Loader.FindRootComponent();       
            string path = EditorUtility.OpenFilePanel("RIB CPI Model Import", "%USERPROFILE%", "cpixml");
            
            if (0 < path.Length)
            {
                Debug.Log("Start importing file \"" + path + "\".");
                Loader.runImport(complexTemplate, path);
            }
        }

        [MenuItem("CPI/Hide or show openings")]
        static void HideShowOpenings()
        {
            var complexTemplate = Loader.FindRootComponent();
            complexTemplate.AcceptVisitor((BuildingComponent component, bool hasChildren) => 
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
            var complexTemplate = Loader.FindRootComponent();
            complexTemplate.AcceptVisitor((BuildingComponent component, bool hasChildren) => 
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
            var complexTemplate = Loader.FindRootComponent();
            complexTemplate.AcceptVisitor((BuildingComponent component, bool hasChildren) =>
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