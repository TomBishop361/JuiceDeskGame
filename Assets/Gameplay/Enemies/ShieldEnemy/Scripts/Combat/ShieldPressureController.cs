using Game.AI.Shield;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI {
	// Code-driven movement/attack pressure for the shield enemy
	// This pressure controller replaces BT movement/attack actions and conditions for shield enemies
	// Movement can use live player prediction while LOS exists, then falls back to remembered
	// awareness positions so the shield enemy can continue pathing around corners
	[DisallowMultipleComponent]
	[RequireComponent(typeof(ShieldEnemy))]
	[RequireComponent(typeof(GroundEnemyMotor))]
	[RequireComponent(typeof(EnemyPressureAgent))]
	[RequireComponent(typeof(EnemyAwarenessReporter))]
	public sealed class ShieldPressureController : MonoBehaviour {
		[Header("Movement")]
		[Tooltip("How often this controller recalculates its NavMesh.")]
		[SerializeField] private float destinationRefreshInterval = 0.18f;
		[Tooltip("Radius used to snap pressure destinations onto the NavMesh.")]
		[SerializeField] private float navMeshSampleRadius = 2.0f;
		[Tooltip("Distance at which a remembered/spawn awareness destination is considered reached.")]
		[SerializeField] private float reachedKnownPositionDistance = 1.75f;

		[Header("Facing")]
		[Tooltip("If true, the shield enemy faces the player whenever direct line of sight exists.")]
		[SerializeField] private bool facePlayerWhileVisible = true;
		[Tooltip("If true, the shield enemy faces its NavMesh desired velocity while pathing without line of sight.")]
		[SerializeField] private bool faceDesiredVelocityWithoutLineOfSight = true;
		[Tooltip("Minimum desired velocity needed before the enemy trusts the NavMesh direction for facing.")]
		[SerializeField] private float movementFaceVelocityThreshold = 0.12f;

		[Header("Attack Tokens")]
		[Tooltip("How long the shield minigun token is refreshed while suppressing.")]
		[SerializeField] private float minigunTokenRefreshTime = 0.35f;
		[Tooltip("How long the shield slam token is held after starting a slam.")]
		[SerializeField] private float slamTokenTime = 1.25f;

		[Header("Control")]
		[Tooltip("If true, calls the existing enemy AcquireTarget when no target is assigned.")]
		[SerializeField] private bool autoAcquireTarget = true;
		[Tooltip("If true, this pressure controller starts attacks. Disable if a BT/action owns attacks.")]
		[SerializeField] private bool driveAttacks = true;
		[Tooltip("If true, this pressure controller moves the enemy. Disable if another system owns movement.")]
		[SerializeField] private bool driveMovement = true;
	
		private ShieldEnemy shield;
		private GroundEnemyMotor groundMotor;
		private EnemyPressureAgent pressureAgent;

		// Cached active pressure director
		// This can change at runtime, so Update() refreshes it from the singleton
		private CombatPressureDirector director;

		// Destination refresh state
		private float nextDestinationRefreshTime = -Mathf.Infinity;
		private Vector3 currentDestination;
		private bool hasDestination;

		// Temporary fallback destination used when this enemy spawns after the player was already spotted
		private bool hasSpawnAwarenessDestination;
		private Vector3 spawnAwarenessDestination;

		private void Awake() {
			shield = GetComponent<ShieldEnemy>();
			groundMotor = GetComponent<GroundEnemyMotor>();
			pressureAgent = GetComponent<EnemyPressureAgent>();
		}

		// Newly spawned/enabled enemies inherit recent player awareness so they can immediately
		// path toward the known fight location instead of waiting idle for direct line of sight
		private void OnEnable() {
			hasDestination = false;
			nextDestinationRefreshTime = -Mathf.Infinity;

			EnemyAwarenessHub hub = EnemyAwarenessHub.Active;
			if (hub != null && hub.TryGetKnownPositionForSpawn(out Vector3 position)) {
				hasSpawnAwarenessDestination = true;
				spawnAwarenessDestination = position;
			}
		}

		private void Update() {
			// The active pressure director may be created/destroyed with encounter state, so refresh it
			director = CombatPressureDirector.Active;

			if (shield == null || groundMotor == null || pressureAgent == null) {
				return;
			}

			if (autoAcquireTarget && shield.HasTarget == false) {
				shield.AcquireTarget();
			}

			if (shield.IsDead || shield.IsStunned) {
				shield.StopMinigunFiring();
				groundMotor.Stop();
				return;
			}

			bool hasTarget = shield.HasTarget && shield.Target != null;
			bool hasAwareness = EnemyAwarenessHub.Active != null && EnemyAwarenessHub.Active.HasKnownPosition;

			// With no target and no known player position, there is nothing meaningful to chase or suppress
			if (hasTarget == false && hasAwareness == false && hasSpawnAwarenessDestination == false) {
				shield.StopMinigunFiring();
				groundMotor.Stop();
				return;
			}

			if (driveAttacks) {
				TickAttacks();
			}

			if (driveMovement) {
				TickMovement();
			}
		}

		// Attacks are intentionally stricter than movement: require LOS plus cooldown/range/token checks
		private void TickAttacks() {
			if (shield.HasTarget == false || shield.Target == null || shield.HasLineOfSight == false) {
				shield.StopMinigunFiring();
				return;
			}

			// Do not fire minigun on top of other attack/exposure states
			if (shield.IsAttacking || shield.IsExposed) {
				shield.StopMinigunFiring();
				return;
			}

			// Prefer a slam when the player is close enough and the slam cooldown is ready
			// Face the player before starting so the slam looks visually correct even if the NavMesh path was turning
			// Tokens prevent too many shield enemies from using high-impact attacks at once
			if (shield.InSlamRange && shield.CanSlam) {
				if (pressureAgent.TryRequestAttackToken(PressureAttackTokenType.ShieldSlam, slamTokenTime)) {
					shield.FaceTarget(shield.Target.position);

					if (shield.TryStartSlam()) {
						return;
					}

					// Return the token if the slam did not actually begin
					pressureAgent.ReleaseAttackToken(PressureAttackTokenType.ShieldSlam);
				}
			}

			// If the shield cannot slam, maintain ranged suppression while the minigun is valid
			// The minigun token is refreshed repeatedly so ownership remains while firing
			if (shield.InFireRange && shield.CanFireMinigun) {
				if (pressureAgent.TryRequestAttackToken(PressureAttackTokenType.ShieldMinigun, minigunTokenRefreshTime)) {
					shield.TryStartMinigun();
				}
				else {
					// Stop if another enemy currently owns the minigun pressure token
					shield.StopMinigunFiring();
				}
			}
			else {
				// Stop firing as soon as range/cooldown conditions are no longer valid
				shield.StopMinigunFiring();
			}
		}

		// Movement is pressure/memory driven
		// LOS is not required for movement around corners
		private void TickMovement() {
			if (shield.IsAttacking || shield.IsExposed) {
				groundMotor.Stop();
				return;
			}

			// While actively suppressing with LOS, hold position and keep facing the player
			if (shield.HasTarget && shield.Target != null && shield.HasLineOfSight && shield.IsFiringMinigun) {
				groundMotor.Stop();
				shield.FaceTarget(shield.Target.position);
				return;
			}

			// Rebuild destinations at a fixed interval to avoid random frame-by-frame NavMesh updates
			if (Time.time >= nextDestinationRefreshTime || hasDestination == false) {
				currentDestination = BuildAdvanceDestination();
				TrySampleNavMesh(currentDestination, out currentDestination);
				hasDestination = true;
				nextDestinationRefreshTime = Time.time + destinationRefreshInterval;
			}

			groundMotor.Chase(currentDestination);
			FaceForCurrentMovement();

			// Shields stay raised while advancing so their movement reads as defensive pressure
			shield.SetShieldRaised(true);
		}

		// Chooses facing separately from path destination
		// Visible player = face player
		// No LOS = face actual NavMesh travel direction
		// Initial Fallback = destination
		// Final Fallback = target position
		private void FaceForCurrentMovement() {
			if (facePlayerWhileVisible && shield.HasTarget && shield.Target != null && shield.HasLineOfSight) {
				shield.FaceTarget(shield.Target.position);
				return;
			}

			if (faceDesiredVelocityWithoutLineOfSight && groundMotor.HasUsefulDesiredVelocity(movementFaceVelocityThreshold)) {
				groundMotor.FaceDirection(groundMotor.DesiredVelocity);
				return;
			}

			if (hasDestination && (currentDestination - transform.position).sqrMagnitude > 0.25f) {
				shield.FaceTarget(currentDestination);
				return;
			}

			if (shield.Target != null) {
				shield.FaceTarget(shield.Target.position);
			}
		}

		// Builds the shield destination from live prediction when visible, otherwise from last known awareness
		// This lets shield enemies advance on the current target while visible and
		// continue pathing toward the last known fight location after sight is broken
		private Vector3 BuildAdvanceDestination() {
			PlayerMotionPredictor predictor = director != null ? director.PlayerPredictor : PlayerMotionPredictor.Active;
			EnemyAwarenessHub awareness = EnemyAwarenessHub.Active;

			bool canUseLivePrediction = shield.HasTarget && shield.Target != null && shield.HasLineOfSight && predictor != null;

			if (canUseLivePrediction) {
				// Live sight overrides spawn awareness because the current target state is fresher
				hasSpawnAwarenessDestination = false;
				return predictor.ShortFuturePosition;
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
			return shield.Target != null ? shield.Target.position : transform.position;
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
				Gizmos.DrawWireSphere(currentDestination, 0.35f);
				Gizmos.DrawLine(transform.position, currentDestination);
			}

			if (hasSpawnAwarenessDestination) {
				Gizmos.DrawWireSphere(spawnAwarenessDestination, 0.45f);
			}
		}

		private void OnValidate() {
			destinationRefreshInterval = Mathf.Max(0.05f, destinationRefreshInterval);
			navMeshSampleRadius = Mathf.Max(0.05f, navMeshSampleRadius);
			reachedKnownPositionDistance = Mathf.Max(0.0f, reachedKnownPositionDistance);
			movementFaceVelocityThreshold = Mathf.Max(0.0f, movementFaceVelocityThreshold);
			minigunTokenRefreshTime = Mathf.Max(0.0f, minigunTokenRefreshTime);
			slamTokenTime = Mathf.Max(0.0f, slamTokenTime);
		}

		// - DEPRECATED - 
		
		// REASON: Shield enemy would face the wrong direction when walking (was facing the predicted direction)
		//private Vector3 GetFacePoint() {
		//	// Prefer facing toward the same predictive/awareness point the shield is chasing
		//	PlayerMotionPredictor predictor = director != null ? director.PlayerPredictor : PlayerMotionPredictor.Active;
		//	if (shield.HasTarget && shield.Target != null && shield.HasLineOfSight && predictor != null) {
		//		return predictor.ShortFuturePosition;
		//	}

		//	if (EnemyAwarenessHub.Active != null && EnemyAwarenessHub.Active.TryGetKnownPosition(out Vector3 knownPosition)) {
		//		return knownPosition;
		//	}

		//	if (hasSpawnAwarenessDestination) {
		//		return spawnAwarenessDestination;
		//	}

		//	return shield.Target != null ? shield.Target.position : currentDestination;
		//}
	}
}