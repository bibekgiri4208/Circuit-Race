using UnityEngine;
using Fusion;

public class NetworkCameraFollow : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0, 3, -6);
    [SerializeField] private float smoothSpeed = 10f;
    private Transform _targetCar;

    void LateUpdate()
    {
        // If we don't have a target yet, look for a spawned network car
        if (_targetCar == null)
        {
            NetworkObject[] networkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
            foreach (var netObj in networkObjects)
            {
                // Find the network object that belongs to the local player and has a collider/rigidbody (the car)
                if (netObj.HasInputAuthority && netObj.CompareTag("Player"))
                {
                    _targetCar = netObj.transform;
                    break;
                }
            }

            // Fallback: Just grab the first NetworkTransform if tags aren't set up yet
            if (_targetCar == null && networkObjects.Length > 0)
            {
                foreach (var netObj in networkObjects)
                {
                    if (netObj.HasInputAuthority && netObj.GetComponent<NetworkTransform>() != null)
                    {
                        _targetCar = netObj.transform;
                        break;
                    }
                }
            }
            return;
        }

        // Smoothly follow the network car
        Vector3 desiredPosition = _targetCar.position + _targetCar.TransformDirection(offset);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.LookAt(_targetCar.position + Vector3.up * 1.5f);
    }
}