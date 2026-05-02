using UnityEngine;
using UnityEngine.AI;

namespace Game.AI {
	// Fair route prediction helper
	// Uses only observed/shared player memory + reaction delay + cooldowns +designer-placed TacticalAnchor points placed in the scene
	[DisallowMultipleComponent]
	[RequireComponent(typeof(EnemyBlackboard))]
	public sealed class EnemyRoutePredictor : MonoBehaviour {
		[Header("Fairness")]
		[Tooltip("Maximum age of visual/shared sighting usable for route prediction.")]
		[SerializeField] private float maxSightingAge = 3.0f;
		[Tooltip("Delay after sighting before an intercept can be chosen. Prevents instant psychic reactions.")]
		[SerializeField] private float reactionDelay = 0.45f;
		[Tooltip("Minimum target speed needed before true interception is allowed.")]
		[SerializeField] private float minimumVelocityForIntercept = 2.0f;
		[Tooltip("Cooldown after committing an intercept.")]
		[SerializeField] private float interceptCooldown = 4.0f;

		[Header("Prediction")]
		[Tooltip("Time (in seconds) ahead to predict the observed target velocity.")]
		[SerializeField] private float predictionTime = 1.15f;
		[Tooltip("Extra uncertainty radius added per second since the last sighting.")]
		[SerializeField] private float uncertaintyGrowthPerSecond = 1.25f;
		[Tooltip("Maximum uncertainty radius allowed.")]
		[SerializeField] private float maxUncertaintyRadius = 4.0f;
		[Tooltip("How far from the prediction an anchor can be and still count.")]
		[SerializeField] private float maxAnchorDistanceFromPrediction = 9.0f;
		[Tooltip("Minimum distance from this enemy to a selected anchor.")]
		[SerializeField] private float minAnchorDistanceFromEnemy = 2.5f;

		[Header("Anchor Scoring")]
		[Tooltip("How much interceptors prefer doors, ramps, traversal exits, and chokepoints.")]
		[SerializeField] private float interceptorTraversalAnchorBias = 3.0f;
		[Tooltip("How much flankers prefer arena side-lane anchors.")]
		[SerializeField] private float flankerSideLaneBias = 2.5f;
		[Tooltip("How much suppressors and anchors prefer cover or chokepoint anchors.")]
		[SerializeField] private float suppressorCoverBias = 2.0f;
		[Tooltip("Small weight applied to distance from this enemy to the anchor.")]
		[SerializeField] private float enemyDistanceScoreWeight = 0.15f;

		[Header("Navigation")]
		[Tooltip("If true, ground enemies validate anchors against the NavMesh before using them.")]
		[SerializeField] private bool validateGroundNavMesh = true;
		[Tooltip("How far from an anchor to search for a valid NavMesh point.")]
		[SerializeField] private float navMeshSampleRadius = 2.0f;

		[Header("Fallbacks")]
		[Tooltip("Distance used when no suitable anchor exists and a flanker needs an offset destination.")]
		[SerializeField] private float fallbackFlankDistance = 6.0f;
		[Tooltip("Distance used when no suitable anchor exists and an anchor/blocker needs to hold near memory.")]
		[SerializeField] private float fallbackAnchorBackoff = 3.5f;

		// Shared perception/memory data for this enemy
		private EnemyBlackboard blackboard;

		// Reused path object to avoid allocating a new NavMeshPath every anchor check
		private NavMeshPath reusablePath;

		private void Awake() {
			blackboard = GetComponent<EnemyBlackboard>();
			reusablePath = new NavMeshPath();
		}

		public bool CanAttemptIntercept() {
			// Cannot predict if there is no usable last-seen memory
			if (blackboard == null || blackboard.HasLastSeenPosition == false) {
				return false;
			}

			// Interception is only allowed after a small reaction delay and before memory becomes too redundant
			float sightingAge = Time.time - blackboard.LastSeenTime;
			if (sightingAge > maxSightingAge || sightingAge < reactionDelay) {
				return false;
			}

			// Prevents the same enemy from constantly re-predicting and cutting the player off
			if ((Time.time - blackboard.LastInterceptTime) < interceptCooldown) {
				return false;
			}

			// Only intercept if the player was moving fast enough to make prediction believable
			return blackboard.ObservedTargetVelocity.magnitude >= minimumVelocityForIntercept;
		}

