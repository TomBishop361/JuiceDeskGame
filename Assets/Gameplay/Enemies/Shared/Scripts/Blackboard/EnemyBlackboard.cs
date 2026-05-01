using UnityEngine;

namespace Game.AI {
	// Stores all runtime AI memory for one enemy
	// BT's + perception + hearing + squad systems read/write here
	[DisallowMultipleComponent]
	public sealed class EnemyBlackboard : MonoBehaviour {
		[Header("Target")]
		[Tooltip("Current target the enemy is tracking.")]
		[field: SerializeField] public Transform Target { get; private set; }
		[Tooltip("True when the enemy currently has a valid target reference or fresh target memory.")]
		[field: SerializeField] public bool HasTarget { get; private set; }
		[Tooltip("True when the enemy currently has line of sight to its target.")]
		[field: SerializeField] public bool HasLineOfSight { get; private set; }
		[Tooltip("Distance from this enemy to its current target.")]
		[field: SerializeField] public float DistanceToTarget { get; private set; } = Mathf.Infinity;

		[Header("Visual Memory")]
		[Tooltip("The last time the enemy had confirmed or shared knowledge of the target.")]
		[field: SerializeField] public float LastSeenTime { get; private set; } = -Mathf.Infinity;
		[Tooltip("Last confirmed or shared world position where the target was seen.")]
		[field: SerializeField] public Vector3 LastSeenPosition { get; private set; }
		[Tooltip("True when a valid last-seen position is available for investigate/search movement.")]
		[field: SerializeField] public bool HasLastSeenPosition { get; private set; }
		[Tooltip("Estimated target velocity from recent confirmed/shared sightings.")]
		[field: SerializeField] public Vector3 ObservedTargetVelocity { get; private set; }
		[Tooltip("Last observed position used for velocity estimation.")]
		[field: SerializeField] public Vector3 LastObservedTargetPosition { get; private set; }
		[Tooltip("True after at least one observed position has been recorded.")]
		[field: SerializeField] public bool HasObservedTargetPosition { get; private set; }
		[Tooltip("Last time the observed target position was updated.")]
		[field: SerializeField] public float LastObservedTargetTime { get; private set; } = -Mathf.Infinity;
		[Tooltip("Last time this enemy received shared visual awareness from squadmates.")]
		[field: SerializeField] public float LastSharedAwarenessTime { get; private set; } = -Mathf.Infinity;

		[Header("Suspicion / Hearing")]
		[Tooltip("Last suspicious noise position heard by this enemy or shared by a squadmate.")]
		[field: SerializeField] public Vector3 SuspiciousNoisePosition { get; private set; }
		[Tooltip("True when a valid suspicious noise position is available.")]
		[field: SerializeField] public bool HasSuspiciousNoise { get; private set; }
		[Tooltip("Last time a suspicious noise was heard or shared.")]
		[field: SerializeField] public float LastHeardNoiseTime { get; private set; } = -Mathf.Infinity;
		[Tooltip("Radius of the latest heard/shared noise.")]
		[field: SerializeField] public float LastHeardNoiseRadius { get; private set; }
		[Tooltip("Kind of the latest heard/shared noise.")]
		[field: SerializeField] public AINoiseKind LastHeardNoiseKind { get; private set; }
		[Tooltip("True if the latest suspicious noise came from a squadmate instead of this enemy's own hearing sensor.")]
		[field: SerializeField] public bool LastNoiseWasShared { get; private set; }

		[Header("Tactical Coordination")]
		[Tooltip("Current role assigned by the squad director.")]
		[field: SerializeField] public EnemyTacticalRole TacticalRole { get; private set; } = EnemyTacticalRole.None;
		[Tooltip("Time when the current role was assigned.")]
		[field: SerializeField] public float RoleAssignedTime { get; private set; } = -Mathf.Infinity;
		[Tooltip("Current tactical movement destination chosen by BT actions.")]
		[field: SerializeField] public Vector3 TacticalDestination { get; private set; }
		[Tooltip("True when a tactical movement destination is valid.")]
		[field: SerializeField] public bool HasTacticalDestination { get; private set; }
		[Tooltip("Last time this enemy committed to an intercept destination. Used as an anti-psychic fairness cooldown.")]
		[field: SerializeField] public float LastInterceptTime { get; private set; } = -Mathf.Infinity;

		[Header("Finite State")]
		[Tooltip("True once the enemy has entered its death state.")]
		[field: SerializeField] public bool IsDead { get; private set; }
		[Tooltip("True when this enemy is temporarily disabled and should not act.")]
		[field: SerializeField] public bool IsDisabled { get; private set; }

		// Assigns or clears the target reference
		// If there is no target and no memory, the enemy fully loses awareness
		public void SetTarget(Transform target) {
			Target = target;

			if (target == null && HasLastSeenPosition == false) {
				HasTarget = false;
				DistanceToTarget = Mathf.Infinity;
				HasLineOfSight = false;
			}
			else if (target != null) {
				HasTarget = true;
			}
		}

		// Stores the latest distance to the current target
		public void SetDistance(float distance) {
			DistanceToTarget = distance;
		}

		// Stores whether the enemy can currently see the target
		// A valid sighting also refreshes the last-seen time
		public void SetLineOfSight(bool hasLineOfSight) {
			HasLineOfSight = hasLineOfSight;
			if (hasLineOfSight) {
				LastSeenTime = Time.time;
			}
		}

		// Saves the last known target position for searching and intercepting
		public void SetLastSeenPosition(Vector3 position) {
			LastSeenPosition = position;
			HasLastSeenPosition = true;
			LastSeenTime = Time.time;
		}

