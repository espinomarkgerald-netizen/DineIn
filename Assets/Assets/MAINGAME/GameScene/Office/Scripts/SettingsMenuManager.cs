using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class SettingsMenuManager : MonoBehaviour
{
    // ── Audio ──────────────────────────────────────────────────────────────────
    public Slider masterVol, musicVol, sfxVol;
    public AudioMixer audioMixer;

    // ── Hospitality vocabulary toggle ──────────────────────────────────────────
    [Header("Hospitality Vocabulary")]
    [Tooltip("When on, all labels switch to real hospitality terminology. " +
             "Assessment-ready mode — no gameplay changes.")]
    [SerializeField] private Toggle hospitalityVocabToggle;

    /// <summary>Fired whenever the hospitality vocabulary mode changes.</summary>
    public static event System.Action<bool> OnHospitalityVocabChanged;

    /// <summary>Whether hospitality vocabulary mode is currently active.</summary>
    public static bool HospitalityVocabEnabled { get; private set; }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        if (hospitalityVocabToggle != null)
        {
            hospitalityVocabToggle.isOn = HospitalityVocabEnabled;
            hospitalityVocabToggle.onValueChanged.AddListener(OnHospitalityToggleChanged);
        }
    }

    private void OnDestroy()
    {
        if (hospitalityVocabToggle != null)
            hospitalityVocabToggle.onValueChanged.RemoveListener(OnHospitalityToggleChanged);
    }

    // ── Audio ──────────────────────────────────────────────────────────────────

    /// <summary>Sets the master volume on the AudioMixer from the slider value.</summary>
    public void ChangeMasterVolume()
    {
        audioMixer.SetFloat("MasterVol", masterVol.value);
    }

    /// <summary>Sets the music volume on the AudioMixer from the slider value.</summary>
    public void ChangeMusicVolume()
    {
        audioMixer.SetFloat("MusicVol", musicVol.value);
    }

    /// <summary>Sets the SFX volume on the AudioMixer from the slider value.</summary>
    public void ChangeSFXVolume()
    {
        audioMixer.SetFloat("SFXVol", sfxVol.value);
    }

    // ── Hospitality vocabulary ─────────────────────────────────────────────────

    /// <summary>
    /// Called by the Toggle's onValueChanged event. Stores the setting and
    /// broadcasts it so any listener (e.g. HUD labels) can react immediately.
    /// </summary>
    private void OnHospitalityToggleChanged(bool isOn)
    {
        HospitalityVocabEnabled = isOn;
        OnHospitalityVocabChanged?.Invoke(isOn);
    }
}
