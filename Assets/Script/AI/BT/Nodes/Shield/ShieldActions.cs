using Unity.AppUI.Core;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;
using Unity.Behavior;

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

	// Category: Enemy/Shield/Actions

	// - SHIELD ACTIONS ID NAMES -
	// AdvanceRaised -> shield.action.move.advance_raised
	// Punch -> shield.action.combat.punch
	// Slam -> shield.action.combat.slam
	// OpenGrappleWindow (if separate) -> shield.action.grapple.open_window
	// CloseGrappleWindow -> shield.action.grapple.close_window
	// BlockReact -> shield.action.defense.block_react (optional later) 

	// - SHIELD ACTION CATEGORY SCRIPT NAMES -
	// ShieldActions_Movement.cs (chase but plays 'raised shield' locomotion
	// ShieldActions_Combat.cs
	// ShieldActions_Grapple.cs
	// ShieldActions_Defense.cs

	//TBD
}
