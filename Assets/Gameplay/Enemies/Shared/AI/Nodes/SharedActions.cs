using UnityEngine;
using Unity.Behavior;
using Game.AI; // IEnemyAgent namespace

// SharedActions.cs
namespace Game.AI.Behavior.Shared {
	// NOTE: IDs must be globally unique and should NEVER change once used in graphs.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// INFO: 'IEnemyAgent' is an interface implemented by all enemy types

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name

	[NodeDescription(name: "Enemy: Acquire Target", description: "Assigns a target.", story: "Enemy acquires a target", category: "Enemy/Shared/Actions/Core", id: "enemy.action.core.acquire_target")]
	public sealed class EnemyAcquireTarget : Action {
		private IEnemyAgent agent;

		protected override Status OnStart() {
			agent = GameObject.GetComponent<IEnemyAgent>();
			if (agent == null) {
				LogFailure("IEnemyAgent not found on this GameObject.", isError: true);
				return Status.Failure;
			}

			agent.AcquireTarget();

			return agent.HasTarget ? Status.Success : Status.Failure;
		}
	}

	[NodeDescription(name: "Enemy: Die", description: "Runs the enemy death logic and stops movement.", story: "Enemy dies", category: "Enemy/Shared/Actions/Core", id: "enemy.action.core.die")]
	public sealed class EnemyDie : Action {
		private IEnemyAgent agent;
		protected override Status OnStart() {
			agent = GameObject.GetComponent<IEnemyAgent>();
			if (agent == null) {
				LogFailure("IEnemyAgent not found on this GameObject.", isError: true);
				return Status.Failure;
			}

			agent.Die();

			return Status.Success;
		}
	}

	[NodeDescription(name: "Enemy: Recover (Tick)", description: "Called while stunned. Returns Running until the enemy is no longer stunned.", story: "Enemy recovers from stun", category: "Enemy/Shared/Actions/Core", id: "enemy.action.core.recover_tick")]
	public sealed class EnemyRecoverTick : Action {
		private IEnemyAgent agent;

		protected override Status OnStart() {
			agent = GameObject.GetComponent<IEnemyAgent>();
			if (agent == null) {
				LogFailure("IEnemyAgent not found on this GameObject.", isError: true);
				return Status.Failure;
			}

			// Safety check on start
			if (agent.IsStunned == false) {
				return Status.Success;
			}

			agent.RecoverTick();

			return Status.Running;
		}

		protected override Status OnUpdate() {
			if (agent == null) {
				return Status.Failure;
			}

			// Runs recover action until enemy is no longer stunned
			if (agent.IsStunned == false) {
				return Status.Success;
			}

			agent.RecoverTick();

			return Status.Running;
		}
	}

	[NodeDescription(name: "Enemy: Chase Target (Tick)", description: "Moves toward target. Returns Success when in attack range.", story: "Enemy chases target", category: "Enemy/Shared/Actions/Movement", id: "enemy.action.move.chase_target_tick")]
	public sealed class EnemyChaseTargetTick : Action {
		private IEnemyAgent agent;

		protected override Status OnStart() {
			agent = GameObject.GetComponent<IEnemyAgent>();
			if (agent == null) {
				LogFailure("IEnemyAgent not found on this GameObject.", isError: true);
				return Status.Failure;
			}

			// Target lost - stop chasing
			if (agent.HasTarget == false) {
				return Status.Failure;
			}
			// Target inside attack range - stop chasing and attack
			if (agent.InAttackRange == true) {
				return Status.Success;
			}

			agent.ChaseTargetTick();

			return Status.Running;
		}

		protected override Status OnUpdate() {
			if (agent == null) {
				return Status.Failure;
			}
			if (agent.HasTarget == false) {
				return Status.Failure;
			}

			if (agent.InAttackRange == true) {
				return Status.Success;
			}

			agent.ChaseTargetTick();

			return Status.Running;
		}
	}

	[NodeDescription(name: "Enemy: Stop Move", description: "Stops movement immediately.", story: "Enemy stops moving", category: "Enemy/Shared/Actions/Movement", id: "enemy.action.move.stop")]
	public sealed class EnemyStopMove : Action {
		private IEnemyAgent agent;

		protected override Status OnStart() {
			agent = GameObject.GetComponent<IEnemyAgent>();
			if (agent == null) {
				LogFailure("IEnemyAgent not found on this GameObject.", isError: true);
				return Status.Failure;
			}

			agent.StopMove();

			return Status.Success;
		}
	}

	//[NodeDescription(name: "Enemy: Face Target", description: "Turns towards a target.", story: "Enemy faces target", category: "Enemy/Shared/Actions/Movement", id: "enemy.action.move.face_target")]
	//public sealed class EnemyFaceTarget : Action {
	//	private IEnemyAgent agent;
	//
	//	protected override Status OnStart() {
	//		agent = GameObject.GetComponent<IEnemyAgent>();
	//		if (agent == null) {
	//			LogFailure("IEnemyAgent not found on this GameObject.", isError: true);
	//			return Status.Failure;
	//		}

	//		agent.FaceTarget();

	//		return Status.Success;
	//	}
	//}

	[NodeDescription(name: "Enemy: Primary Attack", description: "Starts primary attack and waits until it finishes.", story: "Enemy performs primary attack", category: "Enemy/Shared/Actions/Combat", id: "enemy.action.combat.primary_attack")]
	public sealed class EnemyPrimaryAttack : Action {
		private IEnemyAgent agent;

		protected override Status OnStart() {
			agent = GameObject.GetComponent<IEnemyAgent>();
			if (agent == null) {
				LogFailure("IEnemyAgent not found on this GameObject.", isError: true);
				return Status.Failure;
			}

			if (agent.HasTarget == false) {
				return Status.Failure;
			}

			bool hasStartedPrimaryAttack = agent.TryStartPrimaryAttack();

			return hasStartedPrimaryAttack ? Status.Running : Status.Failure;
		}

		protected override Status OnUpdate() {
			if (agent == null) {
				return Status.Failure;
			}

			// Running whilst the Primary animation/attack is active (Returns Success once finished)
			return agent.IsAttacking ? Status.Running : Status.Success;
		}
	}

	// - SHARED ACTIONS ID NAMES -
	// AcquireTarget -> enemy.action.core.acquire_target (optional) - DONE
	// Die -> enemy.action.core.die - DONE
	// Recover -> enemy.action.core.recover_tick - DONE
	// ChaseTarget -> enemy.action.move.chase_target_tick - DONE
	// StopMovement -> enemy.action.move.stop (optional) - DONE
	// FaceTarget -> enemy.action.move.face_target (optional) - PARTIALLY DONE
	// PrimaryAttack -> enemy.action.combat.primary_attack - DONE

	// - SHARED ACTION CATEGORY SCRIPT NAMES -
	// SharedActions_Core.cs
	// SharedActions_Movement.cs
	// SharedActions_Combat.cs
	// SharedActions_Utility.cs (optional)
	// SharedActions_Debug.cs
}