using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Handles the player-specific part of an airlock transition
// This script should be placed on the Player prefab
//
// Responsibilities:
// - temporarily lock player input/movement
// - optionally move the player to a hold point inside the airlock
// - teleport the player to the next scene entry point
// - preserve, damp, or clear Rigidbody velocity after teleporting
[DisallowMultipleComponent]
public sealed class AirlockPlayerHandoff : MonoBehaviour {
	public enum VelocityHandling {
		[Tooltip("Keep the player's velocity after teleporting to the next scene.")]
		Preserve = 0,
		[Tooltip("Multiply the player's velocity by a damping value after teleporting.")]
		Dampen = 1,
		[Tooltip("Clear the player's velocity after teleporting.")]
		Clear = 2
	}

	[Header("References")]
	[Tooltip("Rigidbody used to move and rotate the player during the airlock handoff.")]
	[SerializeField] private Rigidbody playerRigidbody;
	[Tooltip("Unity Input System PlayerInput component. Disabled while the airlock transition is running.")]
	[SerializeField] private PlayerInput playerInput;
	[Tooltip("Input Controller component. Its freeze flag is used while the transition is running.")]
	[SerializeField] private InputController inputController;
	[Tooltip("Additional MonoBehaviours to disable while the player is locked. Examples: weapon firing, grappling, or camera scripts.")]
	[SerializeField] private MonoBehaviour[] additionalComponentsToDisable;

	//[Header("Input Lock Behaviour")]
	//[Tooltip("If true, PlayerInput is disabled while the airlock transition is running.")]
	//[SerializeField] private bool disablePlayerInput = true;
	//[Tooltip("If true, InputController is disabled while the airlock transition is running.")]
	//[SerializeField] private bool disableInputController = true;
	//[Tooltip("If true, InputController.freeze is set while the airlock transition is running.")]
	//[SerializeField] private bool useInputControllerFreezeFlag = true;

	[Header("Debug")]
	[Tooltip("If true, player handoff actions are logged to the Unity Console.")]
	[SerializeField] private bool debugLogging = false;

	public Rigidbody PlayerRigidbody => playerRigidbody;
	public bool IsLocked => isLocked;

	private readonly List<ComponentState> disabledComponentStates = new List<ComponentState>(8);
	private bool isLocked;

	// Previous enabled states are stored so UnlockPlayer restores the player correctly
	private bool previousPlayerInputEnabled;
	private bool previousInputControllerEnabled;
	private bool previousInputControllerFreeze;

	private struct ComponentState {
		public Behaviour Component;
		public bool WasEnabled;

		public ComponentState(Behaviour component, bool wasEnabled) {
			Component = component;
			WasEnabled = wasEnabled;
		}
	}

	private void Reset() {
		ResolveReferences();
	}

	private void Awake() {
		ResolveReferences();
	}

	// Locks player input/movement during the sealed airlock moment
	// Called by AirlockSceneTransitionPortal before the fade/teleport happens
	public void LockPlayer() {
		if (isLocked) {
			return;
		}

		ResolveReferences();
		disabledComponentStates.Clear();

		if (playerInput != null) {
			previousPlayerInputEnabled = playerInput.enabled;
			playerInput.enabled = false;
		}

		if (inputController != null) {
			previousInputControllerEnabled = inputController.enabled;
			previousInputControllerFreeze = inputController.freeze;

			// Freeze first so the controller knows movement should stop
			inputController.freeze = true;

			// Disable it as well so it cannot keep reading movement/fire input during the transition
			inputController.enabled = false;
		}

		if (additionalComponentsToDisable != null) {
			for (int i = 0; i < additionalComponentsToDisable.Length; i++) {
				DisableAndRemember(additionalComponentsToDisable[i]);
			}
		}

		isLocked = true;
		Log("Player locked for airlock transition.");
	}

	// Restores player input/movement and every component that was disabled after the transition finishes
	// Called by AirlockSceneTransitionPortal after the new scene has appeared
	public void UnlockPlayer() {
		if (isLocked == false) {
			return;
		}

		for (int i = 0; i < disabledComponentStates.Count; i++) {
			ComponentState state = disabledComponentStates[i];
			if (state.Component != null) {
				state.Component.enabled = state.WasEnabled;
			}
		}

		disabledComponentStates.Clear();

		if (inputController != null) {
			inputController.freeze = previousInputControllerFreeze;
		}

		if (playerRigidbody != null) {
			playerRigidbody.isKinematic = false;
			playerRigidbody.detectCollisions = true;
		}

		if (playerInput != null) {
			playerInput.enabled = true;
		}

		if (inputController != null) {
			inputController.enabled = true;
			inputController.freeze = previousInputControllerFreeze;
		}

		isLocked = false;
		Log("Player unlocked after airlock transition.");
	}

