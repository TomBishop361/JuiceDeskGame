using Unity.AppUI.Core;
using UnityEngine;
using Unity.Behavior;
using Game.AI.Drone; // DroneEnemy namespace

// DroneActions.cs
namespace Game.AI.Behavior.Drone {
	// NOTE: IDs must be globally unique and should NEVER change once used in graphs.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name

	[NodeDescription(name: "Drone: Recover From Knockdown", description: "Handles knocked down recovery. Running until recovered.", story: "Drone recovers from knockdown", category: "Enemy/Drone/Actions/Core", id: "drone.action.core.recover_knockdown")]
	public sealed class DroneRecoverFromKnockdown : Action {
		private DroneEnemy droneEnemy;

		protected override Status OnStart() {
			droneEnemy = GameObject.GetComponent<DroneEnemy>();
			if (droneEnemy == null) {
				LogFailure("DroneEnemy component missing.", isError: true);
				return Status.Failure;
			}

			if (droneEnemy.IsKnockedDown == false) {
				return Status.Success;
			}

			droneEnemy.RecoverFromKnockdownTick();

			return Status.Running;
		}

		protected override Status OnUpdate() {
			if (droneEnemy == null) {
				return Status.Failure;
			}

			if (droneEnemy.IsKnockedDown == false) {
				return Status.Success;
			}

			droneEnemy.RecoverFromKnockdownTick();

			return Status.Running;
		}
	}

	[NodeDescription(name: "Drone: Move Away (Tick)", description: "Moves away from target when too close.", story: "Drone moves away from target", category: "Enemy/Drone/Actions/Flight", id: "drone.action.flight.move_away_tick")]
	public sealed class DroneMoveAwayTick : Action {
		private DroneEnemy droneEnemy;

		protected override Status OnStart() {
			droneEnemy = GameObject.GetComponent<DroneEnemy>();
			if (droneEnemy == null) {
				LogFailure("DroneEnemy component missing.", isError: true);
				return Status.Failure;
			}

			if (droneEnemy.HasTarget == false) {
				return Status.Failure;
			}

			droneEnemy.MoveAwayTick();

			//return Status.Running;
			return Status.Success; // For continuous actions (maintain range/move away) -> they should return Success so the selector re-evaluates them every frame
		}

		protected override Status OnUpdate() {
			if (droneEnemy == null) {
				return Status.Failure;
			}
			if (droneEnemy.HasTarget == false) {
				return Status.Failure;
			}

			droneEnemy.MoveAwayTick();

			//return Status.Running;
			return Status.Success; // For continuous actions (maintain range/move away) -> they should return Success so the selector re-evaluates them every frame
		}
	}

	[NodeDescription(name: "Drone: Maintain Range (Tick)", description: "Maintains a desired distance band (hover/orbit style).", story: "Drone maintains range", category: "Enemy/Drone/Actions/Flight", id: "drone.action.flight.maintain_range_tick")]
	public sealed class DroneMaintainRangeTick : Action {
		private DroneEnemy droneEnemy;

		protected override Status OnStart() {
			droneEnemy = GameObject.GetComponent<DroneEnemy>();
			if (droneEnemy == null) {
				LogFailure("DroneEnemy component missing.", isError: true);
				return Status.Failure;
			}

			if (droneEnemy.HasTarget == false) {
				return Status.Failure;
			}

			droneEnemy.MaintainRangeTick();

			//return Status.Running;
			return Status.Success; // For continuous actions (maintain range/move away) -> they should return Success so the selector re-evaluates them every frame
		}

		protected override Status OnUpdate() {
			if (droneEnemy == null) {
				return Status.Failure;
			}
			if (droneEnemy.HasTarget == false) {
				return Status.Failure;
			}

			droneEnemy.MaintainRangeTick();

			//return Status.Running;
			return Status.Success; // For continuous actions (maintain range/move away) -> they should return Success so the selector re-evaluates them every frame
		}
	}

	[NodeDescription(name: "Drone: Fire Projectile", description: "Fires a projectile and waits until firing is complete.", story: "Drone fires a projectile", category: "Enemy/Drone/Actions/Combat", id: "drone.action.combat.fire_projectile")]
	public sealed class DroneFireProjectile : Action {
		private DroneEnemy droneEnemy;

		protected override Status OnStart() {
			droneEnemy = GameObject.GetComponent<DroneEnemy>();
			if (droneEnemy == null) {
				LogFailure("DroneEnemy component missing.", isError: true);
				return Status.Failure;
			}

			bool hasStartedFiring = droneEnemy.TryStartFire();

			return hasStartedFiring ? Status.Running : Status.Failure;
		}

		protected override Status OnUpdate() {
			if (droneEnemy == null) {
				return Status.Failure;
			}

			// Running whilst the Firing animation/attack is active (Returns Success once finished)
			return droneEnemy.IsAttacking ? Status.Running : Status.Success;
		}
	}


	// TODO: ADD DroneSetAirborne ACTION NODE CLASS


	// Category: Enemy/Drone/Actions

	// NOTE: Drone could patrol when alert and then land on floor - when re-alerted SetAirborne fires

	// - DRONE ACTIONS ID NAMES -
	// RecoverFromKnockdown -> drone.action.core.recover_knockdown - DONE
	// MoveAway -> drone.action.flight.move_away_tick - DONE
	// MaintainRange -> drone.action.flight.maintain_range_tick - DONE
	// SetAirborne -> drone.action.flight.set_airborne (optional) 
	// FireProjectile -> drone.action.combat.fire_projectile - DONE

	// - DRONE ACTION CATEGORY SCRIPT NAMES -
	// DroneActions_Core.cs
	// DroneActions_Flight.cs
	// DroneActions_Combat.cs

}