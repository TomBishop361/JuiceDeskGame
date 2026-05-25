using UnityEngine;

namespace Game.AI.Shield {
	// Keyboard-driven animation tester for the Shield enemy
	// Setup: Add to ShieldEnemy_Root and enable whilst checking animation/controller setup, then disable/remove before making builds
	[DisallowMultipleComponent]
	public sealed class ShieldEnemyAnimationDebugTool : MonoBehaviour {
		[Header("Debug Toggle")]
		[Tooltip("When disabled, this script does nothing and will not interfere with normal gameplay.")]
		[SerializeField] private bool debugMode = false;

		[Header("References")]
		[Tooltip("Animator used by the Shield enemy.")]
		[SerializeField] private Animator animator;

		[Header("Animator Parameters")]
		[Tooltip("Float parameter used by ShieldEnemy.cs for idle/walking animation control.")]
		[SerializeField] private string moveSpeedParameter = "MoveSpeed";
		[Tooltip("Bool parameter used by ShieldDefenseState.cs when the shield is raised for blocking/advancing.")]
		[SerializeField] private string shieldRaisedBoolParameter = "ShieldRaised";
		[Tooltip("Bool parameter used by ShieldMinigunWeapon.cs while the minigun is firing.")]
		[SerializeField] private string firingBoolParameter = "IsFiring";
		[Tooltip("Bool parameter used by ShieldDefenseState.cs while the enemy is vulnerable/exposed.")]
		[SerializeField] private string exposedBoolParameter = "IsExposed";
		[Tooltip("Trigger parameter used by ShieldShockwaveAttack.cs to start the slam animation.")]
		[SerializeField] private string slamTriggerParameter = "Slam";
		[Tooltip("Trigger parameter used by ShieldDefenseState.cs to play a shield block reaction.")]
		[SerializeField] private string blockReactTriggerParameter = "BlockReact";
		[Tooltip("Trigger parameter used by ShieldStunState.cs to play the hit reaction.")]
		[SerializeField] private string hitTriggerParameter = "Hit";
		[Tooltip("Trigger parameter used by EnemyAgentBase.cs to play the death animation.")]
		[SerializeField] private string dieTriggerParameter = "Die";

		[Header("Locomotion Preview Values")]
		[Tooltip("MoveSpeed value sent when previewing Idle.")]
		[SerializeField] private float idleMoveSpeed = 0.0f;
		[Tooltip("MoveSpeed value sent when previewing walk.")]
		[SerializeField] private float advanceMoveSpeed = 4.5f;

		[Header("Optional Direct State Preview")]
		[Tooltip("When enabled, key presses also crossfade directly to the named states. This should be disabled when testing only normal controller transitions.")]
		[SerializeField] private bool crossFadeStates = true;
		[Tooltip("Animator state name for the idle animation.")]
		[SerializeField] private string idleStateName = "Shield_Idle";
		[Tooltip("Animator state name for shield-raised walking/advancing.")]
		[SerializeField] private string advanceStateName = "Shield_AdvanceShieldRaised";
		[Tooltip("Animator state name for the stationary shield-raised block pose.")]
		[SerializeField] private string shieldRaisedStateName = "Shield_Block";
		[Tooltip("Animator state name for minigun firing.")]
		[SerializeField] private string firingStateName = "Shield_FireMinigun";
		[Tooltip("Animator state name for the shockwave slam.")]
		[SerializeField] private string slamStateName = "Shield_SlamShockwave";
		[Tooltip("Animator state name for the exposed/vulnerable pose.")]
		[SerializeField] private string exposedStateName = "Shield_Exposed";
		[Tooltip("Animator state name for the shield block reaction.")]
		[SerializeField] private string blockReactStateName = "Shield_BlockReact";
		[Tooltip("Animator state name for the normal hit reaction.")]
		[SerializeField] private string hitStateName = "Shield_Hit";
		[Tooltip("Animator state name for death.")]
		[SerializeField] private string dieStateName = "Shield_Die";
		[Tooltip("Blend time used when directly previewing states.")]
		[SerializeField] private float crossFadeDuration = 0.08f;

		[Header("Keyboard Shortcuts")]
		[Tooltip("Preview Idle.")]
		[SerializeField] private KeyCode idleKey = KeyCode.Alpha1;
		[Tooltip("Preview Walk/Advance with shield raised.")]
		[SerializeField] private KeyCode advanceKey = KeyCode.Alpha2;
		[Tooltip("Preview stationary shield-raised block pose.")]
		[SerializeField] private KeyCode shieldRaisedKey = KeyCode.Alpha3;
		[Tooltip("Preview minigun firing.")]
		[SerializeField] private KeyCode firingKey = KeyCode.Alpha4;
		[Tooltip("Preview shockwave slam.")]
		[SerializeField] private KeyCode slamKey = KeyCode.Alpha5;
		[Tooltip("Preview exposed/vulnerable state.")]
		[SerializeField] private KeyCode exposedKey = KeyCode.Alpha6;
		[Tooltip("Preview shield block reaction.")]
		[SerializeField] private KeyCode blockReactKey = KeyCode.Alpha7;
		[Tooltip("Preview normal hit/stun reaction.")]
		[SerializeField] private KeyCode hitKey = KeyCode.Alpha8;
		[Tooltip("Preview death.")]
		[SerializeField] private KeyCode dieKey = KeyCode.Alpha9;
		[Tooltip("Clear all parameters and return to Idle preview.")]
		[SerializeField] private KeyCode resetKey = KeyCode.R;

