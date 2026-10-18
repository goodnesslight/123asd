using UnityEngine;

namespace BestStop
{
    // Bottom-center pivots let props overlap correctly when moved across the parking lot.
    [ExecuteAlways, RequireComponent(typeof(SpriteRenderer))]
    public sealed class GroundSort : MonoBehaviour
    {
        public Transform groundPoint;
        public int orderOffset;
        void LateUpdate()
        {
            float y = groundPoint != null ? groundPoint.position.y : transform.position.y;
            GetComponent<SpriteRenderer>().sortingOrder = 2000 - Mathf.RoundToInt(y * 2) + orderOffset;
        }
    }
}
