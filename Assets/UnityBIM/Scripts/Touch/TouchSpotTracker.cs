using UnityEngine;
using System.Collections;

namespace UnityBitub.Input
{
    /// <summary>
    /// A moving spot tracker. Follows a 3D cursor object and attempts to holds
    /// a given distance.
    /// </summary>
    public class TouchSpotTracker : MonoBehaviour
    {
        public float smooth = 1.5f; // The relative speed at which the camera will catch up.
        public float preferredDistance = 5.0f; // The preferred distance
        public float minimalDistance = 1.0f;
        public float maximalDistance = 10.0f;

        public GameObject cursorObject;     // Reference to the player's object.

        #region Private

        private Vector3 m_newPosition;             // The position the camera is trying to reach.

        #endregion

        void FixedUpdate()
        {
            // A ray from camera position to cursor's object position
            Vector3 ray = transform.position - cursorObject.transform.position;
            // Actual distance
            float distance = ray.magnitude;

            ray.Normalize();

            // Calculate preferred position
            Vector3 preferredPos = cursorObject.transform.position + ray * preferredDistance;
            // Calculate boundaries of acceptance
            Vector3 maxDistancePos = cursorObject.transform.position + ray * maximalDistance;
            Vector3 minDistancePos = cursorObject.transform.position + ray * minimalDistance;

            Vector3[] checkPoints = new Vector3[5];

            checkPoints[0] = preferredPos;
            checkPoints[1] = Vector3.Lerp(minDistancePos, maxDistancePos, 0.05f);
            checkPoints[2] = Vector3.Lerp(minDistancePos, maxDistancePos, 0.25f);
            checkPoints[3] = Vector3.Lerp(minDistancePos, maxDistancePos, 0.50f);
            checkPoints[4] = preferredPos;

            // Run through the check points...
            for (int i = 0; i < checkPoints.Length; i++)
            {
                // ... if the camera can see the player...
                if (ViewingPosCheck(checkPoints[i]))
                {
                    // ... break from the loop.
                    break;
                }
            }

            // Lerp the camera's position between it's current position and it's new position.
            transform.position = Vector3.Lerp(transform.position, m_newPosition, smooth * Time.deltaTime);

            // Make sure the camera is looking at the player.
            SmoothLookAt();
        }


        /// <summary>
        /// Checks whether given position has a barrier free view to player.
        /// </summary>
        /// <returns><c>true</c>, if position has a free view direction towards player's object</return>
        /// <param name="pos">The position vector.</param>
        private bool ViewingPosCheck(Vector3 pos)
        {
            RaycastHit hit;

            // If a raycast from the check position to the player hits something...
            Vector3 direction = cursorObject.transform.position - pos;
            if (Physics.Raycast(pos, direction, out hit, direction.magnitude))
            {

                // ... if it is not the player...
                if (!hit.transform.gameObject.tag.Equals(Tags.Player))
                {

                    return false;
                }
            }

            // If we haven't hit anything or we've hit the player, this is an appropriate position.
            m_newPosition = pos;
            return true;
        }

        /// <summary>
        /// Rotate position vector.
        /// </summary>
        private void SmoothLookAt()
        {
            // Create a vector from the camera towards the player.
            Vector3 relPlayerPosition = cursorObject.transform.position - transform.position;

            // Create a rotation based on the relative position of the player being the forward vector.
            Quaternion lookAtRotation = Quaternion.LookRotation(relPlayerPosition, Vector3.up);

            // Lerp the camera's rotation between it's current rotation and the rotation that looks at the player.
            transform.rotation = Quaternion.Lerp(transform.rotation, lookAtRotation, smooth * Time.deltaTime);
        }

    }

}