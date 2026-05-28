using Game.Audio;
using UnityEngine;
namespace Game.AI.Shield {
    [DisallowMultipleComponent]
    public sealed class ShieldDefenseState : MonoBehaviour {
		[Header("Block")]
		[Tooltip("Collider enabled while the shield is actively blocking incoming attacks.")]
		[SerializeField] private Collider shieldBlockCollider;
		[Tooltip("Forward-facing arc (in degrees) within which the shield can block incoming hits.")]
		[SerializeField] private float blockArcDegrees = 120.0f;

		[Header("Timing")]
		//[Tooltip("Duration of the vulnerability window during which grapple interactions are allowed.")]
		//[SerializeField] private float grappleWindowDuration = 1.0f;
		[Tooltip("Recovery time after the exposed state ends before normal defence resumes.")]
		[SerializeField] private float postExposeRecoveryTime = 0.4f;

		[Header("Animation")]
		[Tooltip("Animator bool name used to indicate that the shield enemy is exposed.")]
		[SerializeField] private string exposedBoolName = "IsExposed";
		[Tooltip("Animator bool name used to indicate that the shield is raised for blocking.")]
		[SerializeField] private string shieldRaisedBoolName = "ShieldRaised";
		[Tooltip("Animator trigger name used to play the shield block reaction animation.")]
		[SerializeField] private string blockReactTriggerName = "BlockReact";

		[Header("SFX")]
		[Tooltip("Played when the shield successfully blocks or deflects an incoming hit.")]
		[SerializeField] private SFXDefinition shieldBlockSFX;

		// Cooldown timer for when the next lunge is allowed to start
		private float exposedEndTime = -Mathf.Infinity;
		//private float grappleWindowEndTime = -Mathf.Infinity;
		private float postExposeEndTime = -Mathf.Infinity;
		private float nextBlockReactTime = -Mathf.Infinity;

		// Animation IDs
		private int exposedBoolHash;
		private int shieldRaisedBoolHash;
		private int blockReactTriggerHash;

		// Shield Enemy Defense State Specific Properties
		public bool IsExposed { get; private set; } // True while the weapon has overheated and the owner should be vulnerable
		public bool ShieldRaised { get; private set; }
		public float PostExposeEndTime => postExposeEndTime;
		public bool IsRecoveringFromExpose => IsExposed == false && Time.time < postExposeEndTime;
		public bool IsExposedOrRecovering => IsExposed || Time.time < postExposeEndTime;

		private void Awake() {
			exposedBoolHash = Animator.StringToHash(exposedBoolName);
			shieldRaisedBoolHash = Animator.StringToHash(shieldRaisedBoolName);
			blockReactTriggerHash = Animator.StringToHash(blockReactTriggerName);
		}

		public void ResetRuntime(Animator animator) {
			IsExposed = false;
			//GrappleWindowOpen = false;
			ShieldRaised = false;
			exposedEndTime = -Mathf.Infinity;
			//grappleWindowEndTime = -Mathf.Infinity;
			postExposeEndTime = -Mathf.Infinity;
			nextBlockReactTime = -Mathf.Infinity;

			if (shieldBlockCollider != null) {
				shieldBlockCollider.enabled = false;
			}

			if (animator != null) {
				animator.SetBool(exposedBoolHash, false);
				animator.SetBool(shieldRaisedBoolHash, false);
			}
		}

		public void TickState(Transform target, Animator animator) {
			if (IsExposed && Time.time >= exposedEndTime) {
				ExitExposed(animator);
			}

			//if (GrappleWindowOpen && Time.time >= grappleWindowEndTime) {
			//	GrappleWindowOpen = false;
			//}

			UpdateShieldBlockState(target);
		}

		public bool CanUseOffense() {
			return IsExposed == false && Time.time >= postExposeEndTime;
		}

		// Check if the player is in front of the shield enemy (used to initiate shield raised for blocking action)
		public bool PlayerInFront(Transform target) {
			if (target == null) {
				return false;
			}

			Vector3 toTarget = target.position - transform.position;
			toTarget.y = 0.0f;

			if (toTarget.sqrMagnitude < 0.0001f) {
				return true;
			}

			float angle = Vector3.Angle(transform.forward, toTarget.normalized);
			return angle <= (blockArcDegrees * 0.5f); // Half to get the front angle
		}

		public void SetShieldRaised(bool isRaised, Animator animator, Transform target = null) {
			ShieldRaised = isRaised && IsExposed == false;

			if (animator != null) {
				animator.SetBool(shieldRaisedBoolHash, ShieldRaised);
			}

			UpdateShieldBlockState(target);
		}

		public void EnterExposed(float duration, Animator animator, Transform target = null) {
			IsExposed = true;
			//GrappleWindowOpen = true;
			exposedEndTime = Time.time + duration;
			//grappleWindowEndTime = Time.time + grappleWindowDuration;
			postExposeEndTime = exposedEndTime + postExposeRecoveryTime;

			SetShieldRaised(false, animator, target);

			if (animator != null) {
				animator.SetBool(exposedBoolHash, true);
			}
		}

		public void ExitExposed(Animator animator) {
			IsExposed = false;

			if (animator != null) {
				animator.SetBool(exposedBoolHash, false);
			}
		}

		public void StopAllStates(Animator animator) {
			IsExposed = false;
			//GrappleWindowOpen = false;
			SetShieldRaised(false, animator);

			if (animator != null) {
				animator.SetBool(exposedBoolHash, false);
			}
		}

		public void TriggerBlockReact(Animator animator) {
			if (animator == null || Time.time < nextBlockReactTime) {
				return;
			}

			nextBlockReactTime = Time.time + 0.20f;
			animator.SetTrigger(blockReactTriggerHash);

			// Play block feedback here so the sound follows the same cooldown as the block reaction animation
			SFXManager.PlayAttached(shieldBlockSFX, transform);
		}

		private void UpdateShieldBlockState(Transform target) {
			if (shieldBlockCollider == null) {
				return;
			}

			// Only enable bullet blocking when shield is raised
			bool shouldBlock = ShieldRaised && IsExposed == false;
			if (target != null) {
				shouldBlock &= PlayerInFront(target);
			}

			shieldBlockCollider.enabled = shouldBlock;
		}
	}
}