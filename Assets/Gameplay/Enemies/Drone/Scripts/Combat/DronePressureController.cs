using Game.AI.Drone;
using UnityEngine;

namespace Game.AI {
	// Code-driven slot orbit and token-gated firing for drones
	// Uses the existing FlightEnemyMotor for obstacle avoidance and separation
	// Drones orbit slots around predicted or remembered player positions
	// This keeps ranged pressure readable and prevents every drone from chasing the same point
	[DisallowMultipleComponent]
	[RequireComponent(typeof(DroneEnemy))]
	[RequireComponent(typeof(DroneFlightMotor))]
	[RequireComponent(typeof(FlightEnemyMotor))]
	[RequireComponent(typeof(EnemyPressureAgent))]
	[RequireComponent(typeof(EnemyAwarenessReporter))]
	public sealed class DronePressureController : MonoBehaviour {
		[Header("Orbit")]
		[Tooltip("Preferred drone orbit distance around the predicted or last known player position.")]
		[SerializeField] private float orbitRadius = 7.0f;
		[Tooltip("Additional radius for drones assigned to BacklineHarass.")]
		[SerializeField] private float backlineExtraDistance = 3.0f;
		[Tooltip("Rotates all drone orbit slots so they do not sit exactly on basic directions.")]
		[SerializeField] private float slotAngleOffsetDegrees = 25.0f;
		[Tooltip("Small sinusoidal slot offset so drones feel alive without changing jobs constantly.")]
		[SerializeField] private float slotRepositionJitter = 0.65f;

		[Header("Fire Fairness")]
		[Tooltip("How often this drone checks whether it may start a readable shot.")]
		[SerializeField] private float fireCheckInterval = 0.15f;
		[Tooltip("Maximum angle to the target before drone fire is allowed.")]
		[SerializeField] private float fireFacingAngle = 55.0f;
		[Tooltip("How long a drone fire token is held after starting a shot.")]
		[SerializeField] private float droneFireTokenTime = 0.75f;
		[Tooltip("If true, drones can only fire with real LOS. Keep true for fairness.")]
		[SerializeField] private bool requireLineOfSightToFire = true;

		[Header("Control")]
		[Tooltip("If true, calls the existing enemy AcquireTarget when no target is assigned.")]
		[SerializeField] private bool autoAcquireTarget = true;
		[Tooltip("If true, this pressure controller starts attacks. Disable if a BT/action owns attacks.")]
		[SerializeField] private bool driveAttacks = true;
		[Tooltip("If true, this pressure controller moves the enemy. Disable if another system owns movement.")]
		[SerializeField] private bool driveMovement = true;

		private DroneEnemy drone;
		private DroneFlightMotor droneFlightMotor;
		private FlightEnemyMotor flightMotor;
		private EnemyPressureAgent pressureAgent;

		// Cached active pressure director
		// This can change at runtime, so Update() refreshes it from the singleton
		private CombatPressureDirector director;

		// Limits fire eligibility checks so drones do not request tokens every frame
		private float nextFireCheckTime = -Mathf.Infinity;

		// Per-drone offset used to desynchronise orbit jitter between drones
		private float jitterSeed;

		// Temporary fallback destination used when this enemy spawns after the player was already spotted
		private bool hasSpawnAwarenessDestination;
		private Vector3 spawnAwarenessDestination;

		private void Awake() {
			drone = GetComponent<DroneEnemy>();
			droneFlightMotor = GetComponent<DroneFlightMotor>();
			flightMotor = GetComponent<FlightEnemyMotor>();
			pressureAgent = GetComponent<EnemyPressureAgent>();
			jitterSeed = Random.Range(0.0f, 100.0f);
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

			if (drone == null || droneFlightMotor == null || flightMotor == null || pressureAgent == null) {
				return;
			}

			if (autoAcquireTarget && drone.HasTarget == false) {
				drone.AcquireTarget();
			}

			if (drone.IsDead || drone.IsStunned || drone.IsKnockedDown) {
				return;
			}

			bool hasTarget = drone.HasTarget && drone.Target != null;
			bool hasAwareness = EnemyAwarenessHub.Active != null && EnemyAwarenessHub.Active.HasKnownPosition;

			// With no target and no known player position, there is no meaningful anchor to move toward
			if (hasTarget == false && hasAwareness == false && hasSpawnAwarenessDestination == false) {
				return;
			}

			if (driveMovement) {
				TickMovement();
			}

			// Fire checks are interval-based for fairness and to avoid token-request spam
			if (driveAttacks && Time.time >= nextFireCheckTime) {
				TickFire();
				nextFireCheckTime = Time.time + Mathf.Max(0.03f, fireCheckInterval);
			}
		}

