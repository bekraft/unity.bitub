using UnityEngine;
using UnityEngine.Serialization;

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityBitub.Model
{

    [System.Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> m_keys = new List<TKey>();

        [SerializeField]
        private List<TValue> m_values = new List<TValue>();

        // save the dictionary to lists
        public void OnBeforeSerialize()
        {
            m_keys.Clear();
            m_values.Clear();

            if (typeof(TKey).IsSubclassOf(typeof(UnityEngine.Object)) || typeof(TKey) == typeof(UnityEngine.Object))
            {
                foreach (var element in this.Where(element => element.Key != null))
                {
                    m_keys.Add(element.Key);
                    m_values.Add(element.Value);
                }
            }
            else            
            {
                foreach (KeyValuePair<TKey, TValue> pair in this)
                {
                    m_keys.Add(pair.Key);
                    m_values.Add(pair.Value);
                }
            }
        }

        public void OnAfterDeserialize()
        {
            base.Clear();

            if (m_keys.Count != m_values.Count)
                throw new System.Exception(string.Format("there are {0} keys and {1} values after deserialization. Make sure that both key and value types are serializable."));

            for (int i = 0; i < m_keys.Count; i++)
                this.Add(m_keys[i], m_values[i]);
        }

        new public void Clear()
        {
            base.Clear();
            m_keys.Clear();
            m_values.Clear();
        }
    }


    
}