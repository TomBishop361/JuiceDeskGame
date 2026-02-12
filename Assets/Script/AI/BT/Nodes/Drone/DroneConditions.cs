using Unity.AppUI.Core;
using UnityEngine;
using Unity.Behavior;
using Game.AI.Drone; // DroneEnemy namespace

// DroneConditions.cs
namespace Game.AI.Behavior.Drone {
	// NOTE: IDs must be globally unique and should NEVER change once used in graphs.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name


	[Condition(name: "Drone: Is Knocked Down", description: "True if drone is knocked down (e.g. tasered).", story: "Drone is knocked down", category: "Enemy/Drone/Conditions/Core", id: "drone.condition.core.is_knocked_down")]
	public sealed class DroneIsKnockedDown : Condition {
		public override bool IsTrue() {
			DroneEnemy droneEnemy = GameObject.GetComponent<DroneEnemy>();

			return droneEnemy != null && droneEnemy.IsKnockedDown;
		}
	}

	[Condition(name: "Drone: Player Too Close", description: "True if target is closer than min range.", story: "Drone target is too close", category: "Enemy/Drone/Conditions/Flight", id: "drone.condition.flight.too_close")]
	public sealed class DronePlayerTooClose : Condition {
		public override bool IsTrue() {
			DroneEnemy droneEnemy = GameObject.GetComponent<DroneEnemy>();

			return droneEnemy != null && droneEnemy.PlayerTooClose;
		}
	}

	[Condition(name: "Drone: In Fire Range", description: "True if target is within firing range.", story: "Drone is in fire range", category: "Enemy/Drone/Conditions/Combat", id: "drone.condition.combat.in_fire_range")]
	public sealed class DroneInFireRange : Condition {
		public override bool IsTrue() {
			DroneEnemy droneEnemy = GameObject.GetComponent<DroneEnemy>();

			return droneEnemy != null && droneEnemy.InFireRange;
		}
	}

	[Condition(name: "Drone: Can Fire", description: "True if drone can fire (cooldown ready, not stunned/knocked).", story: "Drone can fire", category: "Enemy/Drone/Conditions/Combat", id: "drone.condition.combat.can_fire")]
	public sealed class DroneCanFire : Condition {
		public override bool IsTrue() {
			DroneEnemy droneEnemy = GameObject.GetComponent<DroneEnemy>();

			return droneEnemy != null && droneEnemy.CanFire;
		}
	}

	// TODO: ADD DronePlayerTooFar CONDITION NODE CLASS



	// Category: Enemy/Drone/Conditions

	// - DRONE CONDITIONS ID NAMES -
	// IsKnockedDown -> drone.condition.core.is_knocked_down - DONE
	// TooClose -> drone.condition.flight.too_close - DONE
	// TooFar -> drone.condition.flight.too_far (optional)
	// InFireRange -> drone.condition.combat.in_fire_range - DONE
	// CanFire -> drone.condition.combat.can_fire - DONE

	// - DRONE CONDITIONS CATEGORY SCRIPT NAMES -
	// DroneConditions_Core.cs
	// DroneConditions_Flight.cs
	// DroneConditions_Combat.cs

}