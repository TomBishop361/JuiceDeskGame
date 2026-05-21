using UnityEngine;

namespace Game.AI.Drone {
	[DisallowMultipleComponent]
	public sealed class DroneKnockdownState : MonoBehaviour {
		[Header("Timing")]
		[Tooltip("Duration (in seconds) the drone remains knocked down")]
		[SerializeField] private float knockdownDuration = 1.2f;

		//[Header("Animation")]
		//[SerializeField] private string knockedBoolName = "KnockedDown";
		//[SerializeField] private string hitTriggerName = "Hit";

		//private int knockedBoolHash;
		//private int hitTriggerHash;

		public bool IsKnockedDown { get; private set; }

		//// Caches the animator trigger hash used for knockdown hit reaction playback
		//private void Awake() {
		//	//knockedBoolHash = Animator.StringToHash(knockedBoolName);
		//	//hitTriggerHash = Animator.StringToHash(hitTriggerName);
		//}

		// Resets knocked module state for pooling or respawn
		public void ResetRuntime(/*Animator animator*/) {
			IsKnockedDown = false;

			//if (animator != null) {
			//	animator.SetBool(knockedBoolHash, false);
			//}
		}

		// Stops movement + applies knockdown stun to the owner + optionally triggers the hit reaction animation
		public void EnterKnockdown(DroneEnemy owner/*, Animator animator*/) {
			if (owner == null || owner.IsDead) {
				return;
			}

			IsKnockedDown = true;
			owner.StopMove();
			owner.SetStunnedFor(knockdownDuration);

			//if (animator != null) {
			//	animator.SetTrigger(hitTriggerHash);
			//	animator.SetBool(knockedBoolHash, true);
			//}
		}

		public void Tick(DroneEnemy owner/*, Animator animator*/) {
			if (IsKnockedDown == false || owner == null) {
				return;
			}

			owner.StopMove();

			if (owner.IsStunned == false) {
				Recover(/*animator*/);
			}
		}

		public void OnDeath(/*Animator animator*/) {
			Recover(/*animator*/);
		}

		private void Recover(/*Animator animator*/) {
			IsKnockedDown = false;

			//if (animator != null) {
			//	animator.SetBool(knockedBoolHash, false);
			//}
		}
	}
}