		private int moveSpeedHash;
		private int shieldRaisedBoolHash;
		private int firingBoolHash;
		private int exposedBoolHash;
		private int slamTriggerHash;
		private int blockReactTriggerHash;
		private int hitTriggerHash;
		private int dieTriggerHash;

		private void Reset() {
			FindAnimatorIfNeeded();
		}

		private void Awake() {
			FindAnimatorIfNeeded();
			CacheHashes();
		}

		private void OnValidate() {
			idleMoveSpeed = Mathf.Max(0.0f, idleMoveSpeed);
			advanceMoveSpeed = Mathf.Max(0.0f, advanceMoveSpeed);
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

			if (Input.GetKeyDown(advanceKey)) {
				PlayAdvance();
			}

			if (Input.GetKeyDown(shieldRaisedKey)) {
				PlayShieldRaised();
			}

			if (Input.GetKeyDown(firingKey)) {
				PlayFiring();
			}

			if (Input.GetKeyDown(slamKey)) {
				PlaySlam();
			}

			if (Input.GetKeyDown(exposedKey)) {
				PlayExposed();
			}

			if (Input.GetKeyDown(blockReactKey)) {
				PlayBlockReact();
			}

			if (Input.GetKeyDown(hitKey)) {
				PlayHit();
			}

			if (Input.GetKeyDown(dieKey)) {
				PlayDie();
			}

			if (Input.GetKeyDown(resetKey)) {
				ResetPreview();
			}
		}

		// Sends the same MoveSpeed value the ShieldEnemy script uses when standing still
		private void PlayIdle() {
			ClearAllTriggers();
			SetCommonBools(false, false, false);
			SetFloatSafe(moveSpeedHash, idleMoveSpeed, AnimatorControllerParameterType.Float);
			CrossFadeState(idleStateName);
		}

		// Sends a shield raised and movement value high enough to preview the shield-raised advance/walk state
		private void PlayAdvance() {
			ClearAllTriggers();
			SetCommonBools(true, false, false);
			SetFloatSafe(moveSpeedHash, advanceMoveSpeed, AnimatorControllerParameterType.Float);
			CrossFadeState(advanceStateName);
		}

		// Previews the stationary shield-raised pose without firing or moving
		private void PlayShieldRaised() {
			ClearAllTriggers();
			SetCommonBools(true, false, false);
			SetFloatSafe(moveSpeedHash, idleMoveSpeed, AnimatorControllerParameterType.Float);
			CrossFadeState(shieldRaisedStateName);
		}

		// Previews the continuous minigun firing state used by ShieldMinigunWeapon.cs
		private void PlayFiring() {
			ClearAllTriggers();
			SetCommonBools(true, true, false);
			SetFloatSafe(moveSpeedHash, idleMoveSpeed, AnimatorControllerParameterType.Float);
			CrossFadeState(firingStateName);
		}

		// Previews the slam trigger used by ShieldShockwaveAttack.cs
		private void PlaySlam() {
			ClearAllTriggers();
			SetCommonBools(true, false, false);
			SetFloatSafe(moveSpeedHash, idleMoveSpeed, AnimatorControllerParameterType.Float);
			SetTriggerSafe(slamTriggerHash);
			CrossFadeState(slamStateName);
		}

		// Previews the exposed/vulnerable pose that opens after minigun overheat or slam impact
		private void PlayExposed() {
			ClearAllTriggers();
			SetCommonBools(false, false, true);
			SetFloatSafe(moveSpeedHash, idleMoveSpeed, AnimatorControllerParameterType.Float);
			CrossFadeState(exposedStateName);
		}

		// Previews the shield-specific block reaction trigger
		private void PlayBlockReact() {
			ClearAllTriggers();
			SetCommonBools(true, false, false);
			SetFloatSafe(moveSpeedHash, idleMoveSpeed, AnimatorControllerParameterType.Float);
			SetTriggerSafe(blockReactTriggerHash);
			CrossFadeState(blockReactStateName);
		}

		// Previews the normal damage/stun hit reaction trigger
		private void PlayHit() {
			ClearAllTriggers();
			SetCommonBools(false, false, false);
			SetFloatSafe(moveSpeedHash, idleMoveSpeed, AnimatorControllerParameterType.Float);
			SetTriggerSafe(hitTriggerHash);
			CrossFadeState(hitStateName);
		}

