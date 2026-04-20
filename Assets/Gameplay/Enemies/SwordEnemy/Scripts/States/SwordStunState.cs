using UnityEngine;

namespace Game.AI.Sword {
	// Handles hit stun application for the sword enemy, including stun timing + optional hit reaction animation triggering
	[DisallowMultipleComponent]
	public class SwordStunState : MonoBehaviour {
		[Header("Timing")]
		[Tooltip("Duration of hit stun applied when the sword enemy is interrupted by damage.")]
		[SerializeField] private float hitStunDuration = 0.6f;

		[Header("Animation")]
		[Tooltip("Animator trigger name used for the sword enemy's hit reaction.")]
		[SerializeField] private string hitTriggerName = "Hit";

		private int hitTriggerHash;

		// Caches the animator trigger hash used for hit reaction playback
		private void Awake() {
			hitTriggerHash = Animator.StringToHash(hitTriggerName);
		}

		// Resets stun module state for pooling or respawn
		public void ResetRuntime() {
		}

		// Stops movement + applies hit stun to the owner + optionally triggers the hit reaction animation
		public void ApplyHitStun(SwordEnemy owner, Animator animator) {
			if (owner == null || owner.IsDead) {
				return;
			}

			owner.StopMove();
			owner.SetStunnedFor(hitStunDuration);

			if (animator != null) {
				animator.SetTrigger(hitTriggerHash);
			}
		}
	}
}