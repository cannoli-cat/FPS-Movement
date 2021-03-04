using UnityEngine;

public class FollowCam : MonoBehaviour {
    [SerializeField] private Transform target;

    private void Update() {
        transform.position = target.position;
    }
}