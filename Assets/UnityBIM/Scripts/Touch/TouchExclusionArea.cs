using UnityEngine;
using System.Collections;

public class TouchExclusionArea : MonoBehaviour {

    public Vector2 screenOrigin;
    public Vector2 extents;

    public Rect Area
    {
        get {
            float ox = Screen.width * screenOrigin.x;
            float oy = Screen.height * screenOrigin.y;

            return new Rect(ox, oy, Screen.width * extents.x, Screen.height * extents.y); 
        }
    }
}
