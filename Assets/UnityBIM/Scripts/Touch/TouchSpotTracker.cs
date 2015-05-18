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
        public float smooth = 1.5f; 
        public float preferredDistance = 5.0f;
        public float minimalDistance = 1.0f;
        public float maximalDistance = 10.0f;

        public GameObject cursorObject; 

        void FixedUpdate()
        {
            var newPosition = CalculateBestPosition();

            // Lerp the camera's position between it's current position and it's new position.
            transform.position = Vector3.Lerp(transform.position, newPosition, smooth * Time.deltaTime);

            // Make sure the camera is looking at the player.
            SmoothLookAt();
        }


        private Vector3 CalculateBestPosition()
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

            //
            checkPoints[0] = preferredPos;
            checkPoints[1] = Vector3.Lerp(minDistancePos, maxDistancePos, 0.05f);
            checkPoints[2] = Vector3.Lerp(minDistancePos, maxDistancePos, 0.25f);
            checkPoints[3] = Vector3.Lerp(minDistancePos, maxDistancePos, 0.50f);
            checkPoints[4] = preferredPos;

            // Run through the check points...
            for (int i = 0; i < checkPoints.Length; i++)
            {
                RaycastHit hit;
                Vector3 direction = cursorObject.transform.position - checkPoints[i];
                if (Physics.Raycast(checkPoints[i], direction, out hit, direction.magnitude))
                {
                    if (hit.transform.gameObject.tag.Equals("Player"))
                    {
                        // If there's no barrier -> use this position
                        return checkPoints[i];
                    }
                }
            }

            return transform.position;
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