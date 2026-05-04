using Game.AI.Drone;
using Game.AI.Shield;
using Game.AI.Sword;
using UnityEngine;

namespace Game.AI {
	// Role assigned to an enemy by the CombatPressureDirector
	// Pressure Controllers use this value to decide how aggressively or indirectly they should pressure the player
	public enum EnemyPressureJob {
		None,
		DirectPressure,
		CutOff,
		SidePressure,
		RangedPressure,
		BacklineHarass
	}

	// Enemy category used by the pressure system
	// Auto lets the component detect the category from the enemy pressure controller attached to the same GameObject
	public enum EnemyPressureKind {
		Auto,
		Sword,
		Shield,
		Drone
	}

	// Attack token categories used to limit simultaneous high-pressure attacks
	// The director tracks each token type separately so, for example, drone fire and sword swings can have different limits
	public enum PressureAttackTokenType {
		SwordSwing,
		SwordLunge,
		DroneFire,
		ShieldMinigun,
		ShieldSlam
	}

	//	Registration/assignment component for enemies that participate in the pressure system
	// Add this to Sword + Shield + Drone prefabs
	// This component is the bridge between an enemy prefab and the pressure director
	// It stores the current job/slot so enemy-specific controllers stay simple
	[DisallowMultipleComponent]
	public sealed class EnemyPressureAgent : MonoBehaviour {
		[Header("Identity")]
		[Tooltip("Enemy type used by the pressure director. Auto detects from SwordEnemy, ShieldEnemy, or DroneEnemy.")]
		[SerializeField] private EnemyPressureKind enemyKind = EnemyPressureKind.Auto;

		[Header("Runtime")]
		[Tooltip("Runtime pressure job assigned by the director. Usually read-only during play.")]
		[SerializeField] private EnemyPressureJob currentJob = EnemyPressureJob.None;
		[Tooltip("Runtime slot index used for side positions and drone orbit slots. Usually assigned by the director.")]
		[SerializeField] private int slotIndex;
		[Tooltip("Runtime number of slots in this enemy group. Usually assigned by the director.")]
		[SerializeField] private int slotCount = 1;

		private EnemyAgentBase enemy;
		private CombatPressureDirector director;

		// Resolved enemy kind
		public EnemyPressureKind Kind => enemyKind == EnemyPressureKind.Auto ? DetectKind() : enemyKind;

		// Current pressure assignment data read by enemy pressure controllers
		public EnemyPressureJob CurrentJob => currentJob;
		public int SlotIndex => slotIndex;
		public int SlotCount => Mathf.Max(1, slotCount);

		// Accessors for the underlying enemy state
		public EnemyAgentBase Enemy => enemy;
		public bool IsAliveAndEnabled => isActiveAndEnabled && enemy != null && enemy.IsDead == false;
		public bool HasTarget => enemy != null && enemy.HasTarget;
		public Transform Target => enemy != null ? enemy.Target : null;
		public float DistanceToTarget => enemy != null ? enemy.DistanceToTarget : Mathf.Infinity;

		private void Awake() {
			// Cache the base enemy component so the director does not need to know about each specific enemy type
			enemy = GetComponent<EnemyAgentBase>();

			// Lock in the detected type once at startup when the inspector is set to Auto
			if (enemyKind == EnemyPressureKind.Auto) {
				enemyKind = DetectKind();
			}
		}

		private void OnEnable() {
			// Find the active pressure director and register this enemy when it spawns or becomes enabled.
			director = CombatPressureDirector.Active;
			if (director == null) {
				director = FindFirstObjectByType<CombatPressureDirector>();
			}

			director?.Register(this);
		}

		private void OnDisable() {
			// Unregister so disabled or destroyed enemies stop receiving jobs and release any held tokens
			director?.Unregister(this);
		}

		// Store the role and slot assigned by the director
		// Clamp values so pressure controllers never see invalid slot data
		public void SetAssignment(EnemyPressureJob job, int index, int count) {
			currentJob = job;
			slotIndex = Mathf.Max(0, index);
			slotCount = Mathf.Max(1, count);
		}

		// Token gate used by enemies before starting an attack
		// This prevents dogpiling by asking the director whether another attack of this type is allowed right now
		public bool TryRequestAttackToken(PressureAttackTokenType tokenType, float duration) {
			// Prefer the cached director but fall back to the static active director if this agent was enabled before the director existed
			CombatPressureDirector activeDirector = director != null ? director : CombatPressureDirector.Active;
			return activeDirector != null && activeDirector.TryRequestAttackToken(this, tokenType, duration);
		}

		public void ReleaseAttackToken(PressureAttackTokenType tokenType) {
			// Release a token when the attack ends early so another enemy can use that attack slot
			CombatPressureDirector activeDirector = director != null ? director : CombatPressureDirector.Active;
			activeDirector?.ReleaseAttackToken(this, tokenType);
		}

		private EnemyPressureKind DetectKind() {
			// Detect the pressure kind from the pressure controller attached to this prefab
			if (GetComponent<SwordEnemy>() != null) {
				return EnemyPressureKind.Sword;
			}

			if (GetComponent<ShieldEnemy>() != null) {
				return EnemyPressureKind.Shield;
			}

			if (GetComponent<DroneEnemy>() != null) {
				return EnemyPressureKind.Drone;
			}

			// Return Auto if no known enemy pressure controller is found
			// This makes missing setups more obvious
			return EnemyPressureKind.Auto;
		}
	}
}