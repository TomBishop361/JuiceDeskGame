using System.Collections;
using UnityEngine;

// Coroutine runner used only for delayed scene unloads
// It exists so the previous scene can unload even after the door object is destroyed
public sealed class SeamlessSceneRuntimeRunner : MonoBehaviour {
	private static SeamlessSceneRuntimeRunner instance;

	// Runs a coroutine on a hidden DontDestroyOnLoad object
	public static Coroutine Run(IEnumerator routine) {
		if (routine == null) {
			return null;
		}

		return GetOrCreate().StartCoroutine(routine);
	}

	private static SeamlessSceneRuntimeRunner GetOrCreate() {
		if (instance != null) {
			return instance;
		}

		GameObject runnerObject = new GameObject("SeamlessSceneRuntimeRunner");
		runnerObject.hideFlags = HideFlags.HideAndDontSave;
		DontDestroyOnLoad(runnerObject);

		instance = runnerObject.AddComponent<SeamlessSceneRuntimeRunner>();
		return instance;
	}

	private void Awake() {
		if (instance != null && instance != this) {
			Destroy(gameObject);
			return;
		}

		instance = this;
		DontDestroyOnLoad(gameObject);
	}
}