		public bool TryBuildDestination(EnemyTacticalRole role, out Vector3 destination) {
			// Default to current position to ensure that callers always receive a safe value
			destination = transform.position;

			// No memory means there is nothing useful to predict from
			if (blackboard == null || blackboard.HasLastSeenPosition == false) {
				return false;
			}

			// Interceptors use stricter fairness rules than other roles
			if (role == EnemyTacticalRole.Interceptor && CanAttemptIntercept() == false) {
				return false;
			}

			// First try to use level placed anchor points near the predicted player route
			Vector3 predictedPosition = GetPredictedPosition();
			TacticalAnchor bestAnchor = FindBestAnchor(role, predictedPosition, out Vector3 anchorDestination);
			if (bestAnchor != null) {
				destination = anchorDestination;

				// Temporarily claim the anchor so multiple enemies do not stack on the same point
				bestAnchor.Claim();

				// Start intercept cooldown once the enemy commits to an intercept
				if (role == EnemyTacticalRole.Interceptor) {
					blackboard.MarkInterceptCommitted();
				}

				return true;
			}

			// If no good anchor exists, fall back to a simple role-based offset
			if (TryBuildFallback(role, predictedPosition, out destination)) {
				if (role == EnemyTacticalRole.Interceptor) {
					blackboard.MarkInterceptCommitted();
				}

				return true;
			}

			return false;
		}

		public Vector3 GetPredictedPosition() {
			// Without memory, use this enemy's position as a fallback
			if (blackboard == null || blackboard.HasLastSeenPosition == false) {
				return transform.position;
			}

			// Predict from last known position and observed velocity
			float age = Mathf.Max(0.0f, Time.time - blackboard.LastSeenTime);
			Vector3 predicted = blackboard.LastSeenPosition + blackboard.ObservedTargetVelocity * (predictionTime + age * 0.25f);

			// Add uncertainty as memory gets older so enemies do not feel perfectly accurate
			float uncertainty = Mathf.Min(maxUncertaintyRadius, age * uncertaintyGrowthPerSecond);
			if (uncertainty > 0.01f) {
				Vector2 randomOffset = Random.insideUnitCircle * uncertainty;
				predicted += new Vector3(randomOffset.x, 0.0f, randomOffset.y);
			}

			return predicted;
		}

		private TacticalAnchor FindBestAnchor(EnemyTacticalRole role, Vector3 predictedPosition, out Vector3 destination) {
			destination = Vector3.zero;
			TacticalAnchor best = null;
			float bestScore = Mathf.Infinity;

			// Check every active tactical anchor in the scene
			for (int i = 0; i < TacticalAnchor.Anchors.Count; i++) {
				TacticalAnchor anchor = TacticalAnchor.Anchors[i];

				// Skip unusable, claimed, or role-incompatible anchors
				if (anchor == null || anchor.isActiveAndEnabled == false || anchor.IsClaimed || anchor.AllowsRole(role) == false) {
					continue;
				}

				// Anchor must be close enough to the predicted player route
				float distanceToPrediction = Vector3.Distance(anchor.Position, predictedPosition);
				if (distanceToPrediction > maxAnchorDistanceFromPrediction) {
					continue;
				}

				// Avoid selecting anchors that are basically already under this enemy
				float distanceFromEnemy = Vector3.Distance(transform.position, anchor.Position);
				if (distanceFromEnemy < minAnchorDistanceFromEnemy) {
					continue;
				}

				//Vector3 candidate = anchor.Position; // OLD: Go to anchor centre

				// Go to a point within the anchor radius, biased toward the predicted player path
				Vector3 candidate = anchor.GetBiasedPoint(predictedPosition);

				// Ground enemies should only use anchors they can actually path to
				if (validateGroundNavMesh && GetComponent<GroundEnemyMotor>() != null) {
					if (TrySampleGroundDestination(anchor.Position, out candidate) == false) {
						continue;
					}

					if (NavMesh.CalculatePath(transform.position, candidate, NavMesh.AllAreas, reusablePath) == false || reusablePath.status == NavMeshPathStatus.PathInvalid) {
						continue;
					}
				}

				// Lower score is better
				// Priority and role bias make useful anchors more attractive
				float roleBias = GetRoleAnchorBias(role, anchor.AnchorType);
				float score = distanceToPrediction + distanceFromEnemy * enemyDistanceScoreWeight - anchor.Priority - roleBias;

				if (score < bestScore) {
					bestScore = score;
					best = anchor;
					destination = candidate;
				}
			}

			return best;
		}

