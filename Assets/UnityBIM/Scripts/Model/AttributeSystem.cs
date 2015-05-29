using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace UnityBitub.Model
{
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