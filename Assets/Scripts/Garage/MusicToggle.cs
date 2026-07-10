using UnityEngine;
using UnityEngine.UI;

public class MusicToggle : MonoBehaviour
{
    public AudioSource musicSource;

    public Sprite soundOnSprite;
    public Sprite soundOffSprite;

    private Image buttonImage;

    private bool isMuted = false;

    void Start()
    {
        buttonImage = GetComponent<Image>();

        UpdateButtonImage();
    }

    public void ToggleMusic()
    {
        isMuted = !isMuted;

        musicSource.mute = isMuted;

        UpdateButtonImage();
    }

    void UpdateButtonImage()
    {
        if (isMuted)
        {
            buttonImage.sprite = soundOffSprite;
        }
        else
        {
            buttonImage.sprite = soundOnSprite;
        }
    }
}