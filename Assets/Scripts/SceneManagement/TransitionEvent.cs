using UnityEngine;
using UnityEngine.Events;

public class TransitionEvent : MonoBehaviour {
	public UnityEvent onRequirementsMet;

	public void TriggerTransition() {
		onRequirementsMet.Invoke();
	}
}