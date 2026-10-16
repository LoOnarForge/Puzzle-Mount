using UnityEngine;

/// <summary>
/// Basic Music Manager to play background music.
/// </summary>
public class MusicManager : MonoBehaviour
{
    // Singleton instance for global access.
    public static MusicManager Instance { get; private set; }

    [SerializeField] private AudioClip[] songs;
    [SerializeField] private bool playOnAwake = true;
    [SerializeField] private bool loopPlaylist = true;

    private AudioSource audioSource;
    private int currentSongIndex = 0;

    private void Awake()
    {
        // Simple singleton setup.
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start()
    {
        if (playOnAwake && songs != null && songs.Length > 0)
        {
            PlaySong(0);
        }
    }

    private void Update()
    {
        // Check if the current song has finished playing.
        if (!audioSource.isPlaying && loopPlaylist && songs != null && songs.Length > 0)
        {
            PlayNextSong();
        }
    }

    /// <summary>
    /// Plays the next song in the playlist.
    /// </summary>
    public void PlayNextSong()
    {
        if (songs == null || songs.Length == 0) return;

        currentSongIndex = (currentSongIndex + 1) % songs.Length;
        PlaySong(currentSongIndex);
    }

    /// <summary>
    /// Plays a specific song by index.
    /// </summary>
    public void PlaySong(int index)
    {
        if (songs == null || index < 0 || index >= songs.Length) return;

        currentSongIndex = index;
        audioSource.clip = songs[currentSongIndex];
        audioSource.Play();
    }
    
    /// <summary>
    /// Stops the currently playing music.
    /// </summary>
    public void StopMusic()
    {
        audioSource.Stop();
    }
}
