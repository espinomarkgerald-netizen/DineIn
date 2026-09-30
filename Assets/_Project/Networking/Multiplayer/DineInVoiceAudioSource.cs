using UnityEngine;

// Voice has its own local volume controls; legacy SFX auto-routing must skip it.
[DisallowMultipleComponent]
public sealed class DineInVoiceAudioSource : MonoBehaviour { }