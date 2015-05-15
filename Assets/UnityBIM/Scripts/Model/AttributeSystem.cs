using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace UnityBitub.Model
{

    /// <summary>
    /// Maps the ID to the associated building component.
    /// </summary>
    [System.Serializable]
    public class BuildingComponentSet : SerializableDictionary<string, BuildingComponent>
    {
    }

    /// <summary>
    /// Maps the attribute ID to a set of building components.
    /// </summary>
    [System.Serializable]
    public class AttributeComponentMap : SerializableDictionary<string, BuildingComponentSet>
    {
        public BuildingComponentSet GetOrCreateSet(string attributeID)
        {
            BuildingComponentSet componentSet;
            if (!TryGetValue(attributeID, out componentSet))
            {
                componentSet = new BuildingComponentSet();
                Add(attributeID, componentSet);
            }
            return componentSet;
        }
    }


    /// <summary>
    /// An attribute list.
    /// </summary>
    [System.Serializable]
    public class AttributeList : List<NamedAttribute>
    {
        public NamedAttribute findExact(string id)
        {
            return this.Find(x => x.ID.Equals(id));
        }

        public NamedAttribute findPrefix(string id)
        {
            return this.Find(x => x.ID.StartsWith(id));
        }
    }

    [System.Serializable]
    public class NamedAttribute
    {
        public string ID;
        public Attribute Value;
    }

    [System.Serializable]
    public class Attribute
    {
        public AttributeOperator Op = AttributeOperator.Equal;
        public string Serial = "";
        public System.Type DataType = typeof(string);
    }

    public enum AttributeOperator
    {
        Equal, LessThan, GreaterThan, LessThanEqual, GreaterThanEqual
    }
}