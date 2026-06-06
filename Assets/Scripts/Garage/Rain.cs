using UnityEngine;

public class RainToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ParticleSystem rainParticleSystem;
    [SerializeField] private AudioSource rainAudioSource;

    [Header("Audio Settings")]
    [Range(0f, 1f)][SerializeField] private float maxVolume = 0.5f;
    [SerializeField] private float fadeSpeed = 2f;

    private bool isRaining = false;
    private float targetVolume = 0f;

    void Start()
    {
        // Ensure the rain particles and audio are off at startup
        if (rainParticleSystem != null)
        {
            rainParticleSystem.Stop();
        }
        else
        {
            Debug.LogWarning("Rain Particle System is not assigned to the RainToggle script!");
        }

        if (rainAudioSource != null)
        {
            rainAudioSource.volume = 0f;
            rainAudioSource.Stop();
            // Optional but recommended: Ensure looping is true for continuous rain sound
            rainAudioSource.loop = true;
        }
    }

    void Update()
    {
        // Smoothly fades the audio volume over time for a cleaner transition
        if (rainAudioSource != null && rainAudioSource.isPlaying)
        {
            rainAudioSource.volume = Mathf.MoveTowards(rainAudioSource.volume, targetVolume, fadeSpeed * Time.deltaTime);

            // Completely stop the audio track once it has fully faded out
            if (targetVolume == 0f && rainAudioSource.volume == 0f)
            {
                rainAudioSource.Stop();
            }
        }
    }

    // This function will be called by your UI Button
    public void ToggleRain()
    {
        if (rainParticleSystem == null) return;

        // Toggle the state
        isRaining = !isRaining;

        if (isRaining)
        {
            rainParticleSystem.Play();

            if (rainAudioSource != null)
            {
                targetVolume = maxVolume;
                if (!rainAudioSource.isPlaying) rainAudioSource.Play();
            }
        }
        else
        {
            rainParticleSystem.Stop();

            if (rainAudioSource != null)
            {
                targetVolume = 0f;
            }
        }
    }
}