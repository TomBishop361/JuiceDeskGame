using System.Collections;
using UnityEngine;

// Persistent coroutine runner used by scene transitions
// This lets the airlock transition continue even if the old level scene unloads
// and destroys the door, trigger, or portal object that started the transition
[DisallowMultipleComponent]
public sealed class SeamlessSceneRuntimeRunner : MonoBehaviour {
	private static SeamlessSceneRuntimeRunner instance;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStatics() {
		instance = null;
	}

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