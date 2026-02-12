using Unity.AppUI.Core;
using UnityEngine;
using Unity.Behavior;


// DroneActions.cs
namespace Game.AI.Behavior.Drone {
	// NOTE: ids must be UNIQUE across the project. Keep them stable once committed.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name

	// Category: Enemy/Drone/Actions

	// - DRONE ACTIONS ID NAMES -
	// RecoverFromKnockdown -> drone.action.core.recover_knockdown
	// FireProjectile -> drone.action.combat.fire_projectile
	// MaintainRange -> drone.action.flight.maintain_range
	// MoveAway -> drone.action.flight.move_away
	// (optional) SetAirborne -> drone.action.flight.set_airborne

	// - DRONE ACTION CATEGORY SCRIPT NAMES -
	// DroneActions_Core.cs
	// DroneActions_Combat.cs
	// DroneActions_Flight.cs

	//TBD
}