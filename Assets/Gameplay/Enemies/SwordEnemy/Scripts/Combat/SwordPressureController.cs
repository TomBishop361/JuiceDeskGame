using Game.AI.Sword;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI {
	// Code-driven movement/attack pressure for the sword enemy
	// This pressure controller now replaces BT movement/attack actions and conditions for sword enemies
	// Sword movement is prediction-driven while LOS exists, and memory-driven when LOS breaks
	// Attacks still require LOS, facing, cooldown, and a token
	[DisallowMultipleComponent]
	[RequireComponent(typeof(SwordEnemy))]
	[RequireComponent(typeof(GroundEnemyMotor))]
	[RequireComponent(typeof(EnemyPressureAgent))]
	[RequireComponent(typeof(EnemyAwarenessReporter))]
	public sealed class SwordPressureController : MonoBehaviour {
		[Header("Movement")]
		[Tooltip("How often this controller recalculates its NavMesh destination.")]
		[SerializeField] private float destinationRefreshInterval = 0.12f;
		[Tooltip("Horizontal offset used by sword enemies assigned to side pressure.")]
		[SerializeField] private float sidePressureDistance = 3.25f;
		[Tooltip("Extra distance placed ahead of the predicted player position for cutoff sword enemies.")]
		[SerializeField] private float cutOffExtraDistance = 1.5f;
		[Tooltip("Radius used to snap pressure destinations onto the NavMesh.")]
		[SerializeField] private float navMeshSampleRadius = 2.0f;
		[Tooltip("Radius used to detect nearby ground enemies and reduce clumping.")]
		[SerializeField] private float localSpacingRadius = 1.6f;
		[Tooltip("Strength of the small local offset used to separate clustered ground enemies.")]
		[SerializeField] private float localSpacingPush = 1.0f;
		[Tooltip("Distance at which a remembered/spawn awareness destination is considered reached.")]
		[SerializeField] private float reachedKnownPositionDistance = 1.35f;

		[Header("Attack Fairness")]
		[Tooltip("How often this controller checks if it may start a swing/lunge.")]
		[SerializeField] private float attackCheckInterval = 0.10f;
		[Tooltip("Maximum horizontal angle allowed for a sword swing. Higher values make melee more forgiving once the enemy reaches the player.")]
		[SerializeField] private float startMeleeSwingFacingAngle = 95.0f;
		[Tooltip("Maximum vertical height difference allowed for a basic sword swing. Prevents swords hitting through floors or attacking players far above/below them.")]
		[SerializeField] private float meleeSwingMaxVerticalDifference = 2.25f;
		[Tooltip("Minimum continuous visible time before a sword can lunge. Prevents instant psychic lunges.")]
		[SerializeField] private float minVisibleTimeBeforeLunge = 0.25f;
		[Tooltip("Maximum angle to the target before lunge is allowed. Lower is stricter and more readable.")]
		[SerializeField] private float startLungeFacingAngle = 48.0f;
		[Tooltip("Random horizontal offset added to predicted lunge aim so lunges are threatening but not perfect.")]
		[SerializeField] private float lungePredictionRadius = 0.65f;
		[Tooltip("How long a sword swing token is held after starting a swing.")]
		[SerializeField] private float swordSwingTokenTime = 0.70f;
		[Tooltip("How long a sword lunge token is held after starting a lunge.")]
		[SerializeField] private float swordLungeTokenTime = 1.20f;

		[Header("Control")]
		[Tooltip("If true, calls the existing enemy AcquireTarget when no target is assigned.")]
		[SerializeField] private bool autoAcquireTarget = true;
		[Tooltip("If true, this pressure controller starts attacks. Disable if a BT/action owns attacks.")]
		[SerializeField] private bool driveAttacks = true;
		[Tooltip("If true, this pressure controller moves the enemy. Disable if another system owns movement.")]
		[SerializeField] private bool driveMovement = true;
		

		private SwordEnemy sword;
		private GroundEnemyMotor groundMotor;
		private EnemyPressureAgent pressureAgent;

		// Cached active pressure director
		// This can change at runtime, so Update() refreshes it from the singleton
		private CombatPressureDirector director;

		// Time gates used to avoid recalculating destinations/attack checks every frame
		private float nextDestinationRefreshTime = -Mathf.Infinity;
		private float nextAttackCheckTime = -Mathf.Infinity;

		// Tracks when the target first became continuously visible
		// Used to prevent instant lunges immediately after regaining LOS
		private float visibleSince = -Mathf.Infinity;

		// Last movement destination sent to the ground motor
		private Vector3 currentDestination;
		private bool hasDestination;

		// Temporary fallback destination used when this enemy spawns after the player was already spotted
		private bool hasSpawnAwarenessDestination;
		private Vector3 spawnAwarenessDestination;

		private void Awake() {
			sword = GetComponent<SwordEnemy>();
			groundMotor = GetComponent<GroundEnemyMotor>();
			pressureAgent = GetComponent<EnemyPressureAgent>();
		}

		// Newly spawned/enabled enemies inherit recent player awareness so they can immediately
		// path toward the known fight location instead of waiting idle for direct line of sight
		private void OnEnable() {
			EnemyAwarenessHub hub = EnemyAwarenessHub.Active;
			if (hub != null && hub.TryGetKnownPositionForSpawn(out Vector3 position)) {
				hasSpawnAwarenessDestination = true;
				spawnAwarenessDestination = position;
			}
		}

		private void Update() {
			// The active pressure director may be created/destroyed with encounter state, so refresh it
			director = CombatPressureDirector.Active;

			if (sword == null || groundMotor == null || pressureAgent == null) {
				return;
			}

			if (autoAcquireTarget && sword.HasTarget == false) {
				sword.AcquireTarget();
			}

			if (sword.IsDead || sword.IsStunned) {
				groundMotor.Stop();
				return;
			}

			bool hasTarget = sword.HasTarget && sword.Target != null;
			bool hasAwareness = EnemyAwarenessHub.Active != null && EnemyAwarenessHub.Active.HasKnownPosition;

			// With no target and no known player position, there is nothing meaningful to chase
			if (hasTarget == false && hasAwareness == false && hasSpawnAwarenessDestination == false) {
				groundMotor.Stop();
				return;
			}

			UpdateVisibilityTimer();

			// Attack animation/control owns movement while active
			if (sword.IsAttacking) {
				return;
			}

			// Limit attack checks for predictable behaviour and readable decision-making
			if (driveAttacks && Time.time >= nextAttackCheckTime) {
				TickAttacks();
				nextAttackCheckTime = Time.time + Mathf.Max(0.03f, attackCheckInterval);
			}

			if (driveMovement) {
				TickMovement();
			}
		}

		private void UpdateVisibilityTimer() {
			// Start timing only after continuous LOS is established
			if (sword.HasTarget && sword.HasLineOfSight) {
				if (visibleSince <= 0.0f || visibleSince == -Mathf.Infinity) {
					visibleSince = Time.time;
				}
			}
			else {
				// Reset as soon as visibility breaks so the lunge delay is applied again next time
				visibleSince = -Mathf.Infinity;
			}
		}

		// Attacks are intentionally stricter than movement: require LOS + cooldown/range/token checks
		private void TickAttacks() {
			if (sword.HasTarget == false || sword.Target == null) {
				return;
			}

			if (sword.IsAttacking || sword.IsStunned || sword.IsDead) {
				return;
			}

			// Prefer a close-range swing when available even if the normal LOS ray is unreliable on
			// platforms, ramps or when other enemies are nearby
			// Tokens prevent too many swords from attacking at once and keep pressure fair/readable
			if (sword.InSwingRange && sword.CanSwing && CanMeleeSwingAtPlayer()) {
				if (pressureAgent.TryRequestAttackToken(PressureAttackTokenType.SwordSwing, swordSwingTokenTime)) {
					if (sword.TryStartSwing() == false) {
						// Release the token if the attack start failed so another enemy can use it
						pressureAgent.ReleaseAttackToken(PressureAttackTokenType.SwordSwing);
					}
				}

				return;
			}

			// Lunges stay stricter because psychic lunges through geometry would feel unfair
			if (sword.HasLineOfSight == false) {
				return;
			}

			// Lunges require their own range/cooldown checks before fairness gates are evaluated
			if (sword.InLungeRange == false || sword.CanLunge == false) {
				return;
			}

			// Require a small amount of continuous visibility to avoid unfair instant lunges
			if (Time.time - visibleSince < minVisibleTimeBeforeLunge) {
				return;
			}

			// Require readable facing before starting the lunge
			if (IsFacingTarget(startLungeFacingAngle) == false) {
				return;
			}

			if (pressureAgent.TryRequestAttackToken(PressureAttackTokenType.SwordLunge, swordLungeTokenTime) == false) {
				return;
			}

			Vector3 aimPoint = BuildPressureLungeAimPoint();
			if (sword.TryStartPressureLunge(aimPoint) == false) {
				// Return the token if the lunge did not actually begin
				pressureAgent.ReleaseAttackToken(PressureAttackTokenType.SwordLunge);
			}
		}

		private bool CanMeleeSwingAtPlayer() {
			if (sword.Target == null) {
				return false;
			}

			Vector3 toTarget = sword.Target.position - transform.position;

			float verticalDifference = Mathf.Abs(toTarget.y);
			if (verticalDifference > meleeSwingMaxVerticalDifference) {
				return false;
			}

			Vector3 flatToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
			if (flatToTarget.sqrMagnitude < 0.0001f) {
				return true;
			}

			Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
			if (flatForward.sqrMagnitude < 0.0001f) {
				return false;
			}

			float angle = Vector3.Angle(flatForward.normalized, flatToTarget.normalized);

			// Wider than lunge facing angle because melee swings should be forgiving once the enemy has successfully reached the player
			return angle <= startMeleeSwingFacingAngle;
		}

		// Movement is pressure/memory driven
		// LOS is not required for movement around corners
		private void TickMovement() {
			// Only stop to swing if the enemy really has LOS
			// If LOS is false, keep pathing to last known position
			if (sword.HasTarget && sword.Target != null && sword.InSwingRange && CanMeleeSwingAtPlayer()) {
				groundMotor.Stop();
				sword.FaceTarget(sword.Target.position);
				return;
			}

			// Rebuild destinations at a fixed interval to avoid random frame-by-frame NavMesh updates
			if (Time.time >= nextDestinationRefreshTime || hasDestination == false) {
				currentDestination = BuildPressureDestination();
				currentDestination += BuildLocalSpacingOffset();
				TrySampleNavMesh(currentDestination, out currentDestination);
				hasDestination = true;
				nextDestinationRefreshTime = Time.time + Mathf.Max(0.03f, destinationRefreshInterval);
			}

			groundMotor.Chase(currentDestination);
			sword.FaceTarget(GetFacePoint());
		}

		// Builds the sword destination from live prediction when visible, otherwise from last known awareness
		// This lets swords flank/cut off while visible and pursue around corners after sight is broken
		private Vector3 BuildPressureDestination() {
			PlayerMotionPredictor predictor = director != null ? director.PlayerPredictor : PlayerMotionPredictor.Active;
			EnemyAwarenessHub awareness = EnemyAwarenessHub.Active;

			bool canUseLivePrediction = sword.HasTarget && sword.Target != null && sword.HasLineOfSight && predictor != null;
			if (canUseLivePrediction) {
				// Live sight overrides spawn awareness because the current target state is fresher
				hasSpawnAwarenessDestination = false;

				Vector3 moveDir = predictor.PlanarMoveDirection;
				if (moveDir.sqrMagnitude < 0.0001f) {
					// If the player is not moving, use their facing direction as a stable prediction location
					moveDir = Vector3.ProjectOnPlane(sword.Target.forward, Vector3.up).normalized;
				}

				// Alternate side-pressure enemies left/right and push later slots farther out to reduce stacking
				Vector3 side = Vector3.Cross(Vector3.up, moveDir).normalized;
				int sideSign = pressureAgent.SlotIndex % 2 == 0 ? 1 : -1;
				float ringMultiplier = 1.0f + Mathf.Floor(pressureAgent.SlotIndex / 2.0f) * 0.35f;

				switch (pressureAgent.CurrentJob) {
					case EnemyPressureJob.CutOff:
						// Move ahead of the predicted path to intercept the player
						return predictor.MediumFuturePosition + moveDir * cutOffExtraDistance;

					case EnemyPressureJob.SidePressure:
						// Move to one side of the predicted path to create flanking pressure
						return predictor.ShortFuturePosition + side * (sidePressureDistance * ringMultiplier * sideSign);

					case EnemyPressureJob.DirectPressure:
					default:
						// Default behaviour: pressure the player's near-future position directly
						return predictor.ShortFuturePosition;
				}
			}

			// Use the one-time spawn awareness destination until it is reached, then clear it
			if (hasSpawnAwarenessDestination) {
				if (Vector3.Distance(transform.position, spawnAwarenessDestination) > reachedKnownPositionDistance) {
					return spawnAwarenessDestination;
				}

				hasSpawnAwarenessDestination = false;
			}

			// Fall back to global awareness, this is usually the last known player position reported by enemies
			if (awareness != null && awareness.TryGetKnownPosition(out Vector3 knownPosition)) {
				return knownPosition;
			}

			// Final fallback keeps the pressure controller safe if awareness disappears mid-frame
			return sword.Target != null ? sword.Target.position : transform.position;
		}

		// Aim lunges at short-term prediction, then add a small horizontal random offset so
		// attacks feel threatening without being perfectly accurate every time
		private Vector3 BuildPressureLungeAimPoint() {
			PlayerMotionPredictor predictor = director != null ? director.PlayerPredictor : PlayerMotionPredictor.Active;
			Vector3 aim = predictor != null ? predictor.ShortFuturePosition : sword.Target.position;

			Vector2 noise = Random.insideUnitCircle * lungePredictionRadius;
			aim += new Vector3(noise.x, 0.0f, noise.y);
			return aim;
		}

		private Vector3 GetFacePoint() {
			// Prefer facing toward the same predictive/awareness point the sword enemy is chasing
			PlayerMotionPredictor predictor = director != null ? director.PlayerPredictor : PlayerMotionPredictor.Active;
			if (sword.HasTarget && sword.Target != null && sword.HasLineOfSight && predictor != null) {
				return predictor.ShortFuturePosition;
			}

			if (EnemyAwarenessHub.Active != null && EnemyAwarenessHub.Active.TryGetKnownPosition(out Vector3 knownPosition)) {
				return knownPosition;
			}

			if (hasSpawnAwarenessDestination) {
				return spawnAwarenessDestination;
			}

			return sword.Target != null ? sword.Target.position : currentDestination;
		}

		private Vector3 BuildLocalSpacingOffset() {
			if (director == null || localSpacingRadius <= 0.0f) {
				return Vector3.zero;
			}

			Vector3 push = Vector3.zero;
			IReadOnlyList<EnemyPressureAgent> agents = director.Agents;
			for (int i = 0; i < agents.Count; i++) {
				EnemyPressureAgent other = agents[i];
				if (other == null || other == pressureAgent || other.IsAliveAndEnabled == false) {
					continue;
				}

				// Only use other grounded pressure agents for spacing (ranged/flying enemies are ignored here)
				if (other.Kind != EnemyPressureKind.Sword && other.Kind != EnemyPressureKind.Shield) {
					continue;
				}

				Vector3 away = transform.position - other.transform.position;
				away.y = 0.0f;
				float sqrDistance = away.sqrMagnitude;
				if (sqrDistance < 0.0001f || sqrDistance > localSpacingRadius * localSpacingRadius) {
					continue;
				}

				// Nearby agents push more strongly than agents near the edge of the spacing radius
				float distance = Mathf.Sqrt(sqrDistance);
				float strength = 1.0f - Mathf.Clamp01(distance / localSpacingRadius);
				push += away.normalized * (strength * localSpacingPush);
			}

			return push;
		}

		// Returns true only when the sword enemy is facing the target closely enough to begin a lunge
		// All checks are planar so vertical differences do not affect combat readability
		private bool IsFacingTarget(float maxAngle) {
			if (sword.Target == null) {
				return false;
			}

			Vector3 toTarget = sword.Target.position - transform.position;
			toTarget.y = 0.0f;
			if (toTarget.sqrMagnitude < 0.0001f) {
				return true;
			}

			Vector3 forward = transform.forward;
			forward.y = 0.0f;
			if (forward.sqrMagnitude < 0.0001f) {
				return false;
			}

			return Vector3.Angle(forward.normalized, toTarget.normalized) <= maxAngle;
		}

		// Snaps destinations onto the NavMesh so corner pursuit uses pathfinding instead of wall hugging
		private bool TrySampleNavMesh(Vector3 desired, out Vector3 sampled) {
			if (NavMesh.SamplePosition(desired, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas)) {
				sampled = hit.position;
				return true;
			}

			// If sampling fails, keep the unsampled destination rather than discarding movement entirely
			sampled = desired;
			return false;
		}

		private void OnDrawGizmosSelected() {
			if (hasDestination) {
				Gizmos.DrawWireSphere(currentDestination, 0.25f);
				Gizmos.DrawLine(transform.position, currentDestination);
			}

			if (hasSpawnAwarenessDestination) {
				Gizmos.DrawWireSphere(spawnAwarenessDestination, 0.35f);
			}
		}
	}
}