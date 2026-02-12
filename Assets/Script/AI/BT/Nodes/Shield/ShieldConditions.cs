using Unity.AppUI.Core;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;
using Unity.Behavior;

// ShieldConditions.cs
namespace Game.AI.Behavior.Shield {
	// NOTE: ids must be UNIQUE across the project. Keep them stable once committed.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name

	// Category: Enemy/Shield/Conditions

	// - SHIELD CONDITIONS ID NAMES -
	// InPunchRange -> shield.condition.combat.in_punch_range
	// InSlamRange -> shield.condition.combat.in_slam_range
	// CanSlam -> shield.condition.combat.can_slam
	// GrappleWindowOpen -> shield.condition.grapple.window_open
	// (optional later) PlayerInFront -> shield.condition.defense.player_in_front
	// (optional later) PlayerBehind -> shield.condition.defense.player_behind

	// - SHIELD CONDITIONS CATEGORY SCRIPT NAMES -
	// ShieldConditions_Movement.cs (maybe)
	// ShieldConditions_Combat.cs
	// ShieldConditions_Grapple.cs
	// ShieldConditions_Defense.cs

	//TBD
}
