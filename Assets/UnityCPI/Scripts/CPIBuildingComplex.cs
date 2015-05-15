using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using UnityBitub.Model;

namespace UnityBitub.CPI
{

    /// <summary>
    /// CPI model root script. Offers reading and manipulation of whole model. Should be applied
    /// to a root scene object which should carry the CPI model.
    /// </summary>
    public class CPIBuildingComplex : BuildingComplex
    {
        public const string TAG = "UnityBitub.CPI.CPIModel";

        public GameObject componentTemplate;

        void Awake()
        {
            if (null == componentTemplate) {

                Debug.LogError("Missing template object for CPI model.");
            }
        }
    }

}