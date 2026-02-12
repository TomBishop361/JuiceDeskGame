using UnityEngine;
using Unity.Behavior;

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


	// Category: Enemy/Sword/Actions

	// - SWORD ACTIONS ID NAMES -
	// Swing -> sword.action.combat.swing
	// Lunge -> sword.action.combat.lunge

	// - SWORD ACTION CATEGORY SCRIPT NAMES -
	// SwordActions_Movement.cs (maybe)
	// SwordActions_Combat.cs

	//TBD
}
