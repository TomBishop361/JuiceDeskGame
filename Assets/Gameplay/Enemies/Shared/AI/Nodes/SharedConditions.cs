using UnityEngine;
using Unity.Behavior;
using Game.AI; // IEnemyAgent namespace

// SharedConditions.cs
namespace Game.AI.Behavior.Shared {
	// NOTE: IDs must be globally unique and should NEVER change once used in graphs.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name

	[Condition(name: "Enemy: Is Dead", description: "True if the enemy is dead.", story: "Enemy is dead", category: "Enemy/Shared/Conditions/Core", id: "enemy.condition.core.is_dead")]
	public sealed class EnemyIsDead : Condition {
		public override bool IsTrue() {
			IEnemyAgent agent = GameObject.GetComponent<IEnemyAgent>();

			return agent != null && agent.IsDead;
		}
	}

	[Condition(name: "Enemy: Is Stunned", description: "True if the enemy is stunned / recovering.", story: "Enemy is stunned", category: "Enemy/Shared/Conditions/Core", id: "enemy.condition.core.is_stunned")]
	public sealed class EnemyIsStunned : Condition {
		public override bool IsTrue() {
			IEnemyAgent agent = GameObject.GetComponent<IEnemyAgent>();

			return agent != null && agent.IsStunned;
		}
	}

	[Condition(name: "Enemy: Has Target", description: "True if the enemy currently has a target.", story: "Enemy has target", category: "Enemy/Shared/Conditions/Core", id: "enemy.condition.core.has_target")]
	public sealed class EnemyHasTarget : Condition {
		public override bool IsTrue() {
			IEnemyAgent agent = GameObject.GetComponent<IEnemyAgent>();

			return agent != null && agent.HasTarget;
		}
	}

	[Condition(name: "Enemy: In Attack Range", description: "True if the enemy is within its attack range of target.", story: "Enemy is in attack range", category: "Enemy/Shared/Conditions/Combat", id: "enemy.condition.combat.in_attack_range")]
	public sealed class EnemyInAttackRange : Condition {
		public override bool IsTrue() {
			IEnemyAgent agent = GameObject.GetComponent<IEnemyAgent>();

			return agent != null && agent.InAttackRange;
		}
	}

	[Condition(name: "Enemy: Can Attack", description: "True if cooldown/state allows starting an attack now.", story: "Enemy can attack", category: "Enemy/Shared/Conditions/Combat", id: "enemy.condition.combat.can_attack")]
	public sealed class EnemyCanAttack: Condition {
		public override bool IsTrue() {
			IEnemyAgent agent = GameObject.GetComponent<IEnemyAgent>();

			return agent != null && agent.CanAttack;
		}
	}

	//[Condition(name: "Enemy: Target Too Close", description: "True if target is too close.", story: "Target too close", category: "Enemy/Shared/Conditions/Combat", id: "enemy.condition.combat.too_close")]
	//public sealed class EnemyTargetTooClose : Condition {
	//	public override bool IsTrue() {
	//		IEnemyAgent agent = GameObject.GetComponent<IEnemyAgent>();

	//		return agent != null && agent.IsTargetTooClose;
	//	}
	//}


	// - SHARED CONDITIONS ID NAMES -
	// IsDead -> enemy.condition.core.is_dead - DONE
	// IsStunned -> enemy.condition.core.is_stunned - DONE
	// HasTarget -> enemy.condition.core.has_target (optional) - DONE - NOTE: THIS IS THE AWARENESS VISION LOS 
	// InAttackRange -> enemy.condition.combat.in_attack_range - DONE
	// CanAttack -> enemy.condition.combat.can_attack - DONE
	// TargetTooClose -> enemy.condition.combat.too_close (optional)


	// - SHARED CONDITIONS CATEGORY SCRIPT NAMES -
	// SharedConditions_Core.cs
	// SharedConditions_Movement.cs
	// SharedConditions_Combat.cs
	// SharedConditions_Targeting.cs (optional)
	// SharedConditions_Utility.cs (optional)
	// SharedConditions_Debug.cs
}