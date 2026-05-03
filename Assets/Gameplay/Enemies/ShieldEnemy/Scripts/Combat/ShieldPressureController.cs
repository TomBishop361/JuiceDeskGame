using Game.AI.Shield;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI {
	// Code-driven movement/attack pressure for the shield enemy (replaced BT movement/attack actions and conditions)
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
		private CombatPressureDirector director;
		private float nextDestinationRefreshTime = -Mathf.Infinity;
		private Vector3 currentDestination;
		private bool hasDestination;
		private bool hasSpawnAwarenessDestination;
		private Vector3 spawnAwarenessDestination;

		private void Awake() {
			shield = GetComponent<ShieldEnemy>();
			groundMotor = GetComponent<GroundEnemyMotor>();
			pressureAgent = GetComponent<EnemyPressureAgent>();
		}

		// On spawn/enable, inherit recent player awareness so newly spawned enemies do not stand idle
		private void OnEnable() {
			EnemyAwarenessHub hub = EnemyAwarenessHub.Active;
			if (hub != null && hub.TryGetKnownPositionForSpawn(out Vector3 position)) {
				hasSpawnAwarenessDestination = true;
				spawnAwarenessDestination = position;
			}
		}

		private void Update() {
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

			if (shield.IsAttacking || shield.IsExposed) {
				shield.StopMinigunFiring();
				return;
			}

			if (shield.InSlamRange && shield.CanSlam) {
				if (pressureAgent.TryRequestAttackToken(PressureAttackTokenType.ShieldSlam, slamTokenTime)) {
					if (shield.TryStartSlam()) {
						return;
					}

					pressureAgent.ReleaseAttackToken(PressureAttackTokenType.ShieldSlam);
				}
			}

			if (shield.InFireRange && shield.CanFireMinigun) {
				if (pressureAgent.TryRequestAttackToken(PressureAttackTokenType.ShieldMinigun, minigunTokenRefreshTime)) {
					shield.TryStartMinigun();
				}
				else {
					shield.StopMinigunFiring();
				}
			}
			else {
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

			if (shield.HasTarget && shield.HasLineOfSight && shield.IsFiringMinigun) {
				groundMotor.Stop();
				shield.FaceTarget(shield.Target.position);
				return;
			}

			if (Time.time >= nextDestinationRefreshTime || hasDestination == false) {
				currentDestination = BuildAdvanceDestination();
				TrySampleNavMesh(currentDestination, out currentDestination);
				hasDestination = true;
				nextDestinationRefreshTime = Time.time + Mathf.Max(0.05f, destinationRefreshInterval);
			}

			groundMotor.Chase(currentDestination);
			shield.FaceTarget(GetFacePoint());
			shield.SetShieldRaised(true);
		}

		// Builds the shield destination from live prediction when visible, otherwise from last known awareness
		private Vector3 BuildAdvanceDestination() {
			PlayerMotionPredictor predictor = director != null ? director.PlayerPredictor : PlayerMotionPredictor.Active;
			bool canUseLivePrediction = shield.HasTarget && shield.Target != null && shield.HasLineOfSight && predictor != null;

			if (canUseLivePrediction) {
				hasSpawnAwarenessDestination = false;
				return predictor.ShortFuturePosition;
			}

			if (hasSpawnAwarenessDestination) {
				if (Vector3.Distance(transform.position, spawnAwarenessDestination) > reachedKnownPositionDistance) {
					return spawnAwarenessDestination;
				}

				hasSpawnAwarenessDestination = false;
			}

			if (EnemyAwarenessHub.Active != null && EnemyAwarenessHub.Active.TryGetKnownPosition(out Vector3 knownPosition)) {
				return knownPosition;
			}

			return shield.Target != null ? shield.Target.position : transform.position;
		}

		private Vector3 GetFacePoint() {
			PlayerMotionPredictor predictor = director != null ? director.PlayerPredictor : PlayerMotionPredictor.Active;
			if (shield.HasTarget && shield.Target != null && shield.HasLineOfSight && predictor != null) {
				return predictor.ShortFuturePosition;
			}

			if (EnemyAwarenessHub.Active != null && EnemyAwarenessHub.Active.TryGetKnownPosition(out Vector3 knownPosition)) {
				return knownPosition;
			}

			if (hasSpawnAwarenessDestination) {
				return spawnAwarenessDestination;
			}

			return shield.Target != null ? shield.Target.position : currentDestination;
		}

		// Snaps destinations onto the NavMesh so corner pursuit uses pathfinding instead of wall hugging
		private bool TrySampleNavMesh(Vector3 desired, out Vector3 sampled) {
			if (NavMesh.SamplePosition(desired, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas)) {
				sampled = hit.position;
				return true;
			}

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

	}
}