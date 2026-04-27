using UnityEngine;

namespace Game.AI {
	// Handles target acquiring + LOS checks + target memory for an enemy
	// Writes results to the shared enemy blackboard
	public sealed class EnemyPerception : MonoBehaviour {
		[Header("Targeting")]
		[Tooltip("Optional target assigned directly in the inspector. If set, this target is used before auto-acquiring the player.")]
		[SerializeField] private Transform explicitTarget;
		[Tooltip("If true, the perception system automatically looks for the player using the configured tag when no explicit target is assigned.")]
		[SerializeField] private bool autoAcquirePlayerByTag = true;
		[Tooltip("Tag used when auto-acquiring the player target.")]
		[SerializeField] private string playerTag = "Player";
		[Tooltip("How long the enemy remembers the player after losing line of sight.")]
		[SerializeField] private float targetMemoryDuration = 1.25f;

		[Header("Sensors")]
		[Tooltip("Optional LOS sensor used to confirm whether the current target is visible.")]
		[SerializeField] private LOSSensor losSensor;
		[Tooltip("If true, the enemy can still keep a target even when no LOS sensor is assigned.")]
		[SerializeField] private bool allowTargetWithoutSensor = false;

		// The target currently being tracked by this perception component
		// May be assigned explicitly or found automatically by tag.
		public Transform CurrentTarget => explicitTarget;

		private void Reset() {
			if (losSensor == null) {
				losSensor = GetComponent<LOSSensor>();
			}
		}

		// Assigns the LOS sensor used for visibility checks
		public void SetSensor(LOSSensor sensor) {
			losSensor = sensor;
		}

		// Sets a specific target to track directly
		public void SetExplicitTarget(Transform target) {
			explicitTarget = target;
		}

		// Clears the currently assigned explicit target
		public void ClearTarget() {
			explicitTarget = null;
		}

		// Resets runtime state while preserving any inspector-assigned explicit target
		// Useful when the enemy is reused from a pool.
		public void ResetRuntime() {
			// Keep explicitTarget assigned if set in inspector
		}

		// Immediately finds the current target and writes initial target and distance data into the blackboard
		public void AcquireTargetImmediately(Transform owner, EnemyBlackboard blackboard) {
			if (blackboard == null) {
				return;
			}

			if (explicitTarget == null && autoAcquirePlayerByTag) {
				TryFindPlayer();
			}

			blackboard.SetTarget(explicitTarget);

			if (explicitTarget != null) {
				blackboard.SetDistance(Vector3.Distance(owner.position, explicitTarget.position));
				//blackboard.SetHasTarget(false);
				//blackboard.SetLineOfSight(false);
				//blackboard.SetDistance(Mathf.Infinity);
			}

			//float distance = Vector3.Distance(owner.position, explicitTarget.position);
			//blackboard.SetDistance(distance);

			//bool hasLOS = allowTargetWithoutSensor;
			//if (losSensor != null) {
			//	hasLOS = losSensor.HasLOS(explicitTarget);

			//}

			//blackboard.SetLineOfSight(hasLOS);

			//if (hasLOS) {
			//	blackboard.SetLastSeenPosition(explicitTarget.position);
			//}

			//bool keepTarget = hasLOS || blackboard.HasLastSeenPosition;
			//blackboard.SetHasTarget(keepTarget);
		}

		// Updates target + distance + line of sight + memory-based target retention, then writes the results to the blackboard
		public void Tick(Transform owner, EnemyBlackboard blackboard) {
			if (blackboard == null) {
				return;
			}

			if (explicitTarget == null && autoAcquirePlayerByTag) {
				TryFindPlayer();
			}

			blackboard.SetTarget(explicitTarget);

			if (explicitTarget == null) {
				blackboard.SetHasTarget(false);
				blackboard.SetLineOfSight(false);
				blackboard.SetDistance(Mathf.Infinity);
				//blackboard.ClearLastSeenPosition();
				return;
			}

			float distance = Vector3.Distance(owner.position, explicitTarget.position);
			blackboard.SetDistance(distance);

			bool hasLOS = allowTargetWithoutSensor;
			if (losSensor != null) {
				hasLOS = losSensor.HasLOS(explicitTarget);
			}

			blackboard.SetLineOfSight(hasLOS);

			bool keepTarget = hasLOS || (Time.time - blackboard.LastSeenTime) <= targetMemoryDuration;
			blackboard.SetHasTarget(keepTarget);

			//if (hasLOS) {
			//	blackboard.SetLastSeenPosition(explicitTarget.position);
			//}

			//bool hasFreshMemory = blackboard.HasLastSeenPosition && (Time.time - blackboard.LastSeenTime) <= targetMemoryDuration;

			//blackboard.SetHasTarget(hasLOS || hasFreshMemory);
		}

		// Attempts to find the player using the configured tag and assign it as the current target
		private void TryFindPlayer() {
			GameObject player = GameObject.FindGameObjectWithTag(playerTag);
			explicitTarget = player != null ? player.transform : null;
		}
	}
}
