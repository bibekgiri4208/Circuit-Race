using UnityEngine;

public class UIManagerAudio : MonoBehaviour
{
    [Header("Audio Components")]
    [SerializeField] private AudioSource audioSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip clickSound;

    void Start()
    {
        // Setup the AudioSource via code as a safeguard
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
        }
    }

    // Call this function from ANY button's On Click() event
    public void PlayClickSound()
    {
        if (audioSource != null && clickSound != null)
        {
            // PlayOneShot allows overlapping sounds without cutting off a previous click
            audioSource.PlayOneShot(clickSound);
        }
    }
}