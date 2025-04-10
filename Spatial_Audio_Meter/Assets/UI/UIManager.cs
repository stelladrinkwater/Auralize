using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour {
    [SerializeField]
    private AudioController audioController;
    [SerializeField]
    private List<AudioClip> audioList;

    Slider audioSlider;
    private Button playButton;
    private Button exitButton;
    private DropdownField audioDropdown;
    private bool isDragging = false;

    void OnEnable() {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        audioSlider = root.Q<Slider>("AudioProgressSlider");
        playButton = root.Q<Button>("PlayButton");
        audioDropdown = root.Q<DropdownField>("SelectAudioDropdown");
        exitButton = root.Q<Button>("ExitButton");

        // Set slider to value of currently playing audio.
        audioSlider.lowValue = 0;
        audioSlider.highValue = Mathf.CeilToInt(audioController.GetAudioLength());
        audioSlider.value = Mathf.CeilToInt(audioController.GetAudioTime());

        var dragContainer = audioSlider.Q("unity-drag-container");
        dragContainer.RegisterCallback<MouseUpEvent>(OnSliderPointerUp);

        audioSlider.RegisterCallback<ChangeEvent<float>>(OnSliderValueChanged);
        audioSlider.RegisterCallback<PointerDownEvent>(OnSliderPointerDown, useTrickleDown: TrickleDown.TrickleDown);
        audioSlider.RegisterCallback<MouseUpEvent>(OnSliderPointerUp, useTrickleDown: TrickleDown.TrickleDown);
        root.RegisterCallback<MouseUpEvent>(OnSliderPointerUp, useTrickleDown: TrickleDown.TrickleDown);

        playButton.clicked += OnPlayButtonClick;
        exitButton.clicked += Quit;

        if (audioList.Count > 0) {
            List<string> audioNames = audioList.Select(a => a.name).ToList();
            audioDropdown.choices = audioNames;
            audioDropdown.value = audioNames[0];
        } else {
            Debug.LogWarning("No audio set in the audio list of UI Manager.");
        }

        audioDropdown.RegisterCallback<ChangeEvent<string>>(OnAudioDropdownChange);
    }

    void Update() {
        if (!isDragging) {
            audioSlider.value = Mathf.CeilToInt(audioController.GetAudioTime());
        }
    }

    void OnSliderValueChanged(ChangeEvent<float> evt) {
        if (isDragging) {
            audioController.SetAudioTimeSeconds(evt.newValue);
        }
    }

    void OnSliderPointerDown(PointerDownEvent evt) {
        audioController.PauseAudio();
        isDragging = true;
    }

    void OnSliderPointerUp(MouseUpEvent evt) {
        if (isDragging) {
            audioController.PlayAudio();
            isDragging = false;
        }
    }

    void OnPlayButtonClick() {
        if (audioController.IsPlaying()) {
            audioController.PauseAudio();
        } else {
            audioController.PlayAudio();
        }
    }

    void OnAudioDropdownChange(ChangeEvent<string> evt) {
        string selectedTrackName = evt.newValue;
        AudioClip newClip = audioList.Find(t => t.name == selectedTrackName);
        audioController.UpdateAudioClip(newClip);

        // Set slider to value of currently playing audio.
        audioSlider.lowValue = 0;
        audioSlider.highValue = Mathf.CeilToInt(audioController.GetAudioLength());
    }

    void Quit() {
        Application.Quit();
    }
}
