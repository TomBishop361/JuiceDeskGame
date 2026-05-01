using UnityEngine;
using Unity.Behavior;
using Game.AI.Sword; // SwordEnemy namespace

// SwordConditions.cs
namespace Game.AI.Behavior.Sword {
	// NOTE: IDs must be globally unique and should NEVER change once used in graphs.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name

	[Condition(name: "Sword: In Swing Range", description: "True if target is within swing range.", story: "Target is in swing range", category: "Enemy/Sword/Conditions/Combat", id: "sword.condition.combat.in_swing_range")]
	public sealed class SwordInSwingRange : Condition {
		public override bool IsTrue() {
			SwordEnemy swordEnemy = GameObject.GetComponent<SwordEnemy>();

			return swordEnemy != null && swordEnemy.InSwingRange;
		}
	}

	[Condition(name: "Sword: In Lunge Range", description: "True if target is within lunge range.", story: "Target is in lunge range", category: "Enemy/Sword/Conditions/Combat", id: "sword.condition.combat.in_lunge_range")]
	public sealed class SwordInLungeRange : Condition {
		public override bool IsTrue() {
			SwordEnemy swordEnemy = GameObject.GetComponent<SwordEnemy>();

			return swordEnemy != null && swordEnemy.InLungeRange;
		}
	}

	[Condition(name: "Sword: Should Lunge", description: "True if lunge is preferred over swing (typically mid-range).", story: "Sword should lunge", category: "Enemy/Sword/Conditions/Combat", id: "sword.condition.combat.should_lunge")]
	public sealed class SwordShouldLunge : Condition {
		public override bool IsTrue() {
			SwordEnemy swordEnemy = GameObject.GetComponent<SwordEnemy>();

			return swordEnemy != null && swordEnemy.ShouldLunge;
		}
	}

	[Condition(name: "Sword: Can Swing", description: "True if swing can start now (cooldown ready, not stunned, etc.).", story: "Sword can swing", category: "Enemy/Sword/Conditions/Combat", id: "sword.condition.combat.can_swing")]
	public sealed class SwordCanSwing : Condition {
		public override bool IsTrue() {
			SwordEnemy swordEnemy = GameObject.GetComponent<SwordEnemy>();

			return swordEnemy != null && swordEnemy.CanSwing;
		}
	}

	[Condition(name: "Sword: Can Lunge", description: "True if lunge can start now (cooldown ready, not stunned, etc.).", story: "Sword can lunge", category: "Enemy/Sword/Conditions/Combat", id: "sword.condition.combat.can_lunge")]
	public sealed class SwordCanLunge : Condition {
		public override bool IsTrue() {
			SwordEnemy swordEnemy = GameObject.GetComponent<SwordEnemy>();

			return swordEnemy != null && swordEnemy.CanLunge;
		}
	}


	// Category: Enemy/Sword/Conditions

	// - SWORD CONDITIONS ID NAMES -
	// InSwingRange -> sword.condition.combat.in_swing_range - DONE
	// InLungeRange -> sword.condition.combat.in_lunge_range - DONE
	// ShouldLunge -> sword.condition.combat.should_lunge - DONE
	// CanSwing -> sword.condition.combat.can_swing - DONE
	// CanLunge -> sword.condition.combat.can_lunge - DONE

	// - SWORD CONDITIONS CATEGORY SCRIPT NAMES -
	// SwordConditions_Movement.cs (maybe)
	// SwordConditions_Combat.cs
}

// - DEPRECATED (FOR NOW) -

// REASON: Have a shared IsDead Condition node inside SharedConditions.cs
//[Condition(name: "Sword: Is Dead", description: "True if the SwordEnemy is dead.", story: "Sword enemy is dead", category: "Enemy/Sword/Conditions", id: "sword.condition.is_dead")]
//public sealed class SwordIsDead : Condition {
//	public override bool IsTrue() {
//		SwordEnemy enemy = GameObject.GetComponent<SwordEnemy>();
//		return enemy != null && enemy.IsDead;
//	}
//}