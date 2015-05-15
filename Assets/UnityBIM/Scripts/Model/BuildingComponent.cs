using UnityEngine;
using System.Collections;

namespace UnityBitub.Model
{
    /// <summary>
    /// Known component types.
    /// </summary>
    public enum ComponentType
    {
        Something, Wall, LayeredWall, Slab, LayeredSlab, Window, Door, Roof, @Space, Column, BuildingPart, Attribute,
        Foundation, Beam, Opening, Awanting, Facade, Stair, StairFlight
    }

    /// <summary>
    /// A building component type.
    /// </summary>
    public class BuildingComponent : MonoBehaviour
    {
        public string ID;
        public bool IsConstructive = true;
        public ComponentType ComponentType = ComponentType.Something;
        public MaterialID Material;

        [SerializeField]
        public AttributeList Attribute = new AttributeList();
    }

}