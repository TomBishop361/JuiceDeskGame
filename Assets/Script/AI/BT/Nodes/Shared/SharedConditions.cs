using UnityEngine;
using Unity.Behavior;

// SharedConditions.cs
namespace Game.AI.Behavior.Shared {
	// NOTE: ids must be UNIQUE across the project. Keep them stable once committed.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name

	// - Shared Core (Conditions) - 

	[Condition(name: "Enemy: Is Dead", description: "True if the enemy is dead.", story: "Enemy is dead", category: "Enemy/Shared/Conditions", id: "enemy.condition.core.is_dead")]
	public sealed class EnemyIsDead : Condition {
		public override bool IsTrue() {
			IEnemyAgent agent = GameObject.GetComponent<IEnemyAgent>();

			return agent != null && agent.IsDead;
		}
	}

	[Condition(name: "Enemy: Is Stunned", description: "True if the enemy is stunned.", story: "Enemy is stunned", category: "Enemy/Shared/Conditions", id: "enemy.condition.core.is_stunned")]
	public sealed class EnemyIsStunned : Condition {
		public override bool IsTrue() {
			IEnemyAgent agent = GameObject.GetComponent<IEnemyAgent>();

			return agent != null && agent.IsStunned;
		}
	}

	// OPTIONAL - EnemyHasTarget - OPTIONAL

	// EnemyInAttackRange

	// EnemyCanAttack

	// - SHARED CONDITIONS ID NAMES -
	// IsDead -> enemy.condition.core.is_dead - DONE
	// IsStunned -> enemy.condition.core.is_stunned
	// HasTarget -> enemy.condition.core.has_target (optional)
	// InAttackRange -> enemy.condition.combat.in_attack_range
	// CanAttack -> enemy.condition.combat.can_attack
	// TargetTooClose -> enemy.condition.combat.too_close (optional)
	// HasLOS -> enemy.condition.core.has_los (optional)

	// - SHARED CONDITIONS CATEGORY SCRIPT NAMES -
	// SharedConditions_Core.cs
	// SharedConditions_Movement.cs
	// SharedConditions_Combat.cs
	// SharedConditions_Targeting.cs (optional)
	// SharedConditions_Utility.cs (optional)
	// SharedConditions_Debug.cs

	//TBD

}