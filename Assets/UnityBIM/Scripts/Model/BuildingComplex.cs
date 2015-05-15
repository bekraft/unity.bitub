using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace UnityBitub.Model
{
    [System.Serializable]
    public class MaterialID
    {
        public string ID;
        public Material Material;
    }

    /// <summary>
    /// A prototype of a building complex behaviour.
    /// </summary>
    public class BuildingComplex : MonoBehaviour 
    {
        public string Name;
        public string Description;

        public string IdentificationAttributeID = "ID";

        public Shader ShaderOnNewMaterial = Shader.Find("Transparent/Diffuse");

        public MaterialID[] MaterialList;

        public string[] AttributeList = new string[] { "MaterialName" };

        [SerializeField]
        private AttributeComponentMap m_attributeComponentMap = new AttributeComponentMap();

        #region Non-unity behaviour

        /// <summary>
        /// A generic delegate for graph travel handling.
        /// </summary>
        /// <param name="component">The object.</param>
        /// <param name="hasChildren">True, if there are children (not neccessarily CpiObject3D instances)</param>
        /// <returns>Whether to search subtree (children).</returns>
        public delegate bool VisitComponent<T>(T component, bool hasChildren) where T : BuildingComponent;

        /// <summary>
        /// Runs a breath-first search on scene graph and call delegate whenever a CpiObject3D is hit.
        /// </summary>
        /// <param name="visitor">The delegate.</param>
        public void AcceptVisitor<T>(VisitComponent<T> visitor) where T : BuildingComponent
        {
            if (null != visitor)
            {

                var queue = InitSearch();

                while (queue.Count > 0)
                {

                    bool hasChildren;
                    var gameObject = Search(queue, out hasChildren);
                    var component = gameObject.GetComponent<T>();

                    if (null != component)
                    {
                        // If component -> notify visitor
                        if (visitor(component, hasChildren))
                        {
                            Investigate(queue, gameObject);
                        }
                    }
                    else
                    {
                        // If no component, search until components are found
                        Investigate(queue, gameObject);
                    }
                }
            }
        }

        #region Breath first search algorithm

        /// <summary>
        /// Initializes the search.
        /// </summary>
        /// <returns></returns>
        private Queue<Transform> InitSearch()
        {
            var bfsQueue = new Queue<Transform>();
            bfsQueue.Enqueue(transform);
            return bfsQueue;
        }

        /// <summary>
        /// Polls next game object out of the queue.
        /// </summary>
        /// <param name="bfsQueue"></param>
        /// <returns></returns>
        private GameObject Search(Queue<Transform> bfsQueue)
        {
            bool dummy;
            return Search(bfsQueue, out dummy);
        }

        /// <summary>
        /// Polls next game object out of the queue and returns whether the object has children.
        /// </summary>
        /// <param name="bfsQueue">The BFS queue.</param>
        /// <param name="hasChildren"></param>
        /// <returns></returns>
        private GameObject Search(Queue<Transform> bfsQueue, out bool hasChildren)
        {
            hasChildren = false;
            if (bfsQueue.Count == 0)
            {
                return null;
            }

            var nextTransform = bfsQueue.Dequeue();
            hasChildren = nextTransform.childCount > 0;

            return nextTransform.gameObject;
        }

        /// <summary>
        /// Investigates the given game object and add the children to the queue.
        /// </summary>
        /// <param name="bfsQueue">The BFS queue.</param>
        /// <param name="gameObject">The focused object</param>
        /// <returns></returns>
        private bool Investigate(Queue<Transform> bfsQueue, GameObject gameObject)
        {
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                bfsQueue.Enqueue(gameObject.transform.GetChild(i));
            }

            return gameObject.transform.childCount > 0;
        }

        #endregion

        public BuildingComponentSet GetOrCreateSet(string attributeID)
        {
            return m_attributeComponentMap.GetOrCreateSet(attributeID);
        }

        public BuildingComponent GetOrCreateComponent<T>(string id, GameObject template)  where T : BuildingComponent
        {
            BuildingComponentSet componentSet = GetOrCreateSet(id);
            BuildingComponent component;

            if (!componentSet.TryGetValue(id, out component))
            {
                var gameObject = GameObject.Instantiate(template) as GameObject;
                component = gameObject.GetComponent<T>();
                if(null == component)
                {
                    component = gameObject.AddComponent<T>();
                }

                componentSet.Add(id, component);
                // Set name as ID
                gameObject.name = id;
                component.ID = id;
            }

            return component;
        }


        public void AddAttribute(BuildingComponent c, NamedAttribute a)
        {
            var componentSet = m_attributeComponentMap.GetOrCreateSet(a.ID);
            c.Attribute.Add(a);
            
            if(!componentSet.ContainsKey(c.ID))
                componentSet.Add(c.ID, c);
        }

        public void AddAttribute(BuildingComponent c, string id, Attribute a)
        {
            var componentSet = m_attributeComponentMap.GetOrCreateSet(id);
            c.Attribute.Add(new NamedAttribute { ID = id, Value = a });
            
            if (!componentSet.ContainsKey(c.ID))
                componentSet.Add(c.ID, c);
        }

        public void Clean()
        {
            m_attributeComponentMap.Clear();

            List<Transform> children = new List<Transform>();
            foreach (Transform t in gameObject.transform)
                children.Add(t);

            children.ForEach(t => DestroyImmediate(t));
        }

        #endregion
    }

}