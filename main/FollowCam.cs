using UnityEngine;

namespace FPSPrototype.Control {
    public class FollowCam : MonoBehaviour {
        [SerializeField] private Transform target;

        private void Update() {
            transform.position = target.position;
        }
    }
}