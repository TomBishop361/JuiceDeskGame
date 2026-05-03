using Game.AI.Drone;
using Game.AI.Shield;
using Game.AI.Sword;
using UnityEngine;

namespace Game.AI {
	public enum EnemyPressureJob {
		None,
		DirectPressure,
		CutOff,
		SidePressure,
		RangedPressure,
		BacklineHarass
	}

	public enum EnemyPressureKind {
		Auto,
		Sword,
		Shield,
		Drone
	}

	public enum PressureAttackTokenType {
		SwordSwing,
		SwordLunge,
		DroneFire,
		ShieldMinigun,
		ShieldSlam
	}

	//	Registration/assignment component
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

		public EnemyPressureKind Kind => enemyKind == EnemyPressureKind.Auto ? DetectKind() : enemyKind;
		public EnemyPressureJob CurrentJob => currentJob;
		public int SlotIndex => slotIndex;
		public int SlotCount => Mathf.Max(1, slotCount);
		public EnemyAgentBase Enemy => enemy;
		public bool IsAliveAndEnabled => isActiveAndEnabled && enemy != null && enemy.IsDead == false;
		public bool HasTarget => enemy != null && enemy.HasTarget;
		public Transform Target => enemy != null ? enemy.Target : null;
		public float DistanceToTarget => enemy != null ? enemy.DistanceToTarget : Mathf.Infinity;

		private void Awake() {
			enemy = GetComponent<EnemyAgentBase>();
			if (enemyKind == EnemyPressureKind.Auto) {
				enemyKind = DetectKind();
			}
		}

		// On spawn/enable, inherit recent player awareness so newly spawned enemies do not stand idle
		private void OnEnable() {
			director = CombatPressureDirector.Active;
			if (director == null) {
				director = Object.FindFirstObjectByType<CombatPressureDirector>();
			}

			director?.Register(this);
		}

		private void OnDisable() {
			director?.Unregister(this);
		}

		public void SetAssignment(EnemyPressureJob job, int index, int count) {
			currentJob = job;
			slotIndex = Mathf.Max(0, index);
			slotCount = Mathf.Max(1, count);
		}

		// Token gate used by enemies before starting an attack
		// This prevents dogpiling
		public bool TryRequestAttackToken(PressureAttackTokenType tokenType, float duration) {
			CombatPressureDirector activeDirector = director != null ? director : CombatPressureDirector.Active;
			return activeDirector != null && activeDirector.TryRequestAttackToken(this, tokenType, duration);
		}

		public void ReleaseAttackToken(PressureAttackTokenType tokenType) {
			CombatPressureDirector activeDirector = director != null ? director : CombatPressureDirector.Active;
			activeDirector?.ReleaseAttackToken(this, tokenType);
		}

		private EnemyPressureKind DetectKind() {
			if (GetComponent<SwordEnemy>() != null) {
				return EnemyPressureKind.Sword;
			}

			if (GetComponent<ShieldEnemy>() != null) {
				return EnemyPressureKind.Shield;
			}

			if (GetComponent<DroneEnemy>() != null) {
				return EnemyPressureKind.Drone;
			}

			return EnemyPressureKind.Auto;
		}
	}
}