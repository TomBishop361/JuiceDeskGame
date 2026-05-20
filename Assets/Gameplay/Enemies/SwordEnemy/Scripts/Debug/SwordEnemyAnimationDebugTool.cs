using UnityEngine;

namespace Game.AI.Sword {
	// Temporary keyboard-driven animation tester for the Sword enemy
	// Add this to the SwordEnemy_Root prefab only while checking animation/controller setup, then remove or disable it before making any builds
	[DisallowMultipleComponent]
	public sealed class SwordEnemyAnimationDebugTool : MonoBehaviour {
		[Header("Debug Toggle")]
		[Tooltip("When disabled, this script does nothing and will not interfere with normal gameplay.")]
		[SerializeField] private bool debugMode = false;

		[Header("References")]
		[Tooltip("Animator used by the Sword enemy. Leave empty to auto-find one on this GameObject or its children.")]
		[SerializeField] private Animator animator;

		[Header("Animator Parameters")]
		[Tooltip("Float parameter used by SwordEnemy.cs for idle/run animation control.")]
		[SerializeField] private string moveSpeedParameter = "MoveSpeed";
		[Tooltip("Trigger parameter used by SwordMeleeCombat.cs.")]
		[SerializeField] private string swingTriggerParameter = "Swing";
		[Tooltip("Trigger parameter used by SwordLungeAttack.cs.")]
		[SerializeField] private string lungeTriggerParameter = "Lunge";
		[Tooltip("Trigger parameter used by SwordStunState.cs.")]
		[SerializeField] private string hitTriggerParameter = "Hit";
		[Tooltip("Trigger parameter used by EnemyAgentBase.cs.")]
		[SerializeField] private string dieTriggerParameter = "Die";

		[Header("Locomotion Preview Values")]
		[Tooltip("MoveSpeed value sent when previewing Idle.")]
		[SerializeField] private float idleMoveSpeed = 0.0f;
		[Tooltip("MoveSpeed value sent when previewing Run.")]
		[SerializeField] private float runMoveSpeed = 6.0f;

		[Header("Optional Direct State Preview")]
		[Tooltip("When enabled, Idle and Run also crossfade directly to the named states. Disable this when testing only normal controller transitions.")]
		[SerializeField] private bool crossFadeLocomotionStates = true;
		[Tooltip("Animator state name for the idle animation.")]
		[SerializeField] private string idleStateName = "Sword Idle";
		[Tooltip("Animator state name for the run animation.")]
		[SerializeField] private string runStateName = "Sword Run";
		[Tooltip("Short blend time used when directly previewing Idle/Run states.")]
		[SerializeField] private float crossFadeDuration = 0.08f;

		[Header("Keyboard Shortcuts")]
		[Tooltip("Preview Idle.")]
		[SerializeField] private KeyCode idleKey = KeyCode.Alpha1;
		[Tooltip("Preview Run.")]
		[SerializeField] private KeyCode runKey = KeyCode.Alpha2;
		[Tooltip("Trigger Swing.")]
		[SerializeField] private KeyCode swingKey = KeyCode.Alpha3;
		[Tooltip("Trigger Lunge.")]
		[SerializeField] private KeyCode lungeKey = KeyCode.Alpha4;
		[Tooltip("Trigger Hit/Stun reaction.")]
		[SerializeField] private KeyCode hitKey = KeyCode.Alpha5;
		[Tooltip("Trigger Death.")]
		[SerializeField] private KeyCode dieKey = KeyCode.Alpha6;
		[Tooltip("Clear triggers and return to Idle preview.")]
		[SerializeField] private KeyCode resetKey = KeyCode.R;

		private int moveSpeedHash;
		private int swingHash;
		private int lungeHash;
		private int hitHash;
		private int dieHash;

		private void Reset() {
			FindAnimatorIfNeeded();
		}

		private void Awake() {
			FindAnimatorIfNeeded();
			CacheHashes();
		}

		// Rebuilds hashes after Inspector value changes
		private void OnValidate() {
			idleMoveSpeed = Mathf.Max(0.0f, idleMoveSpeed);
			runMoveSpeed = Mathf.Max(0.0f, runMoveSpeed);
			crossFadeDuration = Mathf.Max(0.0f, crossFadeDuration);
			CacheHashes();
		}

		// Handles keyboard input only while debugMode is enabled
		private void Update() {
			if (debugMode == false || animator == null) {
				return;
			}

			if (Input.GetKeyDown(idleKey)) {
				PlayIdle();
			}

			if (Input.GetKeyDown(runKey)) {
				PlayRun();
			}

			if (Input.GetKeyDown(swingKey)) {
				FireTrigger(swingHash);
			}

			if (Input.GetKeyDown(lungeKey)) {
				FireTrigger(lungeHash);
			}

			if (Input.GetKeyDown(hitKey)) {
				FireTrigger(hitHash);
			}

			if (Input.GetKeyDown(dieKey)) {
				FireTrigger(dieHash);
			}

			if (Input.GetKeyDown(resetKey)) {
				ResetPreview();
			}
		}

		// Sends the same MoveSpeed value the real SwordEnemy script uses when standing still
		private void PlayIdle() {
			ResetCombatTriggers();
			animator.SetFloat(moveSpeedHash, idleMoveSpeed);

			if (crossFadeLocomotionStates && string.IsNullOrWhiteSpace(idleStateName) == false) {
				animator.CrossFadeInFixedTime(idleStateName, crossFadeDuration);
			}
		}

		// Sends a movement value high enough to preview the run state/blend
		private void PlayRun() {
			ResetCombatTriggers();
			animator.SetFloat(moveSpeedHash, runMoveSpeed);

			if (crossFadeLocomotionStates && string.IsNullOrWhiteSpace(runStateName) == false) {
				animator.CrossFadeInFixedTime(runStateName, crossFadeDuration);
			}
		}

		// Clears competing triggers before firing the requested action trigger
		private void FireTrigger(int triggerHash) {
			ResetCombatTriggers();
			animator.SetTrigger(triggerHash);
		}

		// Clears temporary inputs and returns the preview to the normal idle value
		private void ResetPreview() {
			ResetCombatTriggers();
			animator.SetFloat(moveSpeedHash, idleMoveSpeed);

			if (crossFadeLocomotionStates && string.IsNullOrWhiteSpace(idleStateName) == false) {
				animator.CrossFadeInFixedTime(idleStateName, crossFadeDuration);
			}
		}

		// Prevents old one-shot triggers from leaking into the next preview key press
		private void ResetCombatTriggers() {
			if (animator == null) {
				return;
			}

			animator.ResetTrigger(swingHash);
			animator.ResetTrigger(lungeHash);
			animator.ResetTrigger(hitHash);
			animator.ResetTrigger(dieHash);
		}

		// Finds the Animator on the root first, then falls back to child visuals
		private void FindAnimatorIfNeeded() {
			if (animator != null) {
				return;
			}

			animator = GetComponent<Animator>();

			if (animator == null) {
				animator = GetComponentInChildren<Animator>(true);
			}
		}

		// Converts parameter names to hashes used by Animator.SetFloat/SetTrigger
		private void CacheHashes() {
			moveSpeedHash = Animator.StringToHash(moveSpeedParameter);
			swingHash = Animator.StringToHash(swingTriggerParameter);
			lungeHash = Animator.StringToHash(lungeTriggerParameter);
			hitHash = Animator.StringToHash(hitTriggerParameter);
			dieHash = Animator.StringToHash(dieTriggerParameter);
		}
	}
}