		// Clears visual memory when the enemy should forget the target
		public void ClearLastSeenPosition() {
			LastSeenPosition = Vector3.zero;
			HasLastSeenPosition = false;
			LastSeenTime = -Mathf.Infinity;
		}

		// Records a real visual sample and estimates target velocity
		// This keeps prediction fair because it only uses observed/shared data
		public void SetObservedTargetPosition(Vector3 position) {
			float now = Time.time;
			if (HasObservedTargetPosition) {
				float dt = Mathf.Max(0.0001f, now - LastObservedTargetTime);
				Vector3 rawVelocity = (position - LastObservedTargetPosition) / dt;

				// Smooth velocity so prediction is less twitchy
				ObservedTargetVelocity = Vector3.Lerp(ObservedTargetVelocity, rawVelocity, 0.65f);
			}
			else {
				ObservedTargetVelocity = Vector3.zero;
			}

			LastObservedTargetPosition = position;
			LastObservedTargetTime = now;
			HasObservedTargetPosition = true;
		}

		// Applies a squadmate's sighting
		// This gives memory but does not count as direct line of sight
		public void SetSharedSighting(Transform target, Vector3 position, Vector3 velocity, float reportTime) {
			// Ignore older reports so fresh information is not overwritten
			if (reportTime < LastSeenTime) {
				return;
			}

			if (target != null) {
				Target = target;
			}

			HasTarget = Target != null;
			HasLineOfSight = false;
			LastSeenPosition = position;
			HasLastSeenPosition = true;
			LastSeenTime = reportTime;
			LastSharedAwarenessTime = Time.time;
			ObservedTargetVelocity = velocity;
			LastObservedTargetPosition = position;
			LastObservedTargetTime = reportTime;
			HasObservedTargetPosition = true;
		}

		// Returns true if the enemy has recent visual/shared memory
		public bool HasFreshSighting(float maxAge) {
			return HasLastSeenPosition && (Time.time - LastSeenTime) <= maxAge;
		}

		// Stores a suspicious sound for investigation
		public void SetSuspiciousNoise(Vector3 position, float radius, AINoiseKind kind, float time, bool shared) {
			if (time < LastHeardNoiseTime) {
				return;
			}

			SuspiciousNoisePosition = position;
			HasSuspiciousNoise = true;
			LastHeardNoiseTime = time;
			LastHeardNoiseRadius = radius;
			LastHeardNoiseKind = kind;
			LastNoiseWasShared = shared;
		}

		// Clears suspicious sound memory
		public void ClearSuspiciousNoise() {
			SuspiciousNoisePosition = Vector3.zero;
			HasSuspiciousNoise = false;
			LastHeardNoiseTime = -Mathf.Infinity;
			LastHeardNoiseRadius = 0.0f;
			LastHeardNoiseKind = AINoiseKind.Generic;
			LastNoiseWasShared = false;
		}

		// Returns true if the enemy has heard a recent suspicious sound
		public bool HasFreshSuspiciousNoise(float maxAge) {
			return HasSuspiciousNoise && (Time.time - LastHeardNoiseTime) <= maxAge;
		}

		// Sets the current squad role
		// The timestamp helps avoid rapid role swapping
		public void SetTacticalRole(EnemyTacticalRole role) {
			if (TacticalRole == role) {
				return;
			}

			TacticalRole = role;
			RoleAssignedTime = Time.time;
		}

		// Stores a destination chosen by a tactical BT node
		public void SetTacticalDestination(Vector3 destination) {
			TacticalDestination = destination;
			HasTacticalDestination = true;
		}

		// Clears the current tactical movement destination
		public void ClearTacticalDestination() {
			TacticalDestination = Vector3.zero;
			HasTacticalDestination = false;
		}

		// Records that this enemy has committed to an intercept
		// Used to prevent constant cutoffs that may feel psychic
		public void MarkInterceptCommitted() {
			LastInterceptTime = Time.time;
		}

		// Overrides whether the enemy should act as if it still has a target
		// Useful when memory keeps the target relevant after LOS is lost
		public void SetHasTarget(bool hasTarget) {
			HasTarget = hasTarget;

			if (hasTarget == false && Target == null) {
				DistanceToTarget = Mathf.Infinity;
			}
		}

		// Updates death state for BT/state checks
		public void SetDead(bool isDead) {
			IsDead = isDead;
		}

		// Updates disabled state for BT/state checks
		public void SetDisabled(bool isDisabled) {
			IsDisabled = isDisabled;
		}

		// Clears all runtime memory so the enemy can be respawned or reused safely
		public void ResetRuntime() {
			Target = null;
			HasTarget = false;
			HasLineOfSight = false;
			DistanceToTarget = Mathf.Infinity;

			LastSeenTime = -Mathf.Infinity;
			LastSeenPosition = Vector3.zero;
			HasLastSeenPosition = false;
			ObservedTargetVelocity = Vector3.zero;
			LastObservedTargetPosition = Vector3.zero;
			HasObservedTargetPosition = false;
			LastObservedTargetTime = -Mathf.Infinity;
			LastSharedAwarenessTime = -Mathf.Infinity;

			ClearSuspiciousNoise();

			TacticalRole = EnemyTacticalRole.None;
			RoleAssignedTime = -Mathf.Infinity;
			ClearTacticalDestination();
			LastInterceptTime = -Mathf.Infinity;

			IsDead = false;
			IsDisabled = false;
		}
	}
}