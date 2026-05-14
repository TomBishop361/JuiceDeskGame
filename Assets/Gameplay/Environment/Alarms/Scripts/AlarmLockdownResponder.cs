using UnityEngine;
using UnityEngine.Events;

// Scene hook for lockdown doors etc
// It reacts to the central alarm system without making the wave spawner know about individual scene objects
[DisallowMultipleComponent]
public sealed class AlarmLockdownResponder : MonoBehaviour {
	[Header("References")]
	[Tooltip("Central alarm controller used as the source for level/encounter alarm behaviour.")]
	[SerializeField] private LevelAlarmController alarmController;

	[Header("Objects To Toggle")]
	[Tooltip("Objects enabled while the alarm is active..")]
	[SerializeField] private GameObject[] enableDuringAlarm;
	[Tooltip("Objects disabled while the alarm is active.")]
	[SerializeField] private GameObject[] disableDuringAlarm;

	[Header("Animator Hook")]
	[Tooltip("Optional animator that receives a bool when the alarm state changes.")]
	[SerializeField] private Animator animator;
	[Tooltip("Animator bool set to true while the alarm is active.")]
	[SerializeField] private string alarmBoolName = "AlarmActive";

	[Header("Unity Events")]
	[Tooltip("Invoked when alarm starts. Example: custom door lock.")]
	[SerializeField] private UnityEvent onAlarmStarted;
	[Tooltip("Invoked when alarm stops. Example: custom door unlock.")]
	[SerializeField] private UnityEvent onAlarmStopped;

	// Cached Animator parameter hash
	private int alarmBoolHash;

	private void Reset() {
		AutoWireReferences();
	}

	private void Awake() {
		AutoWireReferences();
		alarmBoolHash = Animator.StringToHash(alarmBoolName);
	}

	private void OnEnable() {
		SubscribeToAlarm();

		// Immediately match the current alarm state
		// This prevents objects being in the wrong active/inactive state if this responder enables late
		ApplyState(alarmController != null && alarmController.IsAlarmActive, false);
	}

	private void OnDisable() {
		UnsubscribeFromAlarm();
	}

	private void AutoWireReferences() {
		if (alarmController == null) {
			alarmController = FindFirstObjectByType<LevelAlarmController>();
		}

		if (animator == null) {
			animator = GetComponent<Animator>();
		}
	}

	private void SubscribeToAlarm() {
		if (alarmController == null) {
			return;
		}

		alarmController.AlarmStarted -= HandleAlarmStarted;
		alarmController.AlarmStopped -= HandleAlarmStopped;

		alarmController.AlarmStarted += HandleAlarmStarted;
		alarmController.AlarmStopped += HandleAlarmStopped;
	}

	private void UnsubscribeFromAlarm() {
		if (alarmController == null) {
			return;
		}

		alarmController.AlarmStarted -= HandleAlarmStarted;
		alarmController.AlarmStopped -= HandleAlarmStopped;
	}

	private void HandleAlarmStarted() {
		// Alarm has become active, so enable lockdown objects, update animator, and invoke start events
		ApplyState(true, true);
	}

	private void HandleAlarmStopped() {
		// Alarm has ended, so restore normal objects, update animator, and invoke stop events
		ApplyState(false, true);
	}
	
	private void ApplyState(bool alarmActive, bool invokeEvents) {
		// Enable objects such as lockdown barriers while alarm is active
		SetObjectsActive(enableDuringAlarm, alarmActive);

		// Disable objects such as normal lights while alarm is active
		SetObjectsActive(disableDuringAlarm, alarmActive == false);

		// Optional animator hook for doors, barriers, or UI panels
		if (animator != null && string.IsNullOrWhiteSpace(alarmBoolName) == false) {
			animator.SetBool(alarmBoolHash, alarmActive);
		}

		if (invokeEvents) {
			if (alarmActive) {
				onAlarmStarted?.Invoke();
			}
			else {
				onAlarmStopped?.Invoke();
			}
		}
	}

	private void SetObjectsActive(GameObject[] objects, bool active) {
		if (objects == null) {
			return;
		}

		// Null checks allow slots to be left empty without causing any runtime errors
		for (int i = 0; i < objects.Length; i++) {
			if (objects[i] != null) {
				objects[i].SetActive(active);
			}
		}
	}
}
