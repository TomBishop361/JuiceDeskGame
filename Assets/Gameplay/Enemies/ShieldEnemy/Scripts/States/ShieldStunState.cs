using UnityEngine;

namespace Game.AI.Shield {
	[DisallowMultipleComponent]
	public sealed class ShieldStunState : MonoBehaviour {
		[Header("Timing")]
		[SerializeField] private float hitStunDuration = 0.4f;

		[Header("Behaviour")]
		[SerializeField] private bool stopMinigunOnStun = true;
		[SerializeField] private bool lowerShieldOnStun = true;

		[Header("Animation")]
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
		public void ApplyHitStun(ShieldEnemy owner, Animator animator) {
			if (owner == null || owner.IsDead) {
				return;
			}

			if (stopMinigunOnStun) {
				owner.StopMinigunFiring();
			}

			if (lowerShieldOnStun) {
				owner.SetShieldRaised(false);
			}

			owner.StopMove();
			owner.SetStunnedFor(hitStunDuration);

			if (animator != null) {
				animator.SetTrigger(hitTriggerHash);
			}
		}
	}
}