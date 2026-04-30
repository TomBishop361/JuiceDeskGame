using UnityEngine;

public class MusicManager : MonoBehaviour
{
    [SerializeField]
    AudioSource[] tracks;

   

    void Start()
    {
        double startTime = AudioSettings.dspTime + 1.0;

        foreach (AudioSource track in tracks)
        {
            track.PlayScheduled(startTime);
        }
    }

    private void OnValidate()
    {
        tracks = GetComponents<AudioSource>();
    }
}
