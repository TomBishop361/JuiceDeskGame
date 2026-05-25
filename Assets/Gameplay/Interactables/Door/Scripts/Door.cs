using System.Collections;
using UnityEngine;

// Reusable door controller for normal doors
// Handles door lock state and open/close animation
// Seamless level-transition doors should be controlled by SeamlessScenePortal instead
[DisallowMultipleComponent]
public class Door : MonoBehaviour {
	[Header("Door Behaviour")]
	[Tooltip("If true, the door starts locked and will not open until UnlockDoor is called.")]
	[SerializeField] private bool startsLocked = false;
	[Tooltip("If true, this door opens when the player enters its trigger and closes when they leave.")]
	[SerializeField] private bool openFromTrigger = true;
	[Tooltip("Tag allowed to open this door.")]
	[SerializeField] private string playerTag = "Player";

	[Header("Animator")]
	[Tooltip("Animator trigger used to open the door.")]
	[SerializeField] private string openTriggerName = "Open";
	[Tooltip("Animator trigger used to close the door.")]
	[SerializeField] private string closeTriggerName = "Close";

	[Header("Debug")]
	[Tooltip("If true, the door process is logged to the Unity Console.")]
	[SerializeField] private bool debugLogging = true;

	//[SerializeField] private bool loadsScene = false;

	//[Header("Scene Loading")]
	//[SerializeField] private string sceneToLoad;
	//[SerializeField] private SceneTransition transition;
	//[SerializeField] private float loadDelay = 0.75f;

	private Animator animator;
	private bool unlocked = true;
	private bool isOpen;
	//private bool playerInside = false;
	//private bool loading = false;

	private void Awake() {
		animator = GetComponent<Animator>();
		unlocked = !startsLocked;
	}

	private void OnTriggerEnter(Collider other) {
		if (openFromTrigger == false) {
			return;
		}

		if (other.CompareTag(playerTag) == false) {
			return;
		}

		TryOpen();

		//if (unlocked == false) {
		//	return;
		//}

		//playerInside = true;

		//if (animator != null) {
		//	animator.SetTrigger("Open");
		//}

		//// Open door then load scene (if load scene is enabled)
		//if (loadsScene == true && loading == false) {
		//	loading = true;
		//	StartCoroutine(LoadSceneAfterOpen());
		//}
	}

	private void OnTriggerExit(Collider other) {
		if (openFromTrigger == false) {
			return;
		}

		if (other.CompareTag(playerTag) == false) {
			return;
		}

		TryClose();

		//if (other.CompareTag("Player") == false) {
		//	return;
		//}
		//if (unlocked == false) {
		//	return;
		//}

		//playerInside = false;

  //      if (animator != null)
  //      {
  //          animator.SetTrigger("Close");
  //      }

  //      // Close door on exit (if load scene is disabled)
  //      if (loadsScene == false && animator != null) {
		//	animator.SetTrigger("Close");
		//}
	}

	// Unlocks the door so it can be opened by trigger or external calls
	public void UnlockDoor() {
		unlocked = true;

		if (debugLogging) {
			Debug.Log($"{name} unlocked", this);
		}
	}

	// Locks the door and optionally closes it
	public void LockDoor() {
		unlocked = false;
		TryClose();

		if (debugLogging) {
			Debug.Log($"{name} locked", this);
		}
	}

	// Opens the door if it is unlocked
	public void TryOpen() {
		if (unlocked == false) {
			return;
		}

		if (isOpen) {
			return;
		}

		isOpen = true;
		SetAnimatorTrigger(openTriggerName);
	}

	// Closes the door
	public void TryClose() {
		if (isOpen == false) {
			return;
		}

		isOpen = false;
		SetAnimatorTrigger(closeTriggerName);
	}

	private void SetAnimatorTrigger(string triggerName) {
		if (animator == null || string.IsNullOrWhiteSpace(triggerName)) {
			return;
		}

		animator.SetTrigger(triggerName);
	}

	//private IEnumerator LoadSceneAfterOpen() {
	//	yield return new WaitForSeconds(loadDelay);

	//	if (transition != null && !string.IsNullOrEmpty(sceneToLoad)) {
	//		transition.LoadScene(sceneToLoad);
	//	}
	//	else {
	//		Debug.LogWarning($"Door '{name}' is set to load a scene but is missing transition or scene name");
	//	}
	//}

	//public GameObject door;

	//private void OnTriggerEnter(Collider other) {
	//       if (other.CompareTag("Player")) {
	//           GetComponent<Animator>().SetTrigger("Open");
	//       }
	//   }

	//private void OnTriggerExit(Collider other) {
	//	if (other.CompareTag("Player")) {
	//		GetComponent<Animator>().SetTrigger("Close");
	//	}
	//}
}