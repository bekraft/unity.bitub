using UnityEngine;
using System.Collections;

namespace UnityBitub.Input
{

    /// <summary>
    /// A moving spot behaviour. Distinguishes into 2 different modes: Orbit mode (single touch) and escape mode (quick movements).
    /// </summary>
    public class TouchSpotBehaviour : MonoBehaviour
    {
        #region Private

        private Vector3 m_targetPosition;
        private Vector3 m_rayVector;
        private float m_gradient = 0.0f;
        private float m_buttonClickTimeElapsed = float.PositiveInfinity;
        private bool m_isDoubleClick = false;
        private TouchExclusionArea[] m_exclAreas;

        #endregion

        public float speedFactor = 10e-3f;
        public float escapeFactor = 1.0f;
        public float threshold = 1;
        public float doubleClickDeltaTime = 0.2f;

        void Awake()
        {
            m_targetPosition = transform.position;
            m_rayVector = Vector3.forward;

            m_exclAreas = GameObject.FindObjectsOfType<TouchExclusionArea>();
        }

        /// <summary>
        /// True, if hot spot moves on spherical coordinates around player.
        /// </summary>
        bool IsInOrbitMode
        {
            get
            {
                if (UnityEngine.Input.GetMouseButtonDown(0))
                {
                    if (m_buttonClickTimeElapsed < doubleClickDeltaTime)
                    {

                        m_isDoubleClick = true;
                    }
                }

                if (m_isDoubleClick)
                {

                    m_gradient = 2 * threshold;
                }

                if (!UnityEngine.Input.GetMouseButton(0))
                {
                    m_buttonClickTimeElapsed += Time.deltaTime;
                }

                if (UnityEngine.Input.GetMouseButtonUp(0))
                {

                    m_buttonClickTimeElapsed = 0;
                    m_isDoubleClick = false;
                }

                return m_gradient <= threshold;
            }
        }

        void OnGUI()
        {
        }


        void Update()
        {
            var ray = Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition);

            if (UnityEngine.Input.GetMouseButton(0))
            {

                // Check whether cursor is inside an eclusion area
                foreach (TouchExclusionArea a in m_exclAreas)
                {
                    if (a.Area.Contains(UnityEngine.Input.mousePosition))
                    {

                        return;
                    }
                }

                // Get faster with higher gradients
                if (!IsInOrbitMode)
                {

                    m_targetPosition = transform.position + ray.direction * escapeFactor;
                }
                else
                {

                    Vector3 spotVector = transform.position - ray.origin;
                    m_targetPosition = ray.direction * spotVector.magnitude + ray.origin;
                }

                m_rayVector = ray.direction;
            }
            else
            {

                m_gradient = float.NegativeInfinity;
                m_rayVector = Vector3.zero;
            }

            transform.position = Vector3.Lerp(transform.position, m_targetPosition, speedFactor * Time.deltaTime);
        }


        void OnMouseDrag()
        {
            var ray = Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition);
            float grad = (1 - Vector3.Dot(m_rayVector, ray.direction)) / Time.deltaTime;

            if (Mathf.Abs(grad) > m_gradient)
            {
                m_gradient = Mathf.Abs(grad);
            }
            m_rayVector = ray.direction;
        }
    }

}