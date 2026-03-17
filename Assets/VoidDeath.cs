using UnityEngine;
using UnityEngine.Events;

// TODO: REFINE THIS - use a TriggerEvent script and call this since this is not a tracker
public class VoidDeath : MonoBehaviour {
	public UnityEvent onPlayerFellInVoid;

	private bool triggered = false;

	public void PlayerFellInVoid() {
		if (triggered == true) {
			return;
		}
		triggered = true;
		onPlayerFellInVoid.Invoke();
	}

	private void OnTriggerEnter(Collider other) {
		if (other.gameObject.CompareTag("Player") == true) {
			PlayerFellInVoid();
		}
	}

}