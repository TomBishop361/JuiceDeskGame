using UnityEngine;
using Unity.Behavior;
using Game.AI.Shield; // ShieldEnemy namespace

// ShieldActions.cs
namespace Game.AI.Behavior.Shield {
	// NOTE: IDs must be globally unique and should NEVER change once used in graphs.
	// If nodes/conditions go missing in the add menu, it’s often an id/attribute issue.

	// - ID INFORMATION -
	//<scope>.<kind>.<domain>.<name>

	//scope = enemy(shared) or sword / shield / drone
	//kind = action or condition
	//domain = core / combat / move / defense / flight / grapple / utility
	//name = the specific node name


	[NodeDescription(name: "Shield: Advance Raised (Tick)", description: "Moves toward target with shield raised locomotion/state.", story: "Shield enemy advances with shield raised", category: "Enemy/Shield/Actions/Movement", id: "shield.action.move.advance_raised_tick")]
	public sealed class ShieldAdvanceRaisedTick : Action {
		private ShieldEnemy shieldEnemy;

		protected override Status OnStart() {
			shieldEnemy = GameObject.GetComponent<ShieldEnemy>();
			if (shieldEnemy == null) {
				LogFailure("ShieldEnemy component missing.", isError: true);
				return Status.Failure;
			}

			if (shieldEnemy.HasTarget == false) {
				return Status.Failure;
			}

			// If already in slam range, allow combat to take over
			if (shieldEnemy.InSlamRange == true) {
				return Status.Success;
			}

			shieldEnemy.ChaseTargetTick();


			return Status.Running;
		}

		protected override Status OnUpdate() {
			if (shieldEnemy == null) {
				return Status.Failure;
			}
			if (shieldEnemy.HasTarget == false) {
				return Status.Failure;
			}

			if (shieldEnemy.InSlamRange == true) {
				return Status.Success;
			}

			shieldEnemy.ChaseTargetTick();

			return Status.Running;
		}
	}

	[NodeDescription(name: "Shield: Slam Attack (Opens Grapple Window)", description: "Triggers slam attack. Opens grapple window for a duration.", story: "Shield enemy slams and opens grapple window", category: "Enemy/Shield/Actions/Combat", id: "shield.action.combat.slam_open_grapple")]
	public sealed class ShieldSlamAttackOpenGrapple : Action {
		private ShieldEnemy shieldEnemy;

		protected override Status OnStart() {
			shieldEnemy = GameObject.GetComponent<ShieldEnemy>();
			if (shieldEnemy == null) {
				LogFailure("ShieldEnemy component missing.", isError: true);
				return Status.Failure;
			}

			bool hasStartedSlamAttack = shieldEnemy.TryStartSlam();

			return hasStartedSlamAttack ? Status.Running : Status.Failure;
		}

		protected override Status OnUpdate() {
			if (shieldEnemy == null) {
				return Status.Failure;
			}

			// Running whilst the Slam animation/attack is active (Returns Success once finished)
			return shieldEnemy.IsAttacking ? Status.Running : Status.Success;
		}
	}

	[NodeDescription(name: "Shield: Slam Shockwave", description: "Triggers slam animation and expects shockwave to occur via animation event. Opens grapple window.", story: "Shield slams a shockwave", category: "Enemy/Shield/Actions/Combat", id: "shield.action.combat.slam_shockwave")]
	public sealed class ShieldSlamShockwave : Action {
		private ShieldEnemy shieldEnemy;

		protected override Status OnStart() {
			shieldEnemy = GameObject.GetComponent<ShieldEnemy>();
			if (shieldEnemy == null) {
				LogFailure("ShieldEnemy component missing.", isError: true);
				return Status.Failure;
			}

			bool hasStartedSlamShockwave = shieldEnemy.TryStartSlam();

			return hasStartedSlamShockwave ? Status.Running : Status.Failure;
		}

		protected override Status OnUpdate() {
			if (shieldEnemy == null) {
				return Status.Failure;
			}

			return shieldEnemy.IsAttacking ? Status.Running : Status.Success;
		}
	}

