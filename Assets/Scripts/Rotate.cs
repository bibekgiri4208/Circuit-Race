using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    public void FixedUpdate()
    {
        transform.Rotate(0, 0.2f, 0);
    }
}
