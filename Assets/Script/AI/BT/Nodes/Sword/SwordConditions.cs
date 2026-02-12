using UnityEngine;
using Unity.Behavior;

// SwordConditions.cs
namespace Game.AI.Behavior.Sword {
	// NOTE: ids must be UNIQUE across the project. Keep them stable once committed.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name

	// - SWORD CONDITIONS ID NAMES -
	// ShouldLunge -> sword.condition.combat.should_lunge
	// CanLunge -> sword.condition.combat.can_lunge

	// - SWORD CONDITIONS CATEGORY SCRIPT NAMES -
	// SwordConditions_Movement.cs (maybe)
	// SwordConditions_Combat.cs

	// TBD 
}

//[Condition(name: "Sword: Is Dead", description: "True if the SwordEnemy is dead.", story: "Sword enemy is dead", category: "Enemy/Sword/Conditions", id: "sword.condition.is_dead")]
//public sealed class SwordIsDeadCondition : Condition {
//	public override bool IsTrue() {
//		var enemy = GameObject.GetComponent<SwordEnemy>();
//		return enemy != null /*&& enemy.IsDead*/;
//	}
//}