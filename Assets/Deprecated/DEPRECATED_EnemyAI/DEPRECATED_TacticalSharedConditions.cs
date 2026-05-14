using Unity.Behavior;
using UnityEngine;


// DEPRECATED


// TacticalSharedConditions.cs
namespace Game.AI.Behavior.Shared {
	// NOTE: IDs must be globally unique and should NEVER change once used in graphs.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name

	[Condition(name: "Enemy: Has Line Of Sight", description: "True if this enemy currently has confirmed LOS to the target.", story: "Enemy has line of sight", category: "Enemy/Shared/Conditions/Perception", id: "enemy.condition.perception.line_of_sight")]
	public sealed class EnemyHasLineOfSight : Condition {
		public override bool IsTrue() {
			EnemyBlackboard blackboard = GameObject.GetComponent<EnemyBlackboard>();
			return blackboard != null && blackboard.HasLineOfSight;
		}
	}

	[Condition(name: "Enemy: Has Last Seen Position", description: "True if this enemy has a remembered or shared target position.", story: "Enemy has last seen position", category: "Enemy/Shared/Conditions/Perception", id: "enemy.condition.perception.last_seen")]
	public sealed class EnemyHasLastSeenPosition : Condition {
		public override bool IsTrue() {
			EnemyBlackboard blackboard = GameObject.GetComponent<EnemyBlackboard>();
			return blackboard != null && blackboard.HasLastSeenPosition;
		}
	}

	[Condition(name: "Enemy: Has Fresh Last Seen Position", description: "True if last seen memory is younger than Max Age.", story: "Enemy has fresh last seen position within [MaxAge] seconds", category: "Enemy/Shared/Conditions/Perception", id: "enemy.condition.perception.fresh_last_seen")]
	public sealed class EnemyHasFreshLastSeenPosition : Condition {
		[SerializeReference] public BlackboardVariable<float> MaxAge;

		public override bool IsTrue() {
			EnemyBlackboard blackboard = GameObject.GetComponent<EnemyBlackboard>();
			float maxAge = MaxAge != null ? MaxAge.Value : 2.0f;
			return blackboard != null && blackboard.HasFreshSighting(maxAge);
		}
	}

	[Condition(name: "Enemy: Has Suspicious Noise", description: "True if this enemy has heard or received a suspicious noise.", story: "Enemy has suspicious noise", category: "Enemy/Shared/Conditions/Perception", id: "enemy.condition.perception.suspicious_noise")]
	public sealed class EnemyHasSuspiciousNoise : Condition {
		public override bool IsTrue() {
			EnemyBlackboard blackboard = GameObject.GetComponent<EnemyBlackboard>();
			return blackboard != null && blackboard.HasSuspiciousNoise;
		}
	}

	[Condition(name: "Enemy: Has Fresh Suspicious Noise", description: "True if suspicious noise memory is younger than Max Age.", story: "Enemy heard suspicious noise within [MaxAge] seconds", category: "Enemy/Shared/Conditions/Perception", id: "enemy.condition.perception.noise_fresh")]
	public sealed class EnemyHasFreshSuspiciousNoise : Condition {
		[SerializeReference] public BlackboardVariable<float> MaxAge;

		public override bool IsTrue() {
			EnemyBlackboard blackboard = GameObject.GetComponent<EnemyBlackboard>();
			float maxAge = MaxAge != null ? MaxAge.Value : 4.0f;
			return blackboard != null && blackboard.HasFreshSuspiciousNoise(maxAge);
		}
	}

	[Condition(name: "Enemy: Tactical Role Is", description: "True if the squad director assigned the selected role.", story: "Enemy tactical role is [Role]", category: "Enemy/Shared/Conditions/Tactics", id: "enemy.condition.tactics.role_is")]
	public sealed class EnemyTacticalRoleIs : Condition {
		[SerializeReference] public BlackboardVariable<EnemyTacticalRole> Role;

		public override bool IsTrue() {
			EnemyBlackboard blackboard = GameObject.GetComponent<EnemyBlackboard>();
			EnemyTacticalRole expectedRole = Role != null ? Role.Value : EnemyTacticalRole.None;
			return blackboard != null /*&& blackboard.TacticalRole == expectedRole*/;
		}
	}

	[Condition(name: "Enemy: Has Tactical Destination", description: "True if a tactical destination has been computed.", story: "Enemy has tactical destination", category: "Enemy/Shared/Conditions/Tactics", id: "enemy.condition.tactics.has_destination")]
	public sealed class EnemyHasTacticalDestination : Condition {
		public override bool IsTrue() {
			EnemyBlackboard blackboard = GameObject.GetComponent<EnemyBlackboard>();
			return blackboard != null /*&& blackboard.HasTacticalDestination*/;
		}
	}

	[Condition(name: "Enemy: Can Attempt Intercept", description: "True if route prediction fairness gates allow this enemy to intercept.", story: "Enemy can attempt intercept", category: "Enemy/Shared/Conditions/Tactics", id: "enemy.condition.tactics.can_intercept")]
	public sealed class EnemyCanAttemptIntercept : Condition {
		public override bool IsTrue() {
			//EnemyRoutePredictor predictor = GameObject.GetComponent<EnemyRoutePredictor>();
			return true/*predictor != null && predictor.CanAttemptIntercept()*/;
		}
	}

	[Condition(name: "Enemy: Finite State Is", description: "True if the small FSM is in the selected state.", story: "Enemy finite state is [State]", category: "Enemy/Shared/Conditions/Core", id: "enemy.condition.core.finite_state_is")]
	public sealed class EnemyFiniteStateIs : Condition {
		[SerializeReference] public BlackboardVariable<EnemyFiniteState> State;

		public override bool IsTrue() {
			EnemyFSM stateMachine = GameObject.GetComponent<EnemyFSM>();
			EnemyFiniteState expectedState = State != null ? State.Value : EnemyFiniteState.Alive;
			return stateMachine != null && stateMachine.CurrentState == expectedState;
		}
	}
}