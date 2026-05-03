using UnityEngine;

namespace Game.AI {
	// Handles enemy target detection + LOS checks + target memory
	// Writes perception results into EnemyBlackboard so BT's can react
	public sealed class EnemyPerception : MonoBehaviour {
		[Header("Targeting")]
		[Tooltip("Optional target assigned directly in the inspector. If set, this target is used before auto-acquiring the player.")]
		[SerializeField] private Transform explicitTarget;
		[Tooltip("If true, the perception system automatically looks for the player using the configured tag when no explicit target is assigned.")]
		[SerializeField] private bool autoAcquirePlayerByTag = true;
		[Tooltip("Tag used when auto-acquiring the player target.")]
		[SerializeField] private string playerTag = "Player";
		[Tooltip("How long the enemy remembers the player after losing line of sight.")]
		[SerializeField] private float targetMemoryDuration = 1.75f;

		[Header("Sensors")]
		[Tooltip("Optional LOS sensor used to confirm whether the current target is visible.")]
		[SerializeField] private LOSSensor losSensor;
		[Tooltip("If true, the enemy can still keep a target even when no LOS sensor is assigned.")]
		[SerializeField] private bool allowTargetWithoutSensor = false;

		[Header("Squad Sharing")]
		[Tooltip("If true, confirmed sightings are shared through EnemySquadMember / EnemySquadDirector.")]
		[SerializeField] private bool shareSightingsWithSquad = true;
		[Tooltip("Minimum seconds between squad sighting reports from this enemy.")]
		[SerializeField] private float sightingShareInterval = 0.25f;

		// The target currently being tracked by this perception component
		public Transform CurrentTarget => explicitTarget;

		private EnemySquadMember squadMember;
		private float nextSightingShareTime;

		private void Reset() {
			if (losSensor == null) {
				losSensor = GetComponent<LOSSensor>();
			}
		}

		private void Awake() {
			if (losSensor == null) {
				losSensor = GetComponent<LOSSensor>();
			}

			squadMember = GetComponent<EnemySquadMember>();
		}

		// Assigns the LOS sensor used for visibility checks
		public void SetSensor(LOSSensor sensor) {
			losSensor = sensor;
		}

		// Sets a specific target to track directly
		public void SetExplicitTarget(Transform target) {
			explicitTarget = target;
		}

		// Clears the manually assigned target
		public void ClearTarget() {
			explicitTarget = null;
		}

		// Resets runtime-only state for pooling or respawning
		// Keep explicitTarget assigned if set in inspector
		public void ResetRuntime() {
			nextSightingShareTime = 0.0f;
		}

		// Finds the target immediately and writes initial perception data to the blackboard
		// Useful when an enemy spawns or is reused from a pool
		public void AcquireTargetImmediately(Transform owner, EnemyBlackboard blackboard) {
			if (blackboard == null) {
				return;
			}

			// Find the player automatically if no target has been assigned
			if (explicitTarget == null && autoAcquirePlayerByTag) {
				TryFindPlayer();
			}

			blackboard.SetTarget(explicitTarget);

			if (explicitTarget == null || owner == null) {
				return;
			}

			// Store distance so BT conditions can check ranges without recalculating
			float distance = Vector3.Distance(owner.position, explicitTarget.position);
			blackboard.SetDistance(distance);

			// Check LOS using the sensor
			bool hasLOS = allowTargetWithoutSensor || losSensor == null ? allowTargetWithoutSensor : losSensor.HasLOS(explicitTarget);
			blackboard.SetLineOfSight(hasLOS);

			// A confirmed sighting refreshes visual memory and can be shared with the squad
			if (hasLOS) {
				RecordConfirmedSighting(blackboard, explicitTarget.position);
			}
		}

		// Updates target + distance + line of sight + memory-based target retention every AI tick
		public void Tick(Transform owner, EnemyBlackboard blackboard) {
			if (blackboard == null) {
				return;
			}

			// Reacquire the player if the current target is missing
			if (explicitTarget == null && autoAcquirePlayerByTag) {
				TryFindPlayer();
			}

			blackboard.SetTarget(explicitTarget);

			// If no target exists then clear active target data
			if (explicitTarget == null || owner == null) {
				blackboard.SetHasTarget(false);
				blackboard.SetLineOfSight(false);
				blackboard.SetDistance(Mathf.Infinity);
				return;
			}

			// Update distance for range-based BT conditions
			float distance = Vector3.Distance(owner.position, explicitTarget.position);
			blackboard.SetDistance(distance);

			// Confirm whether the target is visible this tick
			bool hasLOS = allowTargetWithoutSensor;
			if (losSensor != null) {
				hasLOS = losSensor.HasLOS(explicitTarget);
			}

			blackboard.SetLineOfSight(hasLOS);

			// Seeing the target updates last-seen position and velocity estimate
			if (hasLOS) {
				RecordConfirmedSighting(blackboard, explicitTarget.position);
			}

			// Keep the target active briefly after LOS is lost so enemies can investigate
			bool hasFreshMemory = blackboard.HasLastSeenPosition && (Time.time - blackboard.LastSeenTime) <= targetMemoryDuration;
			blackboard.SetHasTarget(hasLOS || hasFreshMemory);
		}

		// Stores confirmed visual information and optionally shares it with squadmates
		private void RecordConfirmedSighting(EnemyBlackboard blackboard, Vector3 targetPosition) {
			blackboard.SetLastSeenPosition(targetPosition);
			blackboard.SetObservedTargetPosition(targetPosition);

			// Limit squad reports so enemies do not spam shared awareness every frame
			if (shareSightingsWithSquad && squadMember != null && Time.time >= nextSightingShareTime) {
				squadMember.ReportPlayerSighting(CurrentTarget, targetPosition, blackboard.ObservedTargetVelocity);
				nextSightingShareTime = Time.time + sightingShareInterval;
			}
		}

		// Attempts to find the player using the configured tag and assign it as the current target
		private void TryFindPlayer() {
			GameObject player = GameObject.FindGameObjectWithTag(playerTag);
			explicitTarget = player != null ? player.transform : null;
		}
	}
}
