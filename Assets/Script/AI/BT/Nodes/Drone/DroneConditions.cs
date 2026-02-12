using Unity.AppUI.Core;
using UnityEngine;
using Unity.Behavior;


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

	// Category: Enemy/Drone/Conditions

	// - DRONE CONDITIONS ID NAMES -
	// IsKnockedDown -> drone.condition.core.is_knocked_down
	// CanFire -> drone.condition.combat.can_fire
	// InFireRange -> drone.condition.combat.in_fire_range
	// TooClose -> drone.condition.flight.too_close
	// (optional) TooFar -> drone.condition.flight.too_far

	// - DRONE CONDITIONS CATEGORY SCRIPT NAMES -
	// DroneConditions_Core.cs
	// DroneConditions_Combat.cs
	// DroneConditions_Flight.cs

	//TBD
}