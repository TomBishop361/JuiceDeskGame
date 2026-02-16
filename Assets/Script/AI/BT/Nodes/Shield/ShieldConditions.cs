using UnityEngine;
using Unity.Behavior;
using Game.AI.Shield; // ShieldEnemy namespace

// ShieldConditions.cs
namespace Game.AI.Behavior.Shield {
	// NOTE: IDs must be globally unique and should NEVER change once used in graphs.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//com.juicedesk.projectark.<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name


	[Condition(name: "Shield: In Punch Range", description: "True if target is within punch range.", story: "Target is in punch range", category: "Enemy/Shield/Conditions/Combat", id: "shield.condition.combat.in_punch_range")]
	public sealed class ShieldInPunchRange : Condition {
		public override bool IsTrue() {
			ShieldEnemy shieldEnemy = GameObject.GetComponent<ShieldEnemy>();

			return shieldEnemy != null && shieldEnemy.InPunchRange;
		}
	}

	[Condition(name: "Shield: In Slam Range", description: "True if target is within slam range.", story: "Target is in slam range", category: "Enemy/Shield/Conditions/Combat", id: "shield.condition.combat.in_slam_range")]
	public sealed class ShieldInSlamRange : Condition {
		public override bool IsTrue() {
			ShieldEnemy shieldEnemy = GameObject.GetComponent<ShieldEnemy>();

			return shieldEnemy != null && shieldEnemy.InSlamRange;
		}
	}

	[Condition(name: "Shield: Can Punch", description: "True if punch cooldown is ready and enemy can punch now.", story: "Shield enemy can punch", category: "Enemy/Shield/Conditions/Combat", id: "shield.condition.combat.can_punch")]
	public sealed class ShieldCanPunch : Condition {
		public override bool IsTrue() {
			ShieldEnemy shieldEnemy = GameObject.GetComponent<ShieldEnemy>();

			return shieldEnemy != null && shieldEnemy.CanPunch;
		}
	}

	[Condition(name: "Shield: Can Slam", description: "True if slam cooldown is ready and enemy can slam now.", story: "Shield enemy can slam", category: "Enemy/Shield/Conditions/Combat", id: "shield.condition.combat.can_slam")]
	public sealed class ShieldCanSlam : Condition {
		public override bool IsTrue() {
			ShieldEnemy shieldEnemy = GameObject.GetComponent<ShieldEnemy>();

			return shieldEnemy != null && shieldEnemy.CanSlam;
		}
	}

	[Condition(name: "Shield: Grapple Window Open", description: "True if grapple window is currently open after slam.", story: "Shield grapple window is open", category: "Enemy/Shield/Conditions/Grapple", id: "shield.condition.grapple.window_open")]
	public sealed class ShieldGrappleWindowOpen : Condition {
		public override bool IsTrue() {
			ShieldEnemy shieldEnemy = GameObject.GetComponent<ShieldEnemy>();

			return shieldEnemy != null && shieldEnemy.GrappleWindowOpen;
		}
	}

	// TODO: ADD ShieldPlayerInFront CONDITION NODE CLASS
	// TODO: ADD ShieldPlayerBehind CONDITION NODE CLASS



	// Category: Enemy/Shield/Conditions

	// - SHIELD CONDITIONS ID NAMES -
	// InPunchRange -> shield.condition.combat.in_punch_range - DONE
	// InSlamRange -> shield.condition.combat.in_slam_range - DONE
	// CanPunch -> shield.condition.combat.can_punch - DONE
	// CanSlam -> shield.condition.combat.can_slam - DONE
	// GrappleWindowOpen -> shield.condition.grapple.window_open - DONE
	// (optional later) PlayerInFront -> shield.condition.defense.player_in_front
	// (optional later) PlayerBehind -> shield.condition.defense.player_behind

	// - SHIELD CONDITIONS CATEGORY SCRIPT NAMES -
	// ShieldConditions_Movement.cs (maybe)
	// ShieldConditions_Combat.cs
	// ShieldConditions_Grapple.cs
	// ShieldConditions_Defense.cs
}