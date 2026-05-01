using Unity.Behavior;
using UnityEngine;

// TacticalSharedActions.cs
namespace Game.AI.Behavior.Shared {
	// NOTE: IDs must be globally unique and should NEVER change once used in graphs.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name

	[NodeDescription(name: "Enemy: Compute Intercept Destination", description: "Uses fair route prediction and tactical anchors to choose an intercept destination.", story: "Enemy computes intercept destination", category: "Enemy/Shared/Actions/Tactics", id: "enemy.action.tactics.compute_intercept_destination")]
	public sealed class EnemyComputeInterceptDestination : Action {
		private EnemyBlackboard blackboard;
		private EnemyRoutePredictor predictor;

		protected override Status OnStart() {
			blackboard = GameObject.GetComponent<EnemyBlackboard>();
			predictor = GameObject.GetComponent<EnemyRoutePredictor>();

			if (blackboard == null || predictor == null) {
				LogFailure("EnemyBlackboard or EnemyRoutePredictor missing.", isError: true);
				return Status.Failure;
			}

			if (predictor.TryBuildDestination(EnemyTacticalRole.Interceptor, out Vector3 destination) == false) {
				return Status.Failure;
			}

			blackboard.SetTacticalDestination(destination);
			return Status.Success;
		}
	}

	[NodeDescription(name: "Enemy: Compute Flank Destination", description: "Chooses a side-lane/flank destination using tactical anchors or a fair fallback.", story: "Enemy computes flank destination", category: "Enemy/Shared/Actions/Tactics", id: "enemy.action.tactics.compute_flank_destination")]
	public sealed class EnemyComputeFlankDestination : Action {
		private EnemyBlackboard blackboard;
		private EnemyRoutePredictor predictor;

		protected override Status OnStart() {
			blackboard = GameObject.GetComponent<EnemyBlackboard>();
			predictor = GameObject.GetComponent<EnemyRoutePredictor>();

			if (blackboard == null || predictor == null) {
				LogFailure("EnemyBlackboard or EnemyRoutePredictor missing.", isError: true);
				return Status.Failure;
			}

			if (predictor.TryBuildDestination(EnemyTacticalRole.Flanker, out Vector3 destination) == false) {
				return Status.Failure;
			}

			blackboard.SetTacticalDestination(destination);
			return Status.Success;
		}
	}

	[NodeDescription(name: "Enemy: Compute Anchor Destination", description: "Chooses a blocking/anchor point from tactical anchors or a fair fallback.", story: "Enemy computes anchor destination", category: "Enemy/Shared/Actions/Tactics", id: "enemy.action.tactics.compute_anchor_destination")]
	public sealed class EnemyComputeAnchorDestination : Action {
		private EnemyBlackboard blackboard;
		private EnemyRoutePredictor predictor;

		protected override Status OnStart() {
			blackboard = GameObject.GetComponent<EnemyBlackboard>();
			predictor = GameObject.GetComponent<EnemyRoutePredictor>();

			if (blackboard == null || predictor == null) {
				LogFailure("EnemyBlackboard or EnemyRoutePredictor missing.", isError: true);
				return Status.Failure;
			}

			if (predictor.TryBuildDestination(EnemyTacticalRole.Anchor, out Vector3 destination) == false) {
				return Status.Failure;
			}

			blackboard.SetTacticalDestination(destination);
			return Status.Success;
		}
	}

	[NodeDescription(name: "Enemy: Move To Tactical Destination (Tick)", description: "Moves toward the blackboard tactical destination. Returns Success when arrived.", story: "Enemy moves to tactical destination", category: "Enemy/Shared/Actions/Movement", id: "enemy.action.move.to_tactical_destination_tick")]
	public sealed class EnemyMoveToTacticalDestinationTick : Action {
		private EnemyBlackboard blackboard;
		private EnemyTacticalMover mover;
		private IEnemyAgent agent;

		protected override Status OnStart() {
			blackboard = GameObject.GetComponent<EnemyBlackboard>();
			mover = GameObject.GetComponent<EnemyTacticalMover>();
			agent = GameObject.GetComponent<IEnemyAgent>();

			if (blackboard == null || mover == null) {
				LogFailure("EnemyBlackboard or EnemyTacticalMover missing.", isError: true);
				return Status.Failure;
			}

			return TickMove();
		}

		protected override Status OnUpdate() {
			return TickMove();
		}

		protected override void OnEnd() {
			if (agent != null && (agent.IsDead || agent.IsStunned)) {
				agent.StopMove();
			}
		}

		private Status TickMove() {
			if (blackboard == null || mover == null || blackboard.HasTacticalDestination == false) {
				return Status.Failure;
			}

			if (agent != null && (agent.IsDead || agent.IsStunned || agent.IsAttacking)) {
				return Status.Failure;
			}

			Transform focus = blackboard.Target;
			bool arrived = mover.TickMoveTo(blackboard.TacticalDestination, focus);
			return arrived ? Status.Success : Status.Running;
		}
	}

	[NodeDescription(name: "Enemy: Investigate Suspicion (Tick)", description: "Moves to suspicious noise first, otherwise last-seen position. Clears noise after arrival.", story: "Enemy investigates suspicion", category: "Enemy/Shared/Actions/Perception", id: "enemy.action.perception.investigate_suspicion_tick")]
	public sealed class EnemyInvestigateSuspicionTick : Action {
		private EnemyBlackboard blackboard;
		private EnemyTacticalMover mover;
		private IEnemyAgent agent;
		private Vector3 destination;

