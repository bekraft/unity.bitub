using UnityEngine;
using System.Collections;
using UnityBitub.CPI;

#if UNITY_EDITOR

using UnityEditor;

namespace UnityBitub.CPI.Editor {

    /// <summary>
    /// CPI preferences page in Unity editor.
    /// </summary>
    public class CPIEditorPreferences : MonoBehaviour 
    {
        static bool m_isLoad;

        const string ID_CASTSHADOWS = "CPIEnableShadowCast";
        const string ID_RECEIVESHADOWS = "CPIEnableReceiveShadow";
        const string ID_USELIGHTPROBS = "CPIUseLightProbs";
        const string ID_LOADALLPROPERTIES = "CPILoadAllProperties"; 
        const string ID_TRANSPARENCYOPENINGS = "CPITransparencyOpenings";

        [PreferenceItem("CPI import")]
        public static void GameObjectPreferences()
        {
            // Load preferences from Unity persistency
            if (!m_isLoad) {

                CPIPreferences.EnableCastShadows = EditorPrefs.GetBool(ID_CASTSHADOWS, true);
                CPIPreferences.EnableReceiveShadows = EditorPrefs.GetBool(ID_RECEIVESHADOWS, true);
                CPIPreferences.EnableUseLightProbs = EditorPrefs.GetBool(ID_USELIGHTPROBS, true);
                CPIPreferences.TransparencyOfOpenings = EditorPrefs.GetFloat(ID_TRANSPARENCYOPENINGS, 0.2f);
                m_isLoad = true;
            }

            CPIPreferences.EnableCastShadows = EditorGUILayout.Toggle(
                "Cast shadows", CPIPreferences.EnableCastShadows);
            CPIPreferences.EnableReceiveShadows = EditorGUILayout.Toggle(
                "Receive shadows", CPIPreferences.EnableReceiveShadows);
            CPIPreferences.EnableUseLightProbs = EditorGUILayout.Toggle(
                "Use light probs", CPIPreferences.EnableUseLightProbs);
            CPIPreferences.TransparencyOfOpenings = EditorGUILayout.Slider(
                "Transparency of openings", CPIPreferences.TransparencyOfOpenings, 0, 1);

            if (GUI.changed) {

                EditorPrefs.SetBool(ID_CASTSHADOWS, CPIPreferences.EnableCastShadows);
                EditorPrefs.SetBool(ID_RECEIVESHADOWS, CPIPreferences.EnableReceiveShadows);
                EditorPrefs.SetBool(ID_USELIGHTPROBS, CPIPreferences.EnableUseLightProbs);            
                EditorPrefs.SetFloat(ID_TRANSPARENCYOPENINGS, CPIPreferences.TransparencyOfOpenings);
            }
        } 

    }
}

#endif