using UnityEngine;
using UnityEngine.SceneManagement;

public class ResetLevel1_DevCmd : MonoBehaviour {
	private void Update() {
		if (Input.GetKeyDown(KeyCode.F1)) {
			SceneManager.LoadScene("Level_1");
		}
	}
}