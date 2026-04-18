using UnityEngine;

namespace Game.AI.Sword {
	// Handles the sword enemy's normal melee swing, including cooldown checks + swing range validation + hitbox setup
	// + active-frame hitbox control
	[DisallowMultipleComponent]
	public sealed class SwordMeleeCombat : MonoBehaviour {
		[Header("Hitbox")]
		[Tooltip("Hitbox used for the sword enemy's normal swing attack.")]
		[SerializeField] private MeleeHitbox swingHitbox;

		[Header("Damage")]
		[Tooltip("Attack data applied by the swing hitbox.")]
		[SerializeField] private AttackData swingAttackData = new AttackData();

		[Header("Range")]
		[Tooltip("Maximum target distance allowed for the normal swing attack.")]
		[SerializeField] private float swingRange = 1.8f;

		[Header("Timing")]
		[Tooltip("Cooldown after performing a swing attack.")]
		[SerializeField] private float swingCooldown = 1.0f;
		[Tooltip("Minimum time the enemy remains attack-locked after starting the swing.")]
		[SerializeField] private float swingLockTime = 0.55f;

		[Header("Animation")]
		[Tooltip("Animator trigger name used to start the swing animation.")]
		[SerializeField] private string swingTriggerName = "Swing";

		// Sword swing attack cooldown/state timers

		// Time when the next swing becomes available
		private float nextSwingTime = -Mathf.Infinity;
		private int swingTriggerHash;

		// Sword Enemy Swing Attack Specific Properties

		// Maximum valid distance for the normal swing attack
		public float SwingRange => swingRange;

		// Caches the swing trigger hash + prepares the swing hitbox with attack data
		private void Awake() {
			swingTriggerHash = Animator.StringToHash(swingTriggerName);

			if (swingHitbox != null) {
				swingHitbox.Initialise(swingAttackData);
				swingHitbox.Disable();
			}
		}

		// Clears swing cooldown state and ensures all melee hitboxes are disabled
		public void ResetRuntime() {
			nextSwingTime = -Mathf.Infinity;
			DisableAllHitboxes();
		}

		// Returns true if the supplied distance is inside swing range
		public bool InRange(float distanceToTarget) {
			return distanceToTarget <= swingRange;
		}

		// Returns true if the owner is allowed to begin a swing right now
		public bool CanSwing(SwordEnemy owner) {
			return owner != null
				&& owner.IsDead == false
				&& owner.IsStunned == false
				&& owner.IsAttacking == false
				&& Time.time >= nextSwingTime;
		}

		// Attempts to start the swing attack by setting cooldowns + attack lock timing +the swing animation trigger
		public bool TryStartSwing(SwordEnemy owner, Animator animator) {
			if (owner == null || CanSwing(owner) == false || InRange(owner.DistanceToTarget) == false) {
				return false;
			}

			nextSwingTime = Time.time + swingCooldown;
			owner.BeginAttackLock(swingLockTime);

			SetupSwingData();
			DisableAllHitboxes();

			if (animator != null) {
				animator.SetTrigger(swingTriggerHash);
			}

			return true;
		}

		// Reinitialises swing attack data before the swing active frames begin
		public void SetupSwingData() {
			if (swingHitbox != null) {
				swingHitbox.Initialise(swingAttackData);
			}
		}

		// Enables the swing hitbox during the active damage frames
		public void EnableSwingHitbox() {
			if (swingHitbox != null) {
				swingHitbox.Enable();
			}
		}

		// Disables the swing hitbox after the active damage frames end
		public void DisableSwingHitbox() {
			if (swingHitbox != null) {
				swingHitbox.Disable();
			}
		}

		// Disables all melee hitboxes owned by this module
		public void DisableAllHitboxes() {
			DisableSwingHitbox();
		}
	}
}