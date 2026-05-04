using UnityEngine;

public class MusicManager : MonoBehaviour
{
    [SerializeField]
    AudioSource[] tracks;
    int trackIndex = 0;

    void Start()
    {
        double startTime = AudioSettings.dspTime + 1.0;

        foreach (AudioSource track in tracks)
        {
            track.PlayScheduled(startTime);
            track.mute = true;
            trackIndex = trackIndex + 1;
            Debug.Log(trackIndex);
        }
        PlayMain();
    }

    private void OnValidate()
    {
        tracks = GetComponents<AudioSource>();
    }

    void PlayRelaxed()
    {
        trackIndex = 0;
        foreach (AudioSource track in tracks)
        {
            if (trackIndex <= 21 && trackIndex >= 16)
            {
                track.mute = false;
            }
            trackIndex = trackIndex + 1;
            Debug.Log(trackIndex);
        }

    }
    void PlayMain()
    {
        trackIndex = 0;
        foreach (AudioSource track in tracks)
        {
            if (trackIndex <= 15 && trackIndex >= 11 | trackIndex == 0)
            {
                track.mute = false;
            }
            trackIndex = trackIndex + 1;
            Debug.Log(trackIndex);
        }

    }
    void PlayIntense()
    {
        trackIndex = 0;
        foreach (AudioSource track in tracks)
        {
            if (trackIndex <= 11 && trackIndex >= 0)
            {
                track.mute = false;
            }
            trackIndex = trackIndex + 1;
            Debug.Log(trackIndex);
        }

    }
}