		// Previews the death trigger used by EnemyAgentBase.cs
		private void PlayDie() {
			ClearAllTriggers();
			SetCommonBools(false, false, false);
			SetFloatSafe(moveSpeedHash, idleMoveSpeed, AnimatorControllerParameterType.Float);
			SetTriggerSafe(dieTriggerHash);
			CrossFadeState(dieStateName);
		}

		// Clears temporary inputs and returns the preview to the normal idle value
		private void ResetPreview() {
			ClearAllTriggers();
			SetCommonBools(false, false, false);
			SetFloatSafe(moveSpeedHash, idleMoveSpeed, AnimatorControllerParameterType.Float);
			CrossFadeState(idleStateName);
		}

		// Sets the main bool parameters in one place so that the states do not fight each other during any previews
		private void SetCommonBools(bool shieldRaised, bool isFiring, bool isExposed) {
			SetBoolSafe(shieldRaisedBoolHash, shieldRaised, AnimatorControllerParameterType.Bool);
			SetBoolSafe(firingBoolHash, isFiring, AnimatorControllerParameterType.Bool);
			SetBoolSafe(exposedBoolHash, isExposed, AnimatorControllerParameterType.Bool);
		}

		// Prevents old one-shot triggers from leaking into the next preview key press
		private void ClearAllTriggers() {
			if (animator == null) {
				return;
			}

			ResetTriggerSafe(slamTriggerHash);
			ResetTriggerSafe(blockReactTriggerHash);
			ResetTriggerSafe(hitTriggerHash);
			ResetTriggerSafe(dieTriggerHash);
		}

		// Finds the Animator on the root first and then if unsuccessful, falls back to child visuals
		private void FindAnimatorIfNeeded() {
			if (animator != null) {
				return;
			}

			animator = GetComponent<Animator>();

			if (animator == null) {
				animator = GetComponentInChildren<Animator>(true);
			}
		}

		// Converts parameter names to hashes used by Animator.SetFloat/SetBool/SetTrigger
		private void CacheHashes() {
			moveSpeedHash = Animator.StringToHash(moveSpeedParameter);
			shieldRaisedBoolHash = Animator.StringToHash(shieldRaisedBoolParameter);
			firingBoolHash = Animator.StringToHash(firingBoolParameter);
			exposedBoolHash = Animator.StringToHash(exposedBoolParameter);
			slamTriggerHash = Animator.StringToHash(slamTriggerParameter);
			blockReactTriggerHash = Animator.StringToHash(blockReactTriggerParameter);
			hitTriggerHash = Animator.StringToHash(hitTriggerParameter);
			dieTriggerHash = Animator.StringToHash(dieTriggerParameter);
		}

		// Crossfades only when direct state preview is enabled and the state name exists on layer 0
		private void CrossFadeState(string stateName) {
			if (crossFadeStates == false || animator == null || string.IsNullOrWhiteSpace(stateName)) {
				return;
			}

			int stateHash = Animator.StringToHash(stateName);
			if (animator.HasState(0, stateHash) == false) {
				return;
			}

			animator.CrossFadeInFixedTime(stateHash, crossFadeDuration, 0);
		}

		// Sets a float only if the controller contains the expected parameter
		private void SetFloatSafe(int parameterHash, float value, AnimatorControllerParameterType expectedType) {
			if (HasParameter(parameterHash, expectedType)) {
				animator.SetFloat(parameterHash, value);
			}
		}

		// Sets a bool only if the controller contains the expected parameter
		private void SetBoolSafe(int parameterHash, bool value, AnimatorControllerParameterType expectedType) {
			if (HasParameter(parameterHash, expectedType)) {
				animator.SetBool(parameterHash, value);
			}
		}

		// Fires a trigger only if the controller contains the expected parameter
		private void SetTriggerSafe(int parameterHash) {
			if (HasParameter(parameterHash, AnimatorControllerParameterType.Trigger)) {
				animator.SetTrigger(parameterHash);
			}
		}

		// Resets a trigger only if the controller contains the expected parameter
		private void ResetTriggerSafe(int parameterHash) {
			if (HasParameter(parameterHash, AnimatorControllerParameterType.Trigger)) {
				animator.ResetTrigger(parameterHash);
			}
		}

		// Checks the runtime controller parameter list to avoid any missing-parameter Animator warnings during setup
		private bool HasParameter(int parameterHash, AnimatorControllerParameterType expectedType) {
			if (animator == null || animator.runtimeAnimatorController == null) {
				return false;
			}

			AnimatorControllerParameter[] parameters = animator.parameters;
			for (int i = 0; i < parameters.Length; i++) {
				AnimatorControllerParameter parameter = parameters[i];
				if (parameter.nameHash == parameterHash && parameter.type == expectedType) {
					return true;
				}
			}

			return false;
		}
	}
}