		protected override Status OnStart() {
			blackboard = GameObject.GetComponent<EnemyBlackboard>();
			mover = GameObject.GetComponent<EnemyTacticalMover>();
			agent = GameObject.GetComponent<IEnemyAgent>();

			if (blackboard == null || mover == null) {
				LogFailure("EnemyBlackboard or EnemyTacticalMover missing.", isError: true);
				return Status.Failure;
			}

			if (blackboard.HasSuspiciousNoise) {
				destination = blackboard.SuspiciousNoisePosition;
			}
			else if (blackboard.HasLastSeenPosition) {
				destination = blackboard.LastSeenPosition;
			}
			else {
				return Status.Failure;
			}

			blackboard.SetTacticalDestination(destination);
			return TickInvestigate();
		}

		protected override Status OnUpdate() {
			return TickInvestigate();
		}

		protected override void OnEnd() {
			if (agent != null && (agent.IsDead || agent.IsStunned)) {
				agent.StopMove();
			}
		}

		private Status TickInvestigate() {
			if (blackboard == null || mover == null || agent == null) {
				return Status.Failure;
			}

			if (agent.IsDead || agent.IsStunned || agent.IsAttacking) {
				return Status.Failure;
			}

			bool arrived = mover.TickMoveTo(destination, destination);
			if (arrived) {
				if (blackboard.HasSuspiciousNoise) {
					blackboard.ClearSuspiciousNoise();
				}

				return Status.Success;
			}

			return Status.Running;
		}
	}

	[NodeDescription(name: "Enemy: Search Around Memory (Tick)", description: "Briefly holds/searches near the last investigated point before giving up.", story: "Enemy searches around memory for [Duration] seconds", category: "Enemy/Shared/Actions/Perception", id: "enemy.action.perception.search_memory_tick")]
	public sealed class EnemySearchAroundMemoryTick : Action {
		[SerializeReference] public BlackboardVariable<float> Duration;

		private EnemyBlackboard blackboard;
		private EnemyTacticalMover mover;
		private float endTime;
		private Vector3 focusPoint;

		protected override Status OnStart() {
			blackboard = GameObject.GetComponent<EnemyBlackboard>();
			mover = GameObject.GetComponent<EnemyTacticalMover>();

			if (blackboard == null || mover == null || blackboard.HasLastSeenPosition == false) {
				return Status.Failure;
			}

			float duration = Duration != null ? Duration.Value : 1.5f;
			endTime = Time.time + Mathf.Max(0.1f, duration);
			focusPoint = blackboard.LastSeenPosition;

			return Status.Running;
		}

		protected override Status OnUpdate() {
			if (blackboard == null || mover == null) {
				return Status.Failure;
			}

			mover.Stop(focusPoint);
			return Time.time >= endTime ? Status.Success : Status.Running;
		}
	}

	[NodeDescription(name: "Enemy: Face Known Target", description: "Faces the live target if present, otherwise faces last seen/suspicious memory.", story: "Enemy faces known target", category: "Enemy/Shared/Actions/Movement", id: "enemy.action.move.face_known_target")]
	public sealed class EnemyFaceKnownTarget : Action {
		private GroundEnemyMotor groundMotor;
		private FlightEnemyMotor flightMotor;
		private EnemyBlackboard blackboard;

		protected override Status OnStart() {
			groundMotor = GameObject.GetComponent<GroundEnemyMotor>();
			flightMotor = GameObject.GetComponent<FlightEnemyMotor>();
			blackboard = GameObject.GetComponent<EnemyBlackboard>();

			if (blackboard == null) {
				return Status.Failure;
			}

			Vector3 focus = GetFocusPoint();
			groundMotor?.FaceTarget(focus);
			flightMotor?.FaceTarget(focus);

			return Status.Success;
		}

		private Vector3 GetFocusPoint() {
			if (blackboard.Target != null) {
				return blackboard.Target.position;
			}

			if (blackboard.HasLastSeenPosition) {
				return blackboard.LastSeenPosition;
			}

			if (blackboard.HasSuspiciousNoise) {
				return blackboard.SuspiciousNoisePosition;
			}

			return GameObject.transform.position + GameObject.transform.forward;
		}
	}

	[NodeDescription(name: "Enemy: Clear Tactical Destination", description: "Clears tactical destination memory.", story: "Enemy clears tactical destination", category: "Enemy/Shared/Actions/Tactics", id: "enemy.action.tactics.clear_destination")]
	public sealed class EnemyClearTacticalDestination : Action {
		protected override Status OnStart() {
			EnemyBlackboard blackboard = GameObject.GetComponent<EnemyBlackboard>();
			if (blackboard == null) {
				return Status.Failure;
			}

			blackboard.ClearTacticalDestination();
			return Status.Success;
		}
	}

	[NodeDescription(name: "Enemy: Clear Suspicion", description: "Clears suspicious noise memory.", story: "Enemy clears suspicion", category: "Enemy/Shared/Actions/Perception", id: "enemy.action.perception.clear_suspicion")]
	public sealed class EnemyClearSuspicion : Action {
		protected override Status OnStart() {
			EnemyBlackboard blackboard = GameObject.GetComponent<EnemyBlackboard>();
			if (blackboard == null) {
				return Status.Failure;
			}

			blackboard.ClearSuspiciousNoise();
			return Status.Success;
		}
	}
}
