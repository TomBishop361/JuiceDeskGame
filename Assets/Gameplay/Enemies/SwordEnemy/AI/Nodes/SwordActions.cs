using UnityEngine;
using Unity.Behavior;
using Game.AI.Sword; // SwordEnemy namespace

// SwordActions.cs
namespace Game.AI.Behavior.Sword {
	// NOTE: IDs must be globally unique and should NEVER change once used in graphs.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name

	[NodeDescription(name: "Sword: Swing Attack", description: "Starts a swing attack and waits until it finishes.", story: "Sword enemy performs a swing", category: "Enemy/Sword/Actions/Combat", id: "sword.action.combat.swing_attack")]
	public sealed class SwordSwingAttack : Action {
		private SwordEnemy swordEnemy;

		protected override Status OnStart() {
			swordEnemy = GameObject.GetComponent<SwordEnemy>();
			if (swordEnemy == null) {
				LogFailure("SwordEnemy component missing.", isError: true);
				return Status.Failure;
			}

			bool hasStartedSwing = swordEnemy.TryStartSwing();

			return hasStartedSwing ? Status.Running : Status.Failure;
		}

		protected override Status OnUpdate() {
			if (swordEnemy == null) {
				return Status.Failure;
			}

			return swordEnemy.IsAttacking ? Status.Running : Status.Success;
		}
	}

	[NodeDescription(name: "Sword: Lunge Attack", description: "Starts a lunge attack and waits until it finishes.", story: "Sword enemy performs a lunge", category: "Enemy/Sword/Actions/Combat", id: "sword.action.combat.lunge_attack")]
	public sealed class SwordLungeAttack : Action {
		private SwordEnemy swordEnemy;

		protected override Status OnStart() {
			swordEnemy = GameObject.GetComponent<SwordEnemy>();
			if (swordEnemy == null) {
				LogFailure("SwordEnemy component missing.", isError: true);
				return Status.Failure;
			}

			bool hasStartedLunge = swordEnemy.TryStartLunge();

			return hasStartedLunge ? Status.Running : Status.Failure;
		}

		protected override Status OnUpdate() {
			if (swordEnemy == null) {
				return Status.Failure;
			}

			return swordEnemy.IsAttacking ? Status.Running : Status.Success;
		}
	}

	// Category: Enemy/Sword/Actions

	// - SWORD ACTIONS ID NAMES -
	// Swing -> sword.action.combat.swing - DONE
	// Lunge -> sword.action.combat.lunge - DONE

	// - SWORD ACTION CATEGORY SCRIPT NAMES -
	// SwordActions_Movement.cs (maybe)
	// SwordActions_Combat.cs
}