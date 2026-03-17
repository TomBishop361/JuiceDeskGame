using UnityEngine;
using UnityEngine.Events;

public class RestartLevel_DevCmd : MonoBehaviour {
	public UnityEvent onLevelRestartCmd;

	private bool triggered = false;

	public void RestartLevelCmd() {
		if (triggered == true) {
			return;
		}
		triggered = true;
		onLevelRestartCmd.Invoke();
	}

	private void Update() {
		if (/*(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && */Input.GetKeyDown(KeyCode.BackQuote)) {
			RestartLevelCmd();
		}
	}
}