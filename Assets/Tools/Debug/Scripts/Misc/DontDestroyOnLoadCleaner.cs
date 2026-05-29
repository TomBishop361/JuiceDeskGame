using UnityEngine;
using UnityEngine.SceneManagement;

public static class DontDestroyOnLoadCleaner {
	// Destroys every root object currently living in Unity's hidden DontDestroyOnLoad scene.
	// Useful for testing, restarting the game, or clearing persistent managers between levels.
	public static void DestroyAllDontDestroyOnLoadObjects() {
		// Create a temporary object so we can access the hidden DontDestroyOnLoad scene
		GameObject temp = new GameObject("Temp_DDOL_Scene_Finder");

		Object.DontDestroyOnLoad(temp);

		Scene dontDestroyScene = temp.scene;

		GameObject[] rootObjects = dontDestroyScene.GetRootGameObjects();

		foreach (GameObject rootObject in rootObjects) {
			Object.Destroy(rootObject);
		}
	}
}