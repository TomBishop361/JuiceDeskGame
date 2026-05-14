using Game.AI.Drone;
using UnityEngine;
namespace Game.AI {
    // Small FSM for hard interruption states only
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBlackboard))]
    public sealed class EnemyFSM : MonoBehaviour {
		[Tooltip("Current finite state. Read this from BT conditions for high-priority abort branches.")]
		[field: SerializeField] public EnemyFiniteState CurrentState { get; private set; } = EnemyFiniteState.Spawned;

		// Core enemy reference used to check stun and attack lock state
		private EnemyAgentBase agent;

		// Shared memory/state component used by perception + BT nodes
		private EnemyBlackboard blackboard;

		// Optional drone combat interface
		// Only drone enemies should have this
		private IDroneCombat droneCombat;

		private void Awake() {
			agent = GetComponent<EnemyAgentBase>();
			blackboard = GetComponent<EnemyBlackboard>();
			droneCombat = GetComponent<IDroneCombat>();
		}

		private void OnEnable() {
			// When the enemy is spawned or re-enabled, start in the Spawned state
			CurrentState = EnemyFiniteState.Spawned;
		}

		private void Update() {
			// Keep the FSM synced with the enemy's current hard interruption state
			RefreshState();
		}

		public void SetDisabled(bool disabled) {
			// Update the shared blackboard so BT nodes and other systems know this enemy is disabled
			if (blackboard != null) {
				blackboard.SetDisabled(disabled);
			}

			// Disabled enemies should not run normal behaviour
			CurrentState = disabled ? EnemyFiniteState.Disabled : EnemyFiniteState.Alive;
		}

		public void RefreshState() {
			// Death has the highest priority and should interrupt all other behaviour
			if (blackboard != null && blackboard.IsDead) {
				CurrentState = EnemyFiniteState.Dead;
				return;
			}

			// Disabled enemies should remain inactive until re-enabled
			if (blackboard != null && blackboard.IsDisabled) {
				CurrentState = EnemyFiniteState.Disabled;
				return;
			}

			// Drone-only hard state
			// Ground enemies skip this because droneCombat will be null
			if (droneCombat != null && droneCombat.IsKnockedDown) {
				CurrentState = EnemyFiniteState.KnockedDown;
				return;
			}

			// Stunned enemies should recover before making tactical decisions
			if (agent != null && agent.IsStunned) {
				CurrentState = EnemyFiniteState.Stunned;
				return;
			}

			// AttackLocked means an attack animation/action is currently in progress
			// This prevents movement or new attacks from interrupting the current attack unfairly
			if (agent != null && agent.IsAttacking) {
				CurrentState = EnemyFiniteState.AttackLocked;
				return;
			}

			// Default normal state
			CurrentState = EnemyFiniteState.Alive;
		}
	}
}