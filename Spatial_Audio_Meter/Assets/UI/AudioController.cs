using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public class AudioController : MonoBehaviour {
    [SerializeField]
    private AudioSource audioSource;
    [SerializeField]
    UnityPointilismVisualize visualizerScript;

    void Start() {
        visualizerScript = GetComponent<UnityPointilismVisualize>();
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// Returns current time of the audio source in seconds.
    /// </summary>
    /// <returns></returns>
    public float GetAudioTime() {
        return audioSource.time;
    }

    /// <summary>
    /// Sets audio playback location to the specified time in seconds.
    /// </summary>
    /// <param name="s"></param>
    public void SetAudioTimeSeconds(float s) {
        audioSource.time = s;
    }

    /// <summary>
    /// Sets a new audio clip and plays it.
    /// </summary>
    /// <param name="newClip"></param>
    public void UpdateAudioClip(AudioClip newClip) {
        audioSource.clip = newClip;
        visualizerScript.UpdateAudioClip(newClip);
        audioSource.Play();
    }

    public bool IsPlaying() {
        return audioSource.isPlaying;
    }

    public float GetAudioLength() {
        return audioSource.clip.length;
    }

    public void PlayAudio() => audioSource.Play();

    public void PauseAudio() => audioSource.Pause();
}
