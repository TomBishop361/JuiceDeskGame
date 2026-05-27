using Game.Audio;
using UnityEngine;
using UnityEngine.Events;

// Door wrapper used by the airlock transition system
// Centralises animator triggers, blockers, and feedback hooks
[DisallowMultipleComponent]
public sealed class AirlockDoorController : MonoBehaviour {
	[Header("References")]
	[Tooltip("Animator that owns this door's open/close/lock animations.")]
	[SerializeField] private Animator animator;
	[Tooltip("Collider that blocks the doorway while the door is closed or locked.")]
	[SerializeField] private Collider blockingCollider;

	[Header("Animator Parameters")]
	[Tooltip("Animator trigger fired when the door opens.")]
	[SerializeField] private string openTriggerName = "Open";
	[Tooltip("Animator trigger fired when door closes.")]
	[SerializeField] private string closeTriggerName = "Close";
	[Tooltip("Animator bool set to true while the door is locked.")]
	[SerializeField] private string lockedBoolName = "Locked";

	[Header("Behaviour")]
	[Tooltip("If true, the door begins locked and cannot open until UnlockDoor is called.")]
	[SerializeField] private bool startsLocked = false;

	[Header("Airlock SFX")]
	[Tooltip("Played when the player attempts to open the airlock from outside.")]
	[SerializeField] private SFXDefinition airlockOpenRequestSFX;
	[Tooltip("Played when the airlock door opens.")]
	[SerializeField] private SFXDefinition airlockDoorOpenSFX;
	[Tooltip("Played when the airlock door closes.")]
	[SerializeField] private SFXDefinition airlockDoorCloseSFX;
	[Tooltip("Played when the player tries to open a locked or denied airlock.")]
	[SerializeField] private SFXDefinition airlockDoorLockedSFX;
	[Tooltip("World position to play the airlock door SFX. If empty, this door transform is used.")]
	[SerializeField] private Transform doorSFXPoint;

	[Header("Unity Events")]
	[Tooltip("Invoked when the door receives an open request.")]
	[SerializeField] private UnityEvent onOpenRequested;
	[Tooltip("Invoked when the door receives a close request.")]
	[SerializeField] private UnityEvent onCloseRequested;
	[Tooltip("Invoked when the door is locked.")]
	[SerializeField] private UnityEvent onLocked;
	[Tooltip("Invoked when the door is unlocked.")]
	[SerializeField] private UnityEvent onUnlocked;
	[Tooltip("Invoked when the door refuses to open because it is locked.")]
	[SerializeField] private UnityEvent onOpenDenied;

	[Header("Debug")]
	[Tooltip("If true, door state changes and requests are logged to the Unity Console.")]
	[SerializeField] private bool debugLogging = false;

	public bool IsLocked => locked;
	public bool IsOpen => isOpen;

	private bool locked;
	private bool isOpen;
	private int lockedBoolHash;

	private void Reset() {
		animator = GetComponentInChildren<Animator>();
		blockingCollider = GetComponentInChildren<Collider>();
	}

	private void Awake() {
		if (animator == null) {
			animator = GetComponentInChildren<Animator>();
		}

		// Apply starting lock state
		locked = startsLocked;
		// Doors begin closed by default
		isOpen = false;

		lockedBoolHash = string.IsNullOrWhiteSpace(lockedBoolName) ? 0 : Animator.StringToHash(lockedBoolName);
		ApplyLockedAnimatorState();

		// Make sure the doorway is blocked at scene start if the door is closed or locked
		UpdateBlocker();
	}

	// Called when the player approaches/uses the door from the outside
	// Plays the scanner/access sound before deciding whether the door can actually open
	public void RequestOpenFromOutside() {
		PlayDoorSFX(airlockOpenRequestSFX);

		// If the door is locked, play the denied/locked feedback instead of opening
		if (locked) {
			PlayDoorSFX(airlockDoorLockedSFX);
			onOpenDenied?.Invoke();
			Log("Outside open request denied because the door is locked.");
			return;
		}

		OpenDoor();
	}

	// Opens the door if it is not locked
	// Called by AirlockSceneTransitionPortal when the player is allowed into the airlock
	public void OpenDoor() {
		if (locked) {
			PlayDoorSFX(airlockDoorLockedSFX);
			onOpenDenied?.Invoke();
			Log("Open denied because the door is locked.");
			return;
		}

		if (isOpen) {
			return;
		}

		isOpen = true;

		SetAnimatorTrigger(openTriggerName);
		PlayDoorSFX(airlockDoorOpenSFX);
		onOpenRequested?.Invoke();
		UpdateBlocker();

		Log("Open requested.");
	}

	// Closes the door
	// Called once the player has entered the airlock so the old level is sealed behind them
	public void CloseDoor() {
		// Prevent re-triggering the close animation if the door is already closed
		if (isOpen == false) {
			return;
		}

		isOpen = false;

		SetAnimatorTrigger(closeTriggerName);
		PlayDoorSFX(airlockDoorCloseSFX);
		onCloseRequested?.Invoke();
		UpdateBlocker();

		Log("Close requested.");
	}

	// Locks the door and makes sure it is closed
	// Useful if enemies or alarms should block the exit (prevent it from opening)
	public void LockDoor() {
		locked = true;

		// If the door was open, close it before locking the doorway
		if (isOpen) {
			CloseDoor();
		}

		ApplyLockedAnimatorState();
		// NOTE: Remove if an open door plays its closed and locked SFX and it becomes too noisy/cluttered
		PlayDoorSFX(airlockDoorLockedSFX); 
		onLocked?.Invoke();
		UpdateBlocker();

		Log("Locked.");
	}

	// Unlocks the door so OpenDoor can be used again
	public void UnlockDoor() {
		locked = false;

		ApplyLockedAnimatorState();
		onUnlocked?.Invoke();
		// The door may still be closed after unlocking, so keep the blocker synced
		UpdateBlocker();

		Log("Unlocked.");
	}

	// Called when something tries to open the door but the portal/system refuses it
	// Plays scanner first, then denied/locked feedback
	public void DenyOpenRequest() {
		PlayDoorSFX(airlockOpenRequestSFX);
		PlayDoorSFX(airlockDoorLockedSFX);
		onOpenDenied?.Invoke();
		Log("Open denied.");
	}

	// Enables the blocking collider when the door is closed or locked
	// Disables it only when the door is open and unlocked
	private void UpdateBlocker() {
		if (blockingCollider == null) {
			return;
		}

		blockingCollider.enabled = locked || isOpen == false;
	}

	private void ApplyLockedAnimatorState() {
		if (animator == null || lockedBoolHash == 0) {
			return;
		}

		animator.SetBool(lockedBoolHash, locked);
	}

	// Fires an Animator trigger
	// If no Animator or trigger name is assigned, tthen this doesn't do anything
	private void SetAnimatorTrigger(string triggerName) {
		if (animator == null || string.IsNullOrWhiteSpace(triggerName)) {
			return;
		}

		// Reset first so repeated open/close calls behave reliably
		animator.ResetTrigger(triggerName);
		animator.SetTrigger(triggerName);
	}

	private Vector3 GetDoorSFXPosition() {
		return doorSFXPoint != null ? doorSFXPoint.position : transform.position;
	}

	private void PlayDoorSFX(SFXDefinition sfx) {
		if (sfx == null) {
			return;
		}

		SFXManager.PlayAtPosition(sfx, GetDoorSFXPosition());
	}

	private void Log(string message) {
		if (debugLogging) {
			Debug.Log($"{name}: {message}", this);
		}
	}
}