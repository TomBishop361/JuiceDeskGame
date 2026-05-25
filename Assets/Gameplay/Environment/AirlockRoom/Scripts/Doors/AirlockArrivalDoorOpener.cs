using System.Collections;
using UnityEngine;

// Opens a door in the newly loaded scene when the airlock portal finishes moving the player
[DisallowMultipleComponent]
public sealed class AirlockArrivalDoorOpener : MonoBehaviour {
	[Header("Door")]
	[Tooltip("Door to open when the player is handed off into this scene.")]
	[SerializeField] private AirlockDoorController door;

	[Header("Open Timing")]
	[Tooltip("Delay after player handoff before opening this door.")]
	[SerializeField] private float openDelay = 0.25f;

	[Header("Auto Lock After Exit")]
	[Tooltip("If true, this door can be closed and locked by an AirlockArrivalDoorLockTrigger after the player walks through it.")]
	[SerializeField] private bool lockAfterPlayerExits = true;
	[Tooltip("Delay before closing the door after the player passes the lock trigger.")]
	[SerializeField] private float closeAfterExitDelay = 0.2f;
	[Tooltip("Delay after closing before the door locks.")]
	[SerializeField] private float lockAfterCloseDelay = 0.1f;
	[Tooltip("If true, the close/lock sequence can only happen once.")]
	[SerializeField] private bool lockOnlyOnce = true;

	[Header("Debug")]
	[Tooltip("If true, open/lock actions are logged to the Unity Console.")]
	[SerializeField] private bool debugLogging = false;

	private Coroutine openRoutine;
	private Coroutine closeAndLockRoutine;
	private bool hasLockedAfterExit;

	private void Reset() {
		door = GetComponent<AirlockDoorController>();
	}

	private void OnEnable() {
		// The scene is activated before the handoff event is invoked meaning that
		// this object has time to subscribe
		AirlockSceneTransitionPortal.OnAnyPlayerHandoff += HandlePlayerHandoff;
	}

	private void OnDisable() {
		AirlockSceneTransitionPortal.OnAnyPlayerHandoff -= HandlePlayerHandoff;
	}

	// Called by AirlockSceneTransitionPortal after the player is teleported to this scene's entry point
	private void HandlePlayerHandoff() {
		if (openRoutine != null) {
			StopCoroutine(openRoutine);
		}

		openRoutine = StartCoroutine(OpenDoorRoutine());
	}

	// Used by AirlockArrivalDoorLockTrigger
	// This is called once the player has walked through the new scene's airlock exit door
	public void CloseAndLockAfterPlayerExit() {
		if (lockAfterPlayerExits == false) {
			return;
		}

		if (hasLockedAfterExit && lockOnlyOnce) {
			return;
		}

		if (closeAndLockRoutine != null) {
			StopCoroutine(closeAndLockRoutine);
		}

		closeAndLockRoutine = StartCoroutine(CloseAndLockAfterExitRoutine());
	}

	private IEnumerator OpenDoorRoutine() {
		if (openDelay > 0.0f) {
			yield return new WaitForSeconds(openDelay);
		}

		AirlockDoorController resolvedDoor = ResolveDoor();
		if (resolvedDoor == null) {
			yield break;
		}

		// The arrival door could start locked so the player cannot open it early
		// Unlock first, then open it as part of the arrival reveal
		resolvedDoor.UnlockDoor();
		resolvedDoor.OpenDoor();

		Log("Arrival door opened after player handoff.");
	}

	private IEnumerator CloseAndLockAfterExitRoutine() {
		AirlockDoorController resolvedDoor = ResolveDoor();
		if (resolvedDoor == null) {
			yield break;
		}

		hasLockedAfterExit = true;

		if (closeAfterExitDelay > 0.0f) {
			yield return new WaitForSeconds(closeAfterExitDelay);
		}

		// Close first so the door visibly seals behind the player
		resolvedDoor.CloseDoor();

		if (lockAfterCloseDelay > 0.0f) {
			yield return new WaitForSeconds(lockAfterCloseDelay);
		}

		// Lock after closing so it cannot be reopened from the level side
		resolvedDoor.LockDoor();

		Log("Arrival door closed and locked after player exited.");
	}

	private AirlockDoorController ResolveDoor() {
		if (door == null) {
			door = GetComponent<AirlockDoorController>();
		}

		if (door == null) {
			Debug.LogWarning($"{name}: No AirlockDoorController assigned.", this);
		}

		return door;
	}

	private void Log(string message) {
		if (debugLogging) {
			Debug.Log($"{name}: {message}", this);
		}
	}
}