using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// Oblique overhead, side-on broadcast camera (Master Doc 3.1: 斜め俯瞰).
    /// Sits on the -Z side of the pitch looking at +Z, so +X (HOME's attack) is screen right.
    /// </summary>
    public class MatchCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 16f;
        public float pitchDeg = 42f;
        public float followSpeed = 6f;
        public float fieldOfView = 55f;

        void Start()
        {
            Camera cam = GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = fieldOfView;
            transform.rotation = Quaternion.Euler(pitchDeg, 0f, 0f);
            if (target != null) transform.position = Desired();
        }

        Vector3 Desired()
        {
            Quaternion rot = Quaternion.Euler(pitchDeg, 0f, 0f);
            return target.position - rot * Vector3.forward * distance;
        }

        void LateUpdate()
        {
            if (target == null) return;
            transform.rotation = Quaternion.Euler(pitchDeg, 0f, 0f);
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, Desired(), t);
        }
    }
}
