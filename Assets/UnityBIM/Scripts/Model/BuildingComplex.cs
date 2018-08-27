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

        public Shader ShaderOpaque;
        public Shader ShaderTransparent;

        public MaterialID[] MaterialList;

        public string[] AttributeList = new string[] { "MaterialName" };

        void Awake()
        {
             ShaderTransparent = Shader.Find("Transparent/Diffuse");
             ShaderOpaque = Shader.Find("Diffuse");
        }

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

        public BuildingComponent GetOrCreateComponent<T>(string name, GameObject template)  where T : BuildingComponent
        {
            BuildingComponent component;
            var gameObject = GameObject.Instantiate(template) as GameObject;
            component = gameObject.GetComponent<T>();
            if(null == component)
            {
                component = gameObject.AddComponent<T>();
            }

            // Set name as ID
            gameObject.name = name;
            component.ID = name;

            return component;
        }


        #endregion
    }

}