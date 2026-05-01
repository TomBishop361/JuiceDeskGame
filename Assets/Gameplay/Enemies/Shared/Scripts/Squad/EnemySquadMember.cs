using UnityEngine;

namespace Game.AI {
	// Connects one enemy to a squad director
	// This lets the enemy share awareness and receive a tactical role
	// The director writes roles into EnemyBlackboard so BT's can branch cleanly
	[DisallowMultipleComponent]
	[RequireComponent(typeof(EnemyBlackboard))]
	public sealed class EnemySquadMember : MonoBehaviour {
		[Header("Squad")]
		[Tooltip("Enemies with the same Squad Id share awareness. Use different IDs for separate levels/waves.")]
		[SerializeField] private string squadId = "Default";
		[Tooltip("Optional explicit director. If empty, the member finds a director with the same Squad Id.")]
		[SerializeField] private EnemySquadDirector director;

		public string SquadId => squadId;
		public EnemyBlackboard Blackboard => blackboard;

		// Current tactical role assigned by the squad director
		public EnemyTacticalRole Role => blackboard != null ? blackboard.TacticalRole : EnemyTacticalRole.None;

		private EnemyBlackboard blackboard;

		private void Awake() {
			// Cache the blackboard once so squad messages can update it quickly
			blackboard = GetComponent<EnemyBlackboard>();
		}

		private void OnEnable() {
			// Prefer a director with the same Squad Id
			if (director == null) {
				director = EnemySquadDirector.FindForSquad(squadId);
			}

			// Fallback for scenes with only one director
			if (director == null) {
				director = FindFirstObjectByType<EnemySquadDirector>();
			}

			// Register this enemy so it can receive roles and shared awareness
			director?.Register(this);
		}

		private void OnDisable() {
			// Unregister so disabled or pooled enemies stop receiving squad updates
			director?.Unregister(this);
		}

		// Changes this enemy's squad director at runtime
		public void SetDirector(EnemySquadDirector newDirector) {
			if (director == newDirector) {
				return;
			}

			director?.Unregister(this);
			director = newDirector;
			director?.Register(this);
		}

		// Sends a confirmed player sighting to the squad director
		public void ReportPlayerSighting(Transform target, Vector3 position, Vector3 velocity) {
			director?.ReportPlayerSighting(this, target, position, velocity);
		}

		// Sends a heard noise position to the squad director
		public void ReportHeardNoise(Vector3 position, float radius, AINoiseKind kind, float time) {
			director?.ReportNoise(this, position, radius, kind, time);
		}

		// Receives a player sighting from another squad member
		public void ApplySharedSighting(Transform target, Vector3 position, Vector3 velocity, float reportTime) {
			blackboard?.SetSharedSighting(target, position, velocity, reportTime);
		}

		// Receives a suspicious noise report from another squad member
		public void ApplySharedNoise(Vector3 position, float radius, AINoiseKind kind, float time) {
			blackboard?.SetSuspiciousNoise(position, radius, kind, time, true);
		}

		// Receives the tactical role chosen by the squad director
		public void AssignRole(EnemyTacticalRole role) {
			blackboard?.SetTacticalRole(role);
		}
	}
}