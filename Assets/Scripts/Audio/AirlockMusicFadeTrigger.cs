using Game.Audio;
using UnityEngine;

// Helper for airlock setups where entering a trigger starts the transition before the scene actually loads
// Thius is supposed to be attached to the airlock entry trigger if your transition script is separate from the trigger volume.
[RequireComponent(typeof(Collider))]
public class AirlockMusicFadeTrigger : MonoBehaviour {
	[Header("Trigger")]
	[Tooltip("Only objects with this tag can trigger the music fade out.")]
	[SerializeField] private string playerTag = "Player";

	[Tooltip("If true, this trigger only fades the music once.")]
	[SerializeField] private bool onlyTriggerOnce = true;

	[Header("Music")]
	[Tooltip("How long the current level music takes to fade out after entering the airlock.")]
	[SerializeField] private float fadeOutDuration = 2.0f;

	private bool triggered;

	private void Reset() {
		Collider triggerCollider = GetComponent<Collider>();
		triggerCollider.isTrigger = true;
	}

	private void OnTriggerEnter(Collider other) {
		if (onlyTriggerOnce && triggered) {
			return;
		}

		if (other.CompareTag(playerTag) == false) {
			return;
		}

		triggered = true;
		MusicManager.Stop(fadeOutDuration);
	}

#if UNITY_EDITOR
	private void OnValidate() {
		fadeOutDuration = Mathf.Max(0.0f, fadeOutDuration);
	}
#endif
}