	[NodeDescription(name: "Shield: Fire Minigun (Tick)", description: "Fires minigun while running. Returns Running while firing is possible. Failure if cannot fire. Success if target lost.", story: "Shield fires minigun", category: "Enemy/Shield/Actions/Combat", id: "shield.action.combat.fire_minigun_tick")]
	public sealed class ShieldFireMinigunTick : Action {
		private ShieldEnemy shieldEnemy;

		protected override Status OnStart() {
			shieldEnemy = GameObject.GetComponent<ShieldEnemy>();
			if (shieldEnemy == null) {
				LogFailure("ShieldEnemy component missing.", isError: true);
				return Status.Failure;
			}

			if (shieldEnemy.HasTarget == false) {
				return Status.Failure;
			}

			// Start firing immediately (PROTOTYPE: no spin-up)
			bool canFireMinigun = shieldEnemy.TryStartMinigun();

			return canFireMinigun ? Status.Running : Status.Failure;
		}

		protected override Status OnUpdate() {
			if (shieldEnemy == null) {
				return Status.Failure;
			}
			if (shieldEnemy.HasTarget == false) {
				return Status.Failure;
			}

			bool canFireMinigun = shieldEnemy.TryStartMinigun();

			return canFireMinigun ? Status.Running : Status.Success; //RETRUN SUCCESS?
		}

		protected override void OnEnd() {
			if (shieldEnemy != null) {
				shieldEnemy.StopMinigunFiring();
			}
		}
	}

	[NodeDescription(name: "Shield: Set Shield Raised", description: "Enables/disables shield raised state (and bullet blocking collider).", story: "Shield is raised set to [Raised]", category: "Enemy/Shield/Actions/Defense", id: "shield.action.defense.set_shield_raised")]
	public sealed class ShieldSetShieldRaised : Action {
		[SerializeReference] public BlackboardVariable<bool> Raised;

		private ShieldEnemy shieldEnemy;

		protected override Status OnStart() {
			shieldEnemy = GameObject.GetComponent<ShieldEnemy>();
			if (shieldEnemy == null) {
				LogFailure("ShieldEnemy component missing.", isError: true);
				return Status.Failure;
			}

			shieldEnemy.SetShieldRaised(Raised);

			return Status.Success;
		}
	}

	[NodeDescription(name: "Shield: Enter Exposed State", description: "Makes the shield enemy vulnerable for a duration.", story: "Shield enemy becomes exposed for [Duration] seconds", category: "Enemy/Shield/Actions/Defense", id: "shield.action.defense.enter_exposed_state")]
	public sealed class ShieldEnterExposedState : Action {
		[SerializeReference] public BlackboardVariable<float> Duration;

		private ShieldEnemy shieldEnemy;

		protected override Status OnStart() {
			shieldEnemy = GameObject.GetComponent<ShieldEnemy>();
			if (shieldEnemy == null) {
				LogFailure("ShieldEnemy component missing.", isError: true);
				return Status.Failure;
			}

			shieldEnemy.EnterExposedState(Duration);
			return Status.Success;
		}
	}

	// Category: Enemy/Shield/Actions

	// - SHIELD ACTIONS ID NAMES -
	// AdvanceRaised -> shield.action.move.advance_raised - DONE
	// Punch -> shield.action.combat.punch - DONE
	// Slam -> shield.action.combat.slam - DONE
	// SlamShockwave -> shield.action.combat.slam_shockwave - DONE
	// FireMinigun -> shield.action.combat.fire_minigun_tick - DONE
	// OpenGrappleWindow (if separate) -> shield.action.grapple.open_window - DONE
	// CloseGrappleWindow -> shield.action.grapple.close_window
	// ShieldRaised -> shield.action.defense.set_shield_raised - DONE
	// BlockReact -> shield.action.defense.block_react (optional later) // SAME AS SHIELD RAISED (BLOCKS)

	// - SHIELD ACTION CATEGORY SCRIPT NAMES -
	// ShieldActions_Movement.cs (chase but plays 'raised shield' locomotion
	// ShieldActions_Combat.cs
	// ShieldActions_Grapple.cs
	// ShieldActions_Defense.cs
}
