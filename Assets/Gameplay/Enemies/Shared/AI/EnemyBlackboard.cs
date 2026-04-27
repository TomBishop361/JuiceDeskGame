using UnityEngine;

namespace Game.AI {
	[DisallowMultipleComponent]
	public sealed class EnemyBlackboard : MonoBehaviour {
		[Tooltip("Current target the enemy is tracking.")]
		[field: SerializeField] public Transform Target { get; private set; }
		[Tooltip("True when the enemy currently has a valid target reference.")]
		[field: SerializeField] public bool HasTarget { get; private set; }
		[Tooltip("True when the enemy currently has line of sight to its target.")]
		[field: SerializeField] public bool HasLineOfSight { get; private set; }
		[Tooltip("Distance from this enemy to its current target.")]
		[field: SerializeField] public float DistanceToTarget { get; private set; } = Mathf.Infinity;
		[Tooltip("The last time the enemy had confirmed line of sight to its target.")]
		[field: SerializeField] public float LastSeenTime { get; private set; } = -Mathf.Infinity;
		//[Tooltip("Last confirmed world position where the target was seen.")]
		//[field: SerializeField] public Vector3 LastSeenPosition { get; private set; }
		//[Tooltip("True when a valid last-seen position is available for investigate movement.")]
		//[field: SerializeField] public bool HasLastSeenPosition { get; private set; }
		[Tooltip("True once the enemy has entered its death state.")]
		[field: SerializeField] public bool IsDead { get; private set; }
		[Tooltip("True when this enemy is temporarily disabled and should not act.")]
		[field: SerializeField] public bool IsDisabled { get; private set; }

		// Assigns the current target and updates targeting flags
		// Clears LOS and distance when the target is removed
		public void SetTarget(Transform target) {
			Target = target;
			HasTarget = target != null;

			if (target == null) {
				DistanceToTarget = Mathf.Infinity;
				HasLineOfSight = false;
			}
		}

		// Updates the cached distance to the current target
		public void SetDistance(float distance) {
			DistanceToTarget = distance;
		}

		// Updates line of sight state and stores the time of the last confirmed sighting
		public void SetLineOfSight(bool hasLineOfSight) {
			HasLineOfSight = hasLineOfSight;
			if (hasLineOfSight) {
				LastSeenTime = Time.time;
			}
		}

		//// Updates last seen target position
		//public void SetLastSeenPosition(Vector3 position) {
		//	LastSeenPosition = position;
		//	HasLastSeenPosition = true;
		//	LastSeenTime = Time.time;
		//}

		//// Removes last seen target position
		//public void ClearLastSeenPosition() {
		//	HasLastSeenPosition = false;
		//	LastSeenPosition = Vector3.zero;
		//	LastSeenTime = -Mathf.Infinity;
		//}

		// Overrides the target-availability flag without replacing the current target reference
		// Useful for memory-based awareness or temporary loss of tracking
		public void SetHasTarget(bool hasTarget) {
			HasTarget = hasTarget;

			if (hasTarget == false && Target == null) {
				DistanceToTarget = Mathf.Infinity;
			}
		}

		// Marks the blackboard as dead or alive
		public void SetDead(bool isDead) {
			IsDead = isDead;
		}

		// Marks the blackboard as disabled or enabled
		// Disabled enemies should usually stop behaviour updates
		public void SetDisabled(bool isDisabled) {
			IsDisabled = isDisabled;
		}

		// Clears all runtime state so the blackboard is ready for reuse or respawn
		public void ResetRuntime() {
			Target = null;
			HasTarget = false;
			HasLineOfSight = false;
			DistanceToTarget = Mathf.Infinity;
			LastSeenTime = -Mathf.Infinity;
			//LastSeenPosition = Vector3.zero;
			//HasLastSeenPosition = false;
			IsDead = false;
			IsDisabled = false;
		}
	}
}