		private bool TryBuildFallback(EnemyTacticalRole role, Vector3 predictedPosition, out Vector3 destination) {
			destination = predictedPosition;

			// Direction from enemy to predicted player position
			Vector3 toPrediction = predictedPosition - transform.position;
			toPrediction.y = 0.0f;

			// If the enemy is already at the prediction, use forward as the direction
			if (toPrediction.sqrMagnitude < 0.001f) {
				toPrediction = transform.forward;
			}

			// Side vector used for flank fallback positions
			Vector3 side = Vector3.Cross(Vector3.up, toPrediction.normalized);
			if (Random.value < 0.5f) {
				side = -side;
			}

			// Build a simple fallback destination based on the assigned role
			switch (role) {
				case EnemyTacticalRole.Flanker:
					destination = predictedPosition + side * fallbackFlankDistance;
					break;
				case EnemyTacticalRole.Anchor:
				case EnemyTacticalRole.Suppressor:
					destination = predictedPosition - toPrediction.normalized * fallbackAnchorBackoff;
					break;
				case EnemyTacticalRole.Interceptor:
					destination = predictedPosition;
					break;
				default:
					return false;
			}

			// Ground enemies should snap fallback destinations to the NavMesh
			if (validateGroundNavMesh && GetComponent<GroundEnemyMotor>() != null) {
				return TrySampleGroundDestination(destination, out destination);
			}

			return true;
		}

		private bool TrySampleGroundDestination(Vector3 desired, out Vector3 sampled) {
			// Find the nearest valid NavMesh point to the desired destination
			if (NavMesh.SamplePosition(desired, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas)) {
				sampled = hit.position;
				return true;
			}

			// Return the original point (useful for debugging since it will still show what was attempted)
			sampled = desired;
			return false;
		}

		private float GetRoleAnchorBias(EnemyTacticalRole role, TacticalAnchorType type) {
			// Interceptors prefer chokepoints and traversal exits because those are believable cutoff points
			if (role == EnemyTacticalRole.Interceptor) {
				if ((type & TacticalAnchorType.Door) != 0 ||
					(type & TacticalAnchorType.Ramp) != 0 ||
					(type & TacticalAnchorType.GrappleLanding) != 0 ||
					(type & TacticalAnchorType.WallRunExit) != 0 ||
					(type & TacticalAnchorType.RailExit) != 0 ||
					(type & TacticalAnchorType.PlatformChokepoint) != 0) {
					return interceptorTraversalAnchorBias;
				}
			}

			// Flankers prefer side lanes because they create pressure without directly chasing
			if (role == EnemyTacticalRole.Flanker && (type & TacticalAnchorType.ArenaSideLane) != 0) {
				return flankerSideLaneBias;
			}

			// Suppressors and anchors prefer cover and blocking points
			if ((role == EnemyTacticalRole.Suppressor || role == EnemyTacticalRole.Anchor) &&
				((type & TacticalAnchorType.Cover) != 0 || (type & TacticalAnchorType.PlatformChokepoint) != 0)) {
				return suppressorCoverBias;
			}

			// No extra preference
			return 0.0f;
		}
	}
}