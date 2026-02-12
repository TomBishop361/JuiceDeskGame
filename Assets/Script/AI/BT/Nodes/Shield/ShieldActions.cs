using Unity.AppUI.Core;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;
using Unity.Behavior;
using Game.AI.Shield; // ShieldEnemy namespace

// ShieldActions.cs
namespace Game.AI.Behavior.Shield {
	// NOTE: IDs must be globally unique and should NEVER change once used in graphs.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name


	[NodeDescription(name: "Shield: Advance Raised (Tick)", description: "Moves toward target with shield raised locomotion/state.", story: "Shield enemy advances with shield raised", category: "Enemy/Shield/Actions/Movement", id: "shield.action.move.advance_raised_tick")]
	public sealed class ShieldAdvanceRaisedTick : Action {
		private ShieldEnemy shieldEnemy;

		protected override Status OnStart() {
			shieldEnemy = GameObject.GetComponent<ShieldEnemy>();
			if (shieldEnemy == null) {
				LogFailure("ShieldEnemy component missing.", isError: true);
				return Status.Failure;
			}

			if (shieldEnemy.HasTarget == false) {
				return Status.Failure;
			}

			// If already in punch range, allow combat to take over
			if (shieldEnemy.InPunchRange == true || shieldEnemy.InSlamRange == true) {
				return Status.Success;
			}

			shieldEnemy.AdvanceRaisedTick();

			return Status.Running;
		}

		protected override Status OnUpdate() {
			if (shieldEnemy == null) {
				return Status.Failure;
			}
			if (shieldEnemy.HasTarget == false) {
				return Status.Failure;
			}

			if (shieldEnemy.InPunchRange == true || shieldEnemy.InSlamRange == true) {
				return Status.Success;
			}

			shieldEnemy.AdvanceRaisedTick();

			return Status.Running;
		}
	}

	[NodeDescription(name: "Shield: Punch Attack", description: "Triggers punch attack and waits until finished.", story: "Shield enemy punches", category: "Enemy/Shield/Actions/Combat", id: "shield.action.combat.punch")]
	public sealed class ShieldPunchAttack : Action {
		private ShieldEnemy shieldEnemy;

		protected override Status OnStart() {
			shieldEnemy = GameObject.GetComponent<ShieldEnemy>();
			if (shieldEnemy == null) {
				LogFailure("ShieldEnemy component missing.", isError: true);
				return Status.Failure;
			}

			bool hasStartedPunchAttack = shieldEnemy.TryStartPunch();

			return hasStartedPunchAttack ? Status.Running : Status.Failure;
		}

		protected override Status OnUpdate() {
			if (shieldEnemy == null) {
				return Status.Failure;
			}

			// Running whilst the Punch animation/attack is active (Returns Success once finished)
			return shieldEnemy.IsAttacking ? Status.Running : Status.Success;
		}
	}

	[NodeDescription(name: "Shield: Slam Attack (Opens Grapple Window)", description: "Triggers slam attack. Opens grapple window for a duration.", story: "Shield enemy slams and opens grapple window", category: "Enemy/Shield/Actions/Combat", id: "shield.action.combat.slam_open_grapple")]
	public sealed class ShieldSlamAttackOpenGrapple : Action {
		private ShieldEnemy shieldEnemy;

		protected override Status OnStart() {
			shieldEnemy = GameObject.GetComponent<ShieldEnemy>();
			if (shieldEnemy == null) {
				LogFailure("ShieldEnemy component missing.", isError: true);
				return Status.Failure;
			}

			bool hasStartedSlamAttack = shieldEnemy.TryStartSlam();

			return hasStartedSlamAttack ? Status.Running : Status.Failure;
		}

		protected override Status OnUpdate() {
			if (shieldEnemy == null) {
				return Status.Failure;
			}

			// Running whilst the Slam animation/attack is active (Returns Success once finished)
			return shieldEnemy.IsAttacking ? Status.Running : Status.Success;
		}
	}

	// TODO: ADD ShieldBlockReact ACTION NODE CLASS


	// Category: Enemy/Shield/Actions

	// - SHIELD ACTIONS ID NAMES -
	// AdvanceRaised -> shield.action.move.advance_raised - DONE
	// Punch -> shield.action.combat.punch - DONE
	// Slam -> shield.action.combat.slam - DONE
	// OpenGrappleWindow (if separate) -> shield.action.grapple.open_window - DONE
	// CloseGrappleWindow -> shield.action.grapple.close_window
	// BlockReact -> shield.action.defense.block_react (optional later) 

	// - SHIELD ACTION CATEGORY SCRIPT NAMES -
	// ShieldActions_Movement.cs (chase but plays 'raised shield' locomotion
	// ShieldActions_Combat.cs
	// ShieldActions_Grapple.cs
	// ShieldActions_Defense.cs
}
