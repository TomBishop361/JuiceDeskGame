using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Game.AI.Sword {
	// Handles the sword enemy's lunge attack, including cooldown checks + windup + dash direction setup + hitbox control
	// + lunge cancellation
	[DisallowMultipleComponent]
	public sealed class SwordLungeAttack : MonoBehaviour {
		[Header("References")]
		[Tooltip("Hitbox used during the lunge attack active frames.")]
		[SerializeField] private MeleeHitbox lungeHitbox;
		[Tooltip("Rigidbody used to drive the physical lunge dash.")]
		[SerializeField] private Rigidbody lungeRB;

		[Header("Damage")]
		[Tooltip("Attack data applied by the lunge hitbox.")]
		[SerializeField] private AttackData lungeAttackData = new AttackData();

		[Header("Range")]
		[Tooltip("Minimum distance required before the sword enemy can start a lunge.")]
		[SerializeField] private float lungeMinRange = 2.8f;
		[Tooltip("Maximum distance at which the sword enemy can still start a lunge.")]
		[SerializeField] private float lungeMaxRange = 6.5f;

		[Header("Timing")]
		[Tooltip("Cooldown after a completed or attempted lunge.")]
		[SerializeField] private float lungeCooldown = 2.2f;
		[Tooltip("Minimum time the enemy remains attack-locked after starting the lunge.")]
		[SerializeField] private float lungeLockTime = 0.75f;
		[Tooltip("Windup time before the lunge dash begins.")]
		[SerializeField] private float lungeWindupTime = 0.20f;
		[Tooltip("Duration of the active lunge dash movement.")]
		[SerializeField] private float lungeDuration = 0.22f;
		[Tooltip("Recovery time after the lunge dash has finished.")]
		[SerializeField] private float lungeRecoveryTime = 0.4f;

		[Header("Movement")]
		[Tooltip("Velocity-change force applied when the lunge dash launches.")]
		[SerializeField] private float lungeForce = 30.0f;
		[Tooltip("How far ahead to lead the target's movement when calculating lunge direction.")]
		[SerializeField] private float lungeLeadTime = 0.15f;
		[Tooltip("Maximum upward or downward launch angle allowed when building the lunge direction.")]
		[SerializeField] private float lungeMaxLaunchAngle = 45.0f;

		[Header("Animation")]
		[Tooltip("Animator trigger name used to start the lunge animation.")]
		[SerializeField] private string lungeTriggerName = "Lunge";

		// Sword lunge attack cooldown/state timers

		// Cooldown timer for when the next lunge is allowed to start
		private float nextLungeTime = -Mathf.Infinity;
		private Coroutine lungeRoutine;
		// Cached dash direction built during lunge setup and reused during dash start
		private Vector3 lungeDirection = Vector3.forward;
		private int lungeTriggerHash;

		// Sword Enemy Lunge Attack Specific Properties

		// True while the lunge attack is currently in progress
		public bool IsLunging { get; private set; }
		// Minimum valid distance for the lunge attack
		public float LungeMinRange => lungeMinRange;
		// Maximum valid distance for the lunge attack
		public float LungeMaxRange => lungeMaxRange;

		// Caches runtime references + prepares the lunge hitbox + and ensures the lunge rigidbody starts in a kinematic state
		private void Awake() {
			lungeTriggerHash = Animator.StringToHash(lungeTriggerName); // Trigger

			if (lungeRB == null) {
				lungeRB = GetComponent<Rigidbody>();
			}

			if (lungeRB != null) {
				lungeRB.isKinematic = true;
			}

			if (lungeHitbox != null) {
				lungeHitbox.Initialise(lungeAttackData);
				lungeHitbox.Disable();
			}
		}

		// Clears lunge cooldown and state so the module is ready for pooling or respawn
		public void ResetRuntime(SwordEnemy owner) {
			nextLungeTime = -Mathf.Infinity;
			CancelLunge(owner, true);

			if (lungeRB != null) {
				lungeRB.isKinematic = true;
			}

			DisableLungeHitbox();
		}

		// Returns true if the supplied target distance is inside the valid lunge range band
		public bool InRange(float distanceToTarget) {
			return distanceToTarget >= lungeMinRange && distanceToTarget <= lungeMaxRange;
		}

		// Returns true if the owner is allowed to begin a lunge right now
		public bool CanLunge(SwordEnemy owner) {
			return owner != null
				&& owner.IsDead == false
				&& owner.IsStunned == false
				&& owner.IsAttacking == false
				&& Time.time >= nextLungeTime
				&& IsLunging == false;
		}

		// Attempts to start the lunge attack by setting cooldowns + attack lock timing + the lunge animation trigger
		public bool TryStartLunge(SwordEnemy owner, Animator animator) {
			if (owner == null || owner.HasTarget == false) {
				return false;
			}

			if (CanLunge(owner) == false || InRange(owner.DistanceToTarget) == false) {
				return false;
			}

			nextLungeTime = Time.time + lungeCooldown;
			owner.BeginAttackLock(lungeLockTime);
			SetupLungeData();

			if (animator != null) {
				animator.SetTrigger(lungeTriggerHash);
			}

			if (lungeRoutine != null) {
				StopCoroutine(lungeRoutine);
			}

			// Begin lunge attack Coroutine (PhysX method)
			lungeRoutine = StartCoroutine(LungeWindupRoutine(owner));

			return true;
		}

		// Updates active lunge movement and timing while the lunge is in progress
		public void TickLunge(SwordEnemy owner) {
			if (IsLunging == false || owner == null || owner.Target == null) {
				return;
			}

			owner.FaceTarget(owner.Target.position);
		}

		// Cancels the current lunge + optionally forcing a full reset of movement state
		public void CancelLunge(SwordEnemy owner, bool forceClearAttackLock = false) {
			if (owner == null) {
				owner = GetComponent<SwordEnemy>();
				if (owner == null) {
					Debug.LogError("SwordLungeAttack.CancelLunge: SwordEnemy owner is null.");
					return;
				}
			}

			// Stop current lunge routine
			if (lungeRoutine != null) {
				StopCoroutine(lungeRoutine);
				lungeRoutine = null;
			}

			IsLunging = false;
			DisableLungeHitbox();

			// Reset Physics
			if (lungeRB != null) {
				lungeRB.linearVelocity = Vector3.zero;
				// lungeRB.angularVelocity = Vector3.zero;
				lungeRB.isKinematic = true;
			}

			// If lunge has disabled NavMesh movement, restore it here
			owner.EnableNavAgent();

			if (forceClearAttackLock && owner != null) {
				owner.EndAttackLock();
			}
		}

		// Reinitialises lunge attack data before the active lunge frames begin
		public void SetupLungeData() {
			if (lungeHitbox != null) {
				lungeHitbox.Initialise(lungeAttackData);
			}
		}

		// Enables the lunge hitbox during the active damage frames
		public void EnableLungeHitbox() {
			if (lungeHitbox != null) {
				lungeHitbox.Enable();
			}
		}

		// Disables the lunge hitbox after the active damage frames end
		public void DisableLungeHitbox() {
			if (lungeHitbox != null) {
				lungeHitbox.Disable();
			}
		}

		// Begins the dash phase of the lunge using the prepared lunge direction
		// Calculate dash direction at exact launch moment + ensure enemy is facing within a given angle
		public void OnLungeDashStart(SwordEnemy owner) {
			// Stop lunge
			if (owner == null || owner.Target == null || lungeRB == null) {
				return;
			}

			// Compute the lunge dash direction (this is locked at launch to prevent aimbot-like tracking)
			Vector3 predictedPosition = PredictAimPoint(owner.Target);
			Vector3 flatDirection = predictedPosition - transform.position;
			flatDirection.y = 0.0f;

			if (flatDirection.sqrMagnitude < 0.0001f) {
				flatDirection = transform.forward;
			}

			//// Check if sword enemy is facing enough within a given angle
			//// If not, snap rotation to the dash direction before launch
			//if (IsFacingDirection(owner.Target, lungeDirection) == false) {
			//	Quaternion desiredRotation = Quaternion.LookRotation(lungeDirection);
			//	transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
			//}

			float angle = Vector3.Angle(transform.forward, flatDirection.normalized);
			if (angle > lungeMaxLaunchAngle) {
				flatDirection = transform.forward;
			}

			lungeDirection = flatDirection.normalized;

			owner.DisableNavAgent();

			//// Disable NavMesh agent (for physX lunge movement)
			//if (navMeshAgent != null) {
			//	navMeshAgent.enabled = false;
			//}

			lungeRB.isKinematic = false;
			lungeRB.linearVelocity = Vector3.zero;
			//lungeRB.angularVelocity = Vector3.zero;

			// Dash in the same direction used for enemy facing 
			lungeRB.AddForce(lungeDirection * lungeForce, ForceMode.VelocityChange);
		}

		//private void SetLayerRecursively(GameObject obj, int layer) {
		//	obj.layer = layer;
		//	foreach (Transform child in obj.transform)
		//		SetLayerRecursively(child.gameObject, layer);
		//}

		// Predict the player position using player velocity alongside a time lead (predicat ahead of time)
		private Vector3 PredictAimPoint(Transform target) {
			// Aim at players chest (0.8 - 1.2 for the multiplier depending on player height)
			Vector3 baseAim = target.position + target.forward /*Vector3.up * 1.0f*/;

			if (target.TryGetComponent(out Rigidbody rb) == true) {
				return baseAim + rb.linearVelocity * lungeLeadTime;
			}

			// TODO: Expose velocity & use that instead
			return baseAim;
		}

		//// Ensure that enemy is facing their targets direction within a given angle (prevents side-ways lunging)
		//private bool IsFacingDirection(Transform target, Vector3 worldDirection) {
		//	if (target == null) {
		//		return false;
		//	}

		//	worldDirection.y = 0.0f;
		//	if (worldDirection.sqrMagnitude < 0.001f) {
		//		return true;
		//	}

		//	float facingAngle = Vector3.Angle(transform.forward, worldDirection);

		//	return facingAngle <= lungeMaxLaunchAngle;
		//}


		// Ends the dash phase and transitions the lunge into recovery
		public void OnLungeDashEnd(SwordEnemy owner) {
			if (owner == null) {
				owner = GetComponent<SwordEnemy>();
				if (owner == null) {
					Debug.LogError("SwordLungeAttack.OnLungeDashEnd: SwordEnemy owner is null.");
					return;
				}
			}

			if (lungeRB != null) {
				lungeRB.linearVelocity = Vector3.zero;
				//lungeRB.angularVelocity = Vector3.zero;
				lungeRB.isKinematic = true;
			}

			owner.EnableNavAgent();

			if (owner != null && gameObject.activeInHierarchy) {
				StartCoroutine(RecoveryRoutine(owner));
			}
		}

		// TODO: COMMENT
		private IEnumerator LungeWindupRoutine(SwordEnemy owner) {
			IsLunging = true;

			float endTime = Time.time + lungeWindupTime;
			while (Time.time < endTime) {
				if (owner == null || owner.IsDead || owner.HasTarget == false) {
					CancelLunge(owner, true);
					yield break;
				}

				owner.StopMove();
				owner.FaceTarget(owner.Target.position);
				yield return null;
			}

			float dashEndTime = Time.time + lungeDuration;
			while (Time.time < dashEndTime) {
				if (owner == null || owner.IsDead) {
					CancelLunge(owner, true);
					yield break;
				}

				yield return null;
			}
		}

		// TODO: COMMENT
		private IEnumerator RecoveryRoutine(SwordEnemy owner) {
			yield return new WaitForSeconds(lungeRecoveryTime);
			IsLunging = false;
			DisableLungeHitbox();

			if (owner != null && owner.IsDead == false) {
				owner.EndAttackLock();
			}
		}
	}
}