		// Movement is pressure/memory driven
		// LOS is not required for movement around corners
		private void TickMovement() {
			// If the target is too close, let the drone flight motor handle movement so it can create space
			if (drone.HasTarget && drone.Target != null && drone.TargetTooClose) {
				droneFlightMotor.TickMovement(drone.Target);
				return;
			}

			PlayerMotionPredictor predictor = director != null ? director.PlayerPredictor : PlayerMotionPredictor.Active;
			EnemyAwarenessHub awareness = EnemyAwarenessHub.Active;

			bool canUseLivePrediction = drone.HasTarget && drone.Target != null && drone.HasLineOfSight && predictor != null;

			Vector3 anchor;
			Vector3 facePoint;

			if (canUseLivePrediction) {
				// Live sight overrides spawn awareness because the current target state is fresher
				hasSpawnAwarenessDestination = false;
				anchor = predictor.MediumFuturePosition;
				facePoint = predictor.ShortFuturePosition;
			}
			else if (hasSpawnAwarenessDestination) {
				// Use the one-time spawn awareness destination until it is reached, then clear it
				anchor = spawnAwarenessDestination;
				facePoint = spawnAwarenessDestination;
			}
			else if (awareness != null && awareness.TryGetKnownPosition(out Vector3 knownPosition)) {
				// Use shared last-known player awareness when LOS is lost.
				anchor = knownPosition;
				facePoint = knownPosition;
			}
			else if (drone.Target != null) {
				// Fallback to the raw target transform if there is a target but no predictor/awareness data
				anchor = drone.Target.position;
				facePoint = drone.Target.position;
			}
			else {
				return;
			}

			// Convert this drone's pressure slot into a desired orbit position and face the target/known point
			Vector3 desired = BuildSlotPosition(anchor, canUseLivePrediction ? predictor : null);
			flightMotor.MoveToPoint(desired, facePoint, anchor.y);
		}

		private void TickFire() {
			// Drones only fire at actual targets
			// Remembered positions are for movement only
			if (drone.HasTarget == false || drone.Target == null) {
				return;
			}

			// Avoid starting a new shot while already attacking, on cooldown, or outside fire range
			if (drone.IsAttacking || drone.CanFire == false || drone.InFireRange == false) {
				return;
			}

			// Optional LOS gate keeps ranged pressure readable and avoids unfair shots through cover
			if (requireLineOfSightToFire && drone.HasLineOfSight == false) {
				return;
			}

			// Require the drone to be roughly facing the target before it can shoot
			if (IsFacingTarget(fireFacingAngle) == false) {
				return;
			}

			// Request a shared fire token so only a limited number of drones can shoot at once
			if (pressureAgent.TryRequestAttackToken(PressureAttackTokenType.DroneFire, droneFireTokenTime) == false) {
				return;
			}

			// If the shot fails to start, immediately release the token so another enemy can use it
			if (drone.TryStartFire() == false) {
				pressureAgent.ReleaseAttackToken(PressureAttackTokenType.DroneFire);
			}
		}

		// Converts the assigned drone slot into a world-space orbit position
		private Vector3 BuildSlotPosition(Vector3 anchor, PlayerMotionPredictor predictor) {
			// Slot count/index comes from the pressure agent so multiple drones spread evenly around the anchor
			int count = Mathf.Max(1, pressureAgent != null ? pressureAgent.SlotCount : 1);
			int slot = pressureAgent != null ? Mathf.Abs(pressureAgent.SlotIndex) % count : 0;

			// Convert the slot index into a direction around the target
			float angle = ((360.0f / count) * slot + slotAngleOffsetDegrees) * Mathf.Deg2Rad;
			Vector3 radial = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));

			// Backline harassers prefer to sit behind the player's planar movement direction when prediction is available
			if (predictor != null && pressureAgent != null && pressureAgent.CurrentJob == EnemyPressureJob.BacklineHarass) {
				Vector3 behind = -predictor.PlanarMoveDirection;
				if (behind.sqrMagnitude > 0.0001f) {
					radial = behind.normalized;
				}
			}

			// Backline harassers orbit farther out than standard pressure slots
			float radius = orbitRadius;
			if (pressureAgent != null && pressureAgent.CurrentJob == EnemyPressureJob.BacklineHarass) {
				radius += backlineExtraDistance;
			}

			// Add a small tangential wobble so drones do not look perfectly static when in their slots
			float jitter = Mathf.Sin(Time.time * 0.7f + jitterSeed) * slotRepositionJitter;
			Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;

			return anchor + radial * radius + tangent * jitter;
		}

		private bool IsFacingTarget(float maxAngle) {
			if (drone.Target == null) {
				return false;
			}

			// Compare only planar direction so vertical height differences do not affect fire permission
			Vector3 toTarget = drone.Target.position - transform.position;
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

		private void OnDrawGizmosSelected() {
			if (pressureAgent == null) {
				pressureAgent = GetComponent<EnemyPressureAgent>();
			}

			PlayerMotionPredictor predictor = PlayerMotionPredictor.Active;
			if (predictor == null || pressureAgent == null) {
				return;
			}

			// Visualise the calculated orbit slot from the current predicted player position
			Vector3 slot = BuildSlotPosition(predictor.MediumFuturePosition, predictor);
			Gizmos.DrawWireSphere(slot, 0.3f);
			Gizmos.DrawLine(transform.position, slot);
		}
	}
}