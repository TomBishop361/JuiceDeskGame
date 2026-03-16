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

	[Condition(name: "Shield: In Fire Range", description: "True if target is within minigun firing range band.", story: "Target is in fire range", category: "Enemy/Shield/Conditions/Combat", id: "shield.condition.combat.in_fire_range")]
	public sealed class ShieldInFireRange : Condition {
		public override bool IsTrue() {
			ShieldEnemy shieldEnemy = GameObject.GetComponent<ShieldEnemy>();

			return shieldEnemy != null && shieldEnemy.InFireRange;
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

	[Condition(name: "Shield: Can Fire Minigun", description: "True if minigun firing is allowed now (cooldown ready, not stunned/dead/slamming).", story: "Shield can fire minigun", category: "Enemy/Shield/Conditions/Combat", id: "shield.condition.combat.can_fire_minigun")]
	public sealed class ShieldCanFireMinigun : Condition {
		public override bool IsTrue() {
			ShieldEnemy shieldEnemy = GameObject.GetComponent<ShieldEnemy>();

			return shieldEnemy != null && shieldEnemy.CanFireMinigun;
		}
	}

	[Condition(name: "Shield: Has Muzzle LOS", description: "True if shield enemy muzzle has a line of sight .", story: "Shield has muzzle LOS", category: "Enemy/Shield/Conditions/Combat", id: "shield.condition.combat.has_muzzle_los")]
	public sealed class ShieldHasMuzzleLOS : Condition {
		public override bool IsTrue() {
			ShieldEnemy shieldEnemy = GameObject.GetComponent<ShieldEnemy>();

			return shieldEnemy != null && shieldEnemy.HasMuzzleLOS;
		}
	}

	[Condition(name: "Shield: Grapple Window Open", description: "True if grapple window is currently open after slam.", story: "Shield grapple window is open", category: "Enemy/Shield/Conditions/Grapple", id: "shield.condition.grapple.window_open")]
	public sealed class ShieldGrappleWindowOpen : Condition {
		public override bool IsTrue() {
			ShieldEnemy shieldEnemy = GameObject.GetComponent<ShieldEnemy>();

			return shieldEnemy != null && shieldEnemy.GrappleWindowOpen;
		}
	}

	[Condition(name: "Shield: Player In Front", description: "True if player is within the shield frontal blocking arc.", story: "Player is in front", category: "Enemy/Shield/Conditions/Defense", id: "shield.condition.defense.player_in_front")]
	public sealed class ShieldPlayerInFront : Condition {
		public override bool IsTrue() {
			ShieldEnemy shieldEnemy = GameObject.GetComponent<ShieldEnemy>();

			return shieldEnemy != null && shieldEnemy.PlayerInFront;
		}
	}

	// TODO: ADD ShieldPlayerBehind CONDITION NODE CLASS



	// Category: Enemy/Shield/Conditions

	// - SHIELD CONDITIONS ID NAMES -
	// InPunchRange -> shield.condition.combat.in_punch_range - DONE
	// InSlamRange -> shield.condition.combat.in_slam_range - DONE
	// InFireRange -> shield.condition.combat.in_fire_range - DONE
	// CanPunch -> shield.condition.combat.can_punch - DONE
	// CanSlam -> shield.condition.combat.can_slam - DONE
	// CanFire -> shield.condition.combat.can_fire_minigun - DONE
	// HasMuzzleLOS -> shield.condition.combat.has_muzzle_los - DONE - NOTE: THIS IS COMBAT LOS - RAYCAST FROM THE MINIGUN MUZZLE
	// GrappleWindowOpen -> shield.condition.grapple.window_open - DONE
	// (optional later) PlayerInFront -> shield.condition.defense.player_in_front - DONE
	// (optional later) PlayerBehind -> shield.condition.defense.player_behind

	// - SHIELD CONDITIONS CATEGORY SCRIPT NAMES -
	// ShieldConditions_Movement.cs (maybe)
	// ShieldConditions_Combat.cs
	// ShieldConditions_Grapple.cs
	// ShieldConditions_Defense.cs
}