	// Moves the player to a temporary hold point inside the old-scene airlock
	// This is used before the fade so the player appears centred/controlled inside the sealed room
	public void MoveToHoldPoint(Transform holdPoint, bool alignRotation, bool clearVelocity) {
		if (holdPoint == null) {
			return;
		}

		ResolveReferences();

		MovePlayer(
			holdPoint.position, 
			holdPoint.rotation, 
			alignRotation, 
			clearVelocity ? VelocityHandling.Clear : VelocityHandling.Preserve, 
			1.0f, 
			true
		);
	}

	// Teleports the player to the next scene entry point
	// This happens while the screen is black / hidden by the airlock effect
	public void TeleportToEntryPoint(Transform entryPoint, bool alignRotation, VelocityHandling velocityHandling, float velocityDamping, bool clearAngularVelocity) {
		if (entryPoint == null) {
			return;
		}

		ResolveReferences();

		Physics.SyncTransforms();

		MovePlayer(
			entryPoint.position, 
			entryPoint.rotation,
			alignRotation, 
			velocityHandling, 
			velocityDamping, 
			clearAngularVelocity
		);
	}

	// Moves the player Rigidbody to a target position/rotation
	private void MovePlayer(Vector3 targetPosition, Quaternion targetRotation, bool alignRotation, VelocityHandling velocityHandling, float velocityDamping, bool clearAngularVelocity) {
		Vector3 preservedVelocity = Vector3.zero;
		Vector3 preservedAngularVelocity = Vector3.zero;

		if (playerRigidbody != null) {
			// Store velocity before teleporting so it can be preserved/damped/cleared afterwards
			preservedVelocity = playerRigidbody.linearVelocity;
			preservedAngularVelocity = playerRigidbody.angularVelocity;

			playerRigidbody.position = targetPosition;

			if (alignRotation) {
				playerRigidbody.rotation = targetRotation;
			}
		}
		else {
			// Fallback if no Rigidbody exists
			transform.position = targetPosition;

			if (alignRotation) {
				transform.rotation = targetRotation;
			}
		}

		ApplyVelocity(
			velocityHandling,
			preservedVelocity,
			preservedAngularVelocity,
			Mathf.Clamp01(velocityDamping),
			clearAngularVelocity
		);

		// Ensures physics queries/colliders immediately use the new transform position
		Physics.SyncTransforms();

		Log($"Player moved to {targetPosition}.");
	}

	// Applies the selected velocity behaviour after teleporting
	private void ApplyVelocity(VelocityHandling velocityHandling, Vector3 preservedVelocity, Vector3 preservedAngularVelocity, float velocityDamping, bool clearAngularVelocity) {
		if (playerRigidbody == null) {
			return;
		}

		switch (velocityHandling) {
			case VelocityHandling.Preserve:
				playerRigidbody.linearVelocity = preservedVelocity;
				break;

			case VelocityHandling.Dampen:
				playerRigidbody.linearVelocity = preservedVelocity * velocityDamping;
				break;

			case VelocityHandling.Clear:
				playerRigidbody.linearVelocity = Vector3.zero;
				break;
		}

		playerRigidbody.angularVelocity = clearAngularVelocity ? Vector3.zero : preservedAngularVelocity;
	}

	private void DisableAndRemember(Behaviour component) {
		if (component == null) {
			return;
		}

		disabledComponentStates.Add(new ComponentState(component, component.enabled));
		component.enabled = false;
	}

	private void ResolveReferences() {
		if (playerRigidbody == null) {
			playerRigidbody = GetComponent<Rigidbody>();
		}

		if (playerInput == null) {
			playerInput = GetComponent<PlayerInput>();
		}

		if (inputController == null) {
			inputController = GetComponent<InputController>();
		}
	}

	private void Log(string message) {
		if (debugLogging) {
			Debug.Log($"{name}: {message}", this);
		}
	}
}