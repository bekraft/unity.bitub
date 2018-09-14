using UnityEngine;
using System.Collections;

using UnityBitub.Model;

namespace UnityBitub.CPI
{

    /// <summary>
    /// The component type of CPI component
    /// </summary>
    public enum CPIComponentType
    {
        Default, Alcove, Wall, Multi_Wall, Column, Door, Slab, Window, Opening, Part, Attribute, @Space, Foundation, Beam, Surface, Line
    }

    /// <summary>
    /// CPI building component object.
    /// </summary>
    public class CPIBuildingComponent : BuildingComponent
    {
        public CPIComponentType cpiComponentType = CPIComponentType.Attribute;
        public int cpiID = -1;

        public static ComponentType ComponentTypeSwitch(CPIComponentType c)
        {
            // Reparse by default
            ComponentType converted;
            try
            {
                converted = (ComponentType)System.Enum.Parse(typeof(ComponentType), System.Enum.GetName(typeof(CPIComponentType), c), true);
            }
            catch (System.Exception)
            {
                // Otherwise case based decisions
                switch (c)
                {
                    case CPIComponentType.Multi_Wall:
                        return ComponentType.LayeredWall;
                    case CPIComponentType.Attribute:
                        return ComponentType.Something;
                    case CPIComponentType.Part:
                        return ComponentType.BuildingPart;
                    default:
                        return ComponentType.Something;
                }
            }

            return converted;
        }
    }

}