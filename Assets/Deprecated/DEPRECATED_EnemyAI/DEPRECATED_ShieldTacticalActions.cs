using Game.AI.Shield;
using Unity.Behavior;
using UnityEngine;


// DEPRECATED


namespace Game.AI.Behavior.Shield {
	// NOTE: IDs must be globally unique and should NEVER change once used in graphs.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name

	[NodeDescription(name: "Shield: Block React", description: "Triggers the shield block reaction without starting a new attack.", story: "Shield enemy block reacts", category: "Enemy/Shield/Actions/Defense", id: "shield.action.defense.block_react")]
	public sealed class ShieldBlockReact : Action {
		private ShieldEnemy shieldEnemy;

		protected override Status OnStart() {
			shieldEnemy = GameObject.GetComponent<ShieldEnemy>();
			if (shieldEnemy == null) {
				LogFailure("ShieldEnemy component missing.", isError: true);
				return Status.Failure;
			}

			shieldEnemy.TriggerBlockReact();
			return Status.Success;
		}
	}

	[NodeDescription(name: "Shield: Stop Minigun", description: "Stops minigun firing cleanly. Useful for BT abort/interrupt cleanup.", story: "Shield enemy stops minigun", category: "Enemy/Shield/Actions/Combat", id: "shield.action.combat.stop_minigun")]
	public sealed class ShieldStopMinigun : Action {
		private ShieldEnemy shieldEnemy;

		protected override Status OnStart() {
			shieldEnemy = GameObject.GetComponent<ShieldEnemy>();
			if (shieldEnemy == null) {
				LogFailure("ShieldEnemy component missing.", isError: true);
				return Status.Failure;
			}

			shieldEnemy.StopMinigunFiring();
			return Status.Success;
		}
	}
}
