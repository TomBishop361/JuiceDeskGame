using Game.AI.Drone;
using Unity.Behavior;
using UnityEngine;


// DEPRECATED

namespace Game.AI.Behavior.Drone {
	// NOTE: IDs must be globally unique and should NEVER change once used in graphs.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name
	[NodeDescription(name: "Drone: Orbit Target (Tick)", description: "Uses FlightEnemyMotor orbit motion while facing the target.", story: "Drone enemy orbits target", category: "Enemy/Drone/Actions/Flight", id: "drone.action.flight.orbit_target_tick")]
	public sealed class DroneOrbitTargetTick : Action {
		[SerializeReference] public BlackboardVariable<float> OrbitDirection;

		private DroneEnemy droneEnemy;
		private FlightEnemyMotor flightMotor;

		protected override Status OnStart() {
			droneEnemy = GameObject.GetComponent<DroneEnemy>();
			flightMotor = GameObject.GetComponent<FlightEnemyMotor>();

			if (droneEnemy == null || flightMotor == null) {
				LogFailure("DroneEnemy or FlightEnemyMotor missing.", isError: true);
				return Status.Failure;
			}

			return TickOrbit();
		}

		protected override Status OnUpdate() {
			return TickOrbit();
		}

		private Status TickOrbit() {
			if (droneEnemy == null || flightMotor == null || droneEnemy.HasTarget == false || droneEnemy.Target == null) {
				return Status.Failure;
			}

			if (droneEnemy.IsDead || droneEnemy.IsStunned || droneEnemy.IsKnockedDown || droneEnemy.IsAttacking) {
				return Status.Failure;
			}

			float direction = OrbitDirection != null ? OrbitDirection.Value : 1.0f;
			flightMotor.Orbit(droneEnemy.Target, Mathf.Approximately(direction, 0.0f) ? 1.0f : direction);
			return Status.Running;
		}
	}

	[NodeDescription(name: "Drone: Hold Position Facing Target (Tick)", description: "Stops translation but keeps drone facing the target or memory point.", story: "Drone enemy holds position facing target", category: "Enemy/Drone/Actions/Flight", id: "drone.action.flight.hold_facing_target_tick")]
	public sealed class DroneHoldFacingTargetTick : Action {
		private DroneEnemy droneEnemy;
		private FlightEnemyMotor flightMotor;
		private EnemyBlackboard blackboard;

		private float holdAnchorY;

		protected override Status OnStart() {
			droneEnemy = GameObject.GetComponent<DroneEnemy>();
			flightMotor = GameObject.GetComponent<FlightEnemyMotor>();
			blackboard = GameObject.GetComponent<EnemyBlackboard>();

			if (flightMotor == null || blackboard == null) {
				LogFailure("FlightEnemyMotor or EnemyBlackboard missing.", isError: true);
				return Status.Failure;
			}

			holdAnchorY = GameObject.transform.position.y;

			return Status.Running;
		}

		protected override Status OnUpdate() {
			if (flightMotor == null || blackboard == null) {
				return Status.Failure;
			}

			Vector3 focus = blackboard.Target != null ? blackboard.Target.position :
				blackboard.HasLastSeenPosition ? blackboard.LastSeenPosition :
				GameObject.transform.position + GameObject.transform.forward;

			//flightMotor.HoldPosition(focus, GameObject.transform.position.y);
			flightMotor.HoldPosition(focus, holdAnchorY);
			return droneEnemy != null && (droneEnemy.IsDead || droneEnemy.IsKnockedDown) ? Status.Failure : Status.Running;
		}
	}
}
