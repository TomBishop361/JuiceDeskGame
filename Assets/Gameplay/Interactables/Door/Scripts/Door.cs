using System.Collections;
using UnityEngine;

public class Door : MonoBehaviour {
	[Header("Door Behaviour")]
	[SerializeField] private bool startsLocked = false;
	[SerializeField] private bool loadsScene = false;

	[Header("Scene Loading")]
	[SerializeField] private string sceneToLoad;
	[SerializeField] private SceneTransition transition;
	[SerializeField] private float loadDelay = 0.75f;

	private Animator animator;
	private bool unlocked = true;
	private bool playerInside = false;
	private bool loading = false;

	private void Awake() {
		animator = GetComponent<Animator>();
		unlocked = !startsLocked;
	}

	public void UnlockDoor() {
		unlocked = true;
		Debug.Log($"{name} unlocked");
	}

	private void OnTriggerEnter(Collider other) {
		if (other.CompareTag("Player") == false) {
			return;
		}
		if (unlocked == false) {
			return;
		}

		playerInside = true;

		if (animator != null) {
			animator.SetTrigger("Open");
		}

		// Open door then load scene (if load scene is enabled)
		if (loadsScene == true && loading == false) {
			loading = true;
			StartCoroutine(LoadSceneAfterOpen());
		}
	}

	private void OnTriggerExit(Collider other) {
		if (other.CompareTag("Player") == false) {
			return;
		}
		if (unlocked == false) {
			return;
		}

		playerInside = false;

		// Close door on exit (if load scene is disabled)
		if (loadsScene == false && animator != null) {
			animator.SetTrigger("Close");
		}
	}

	private IEnumerator LoadSceneAfterOpen() {
		yield return new WaitForSeconds(loadDelay);

		if (transition != null && !string.IsNullOrEmpty(sceneToLoad)) {
			transition.LoadScene(sceneToLoad);
		}
		else {
			Debug.LogWarning($"Door '{name}' is set to load a scene but is missing transition or scene name");
		}
	}

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