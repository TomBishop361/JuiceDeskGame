using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CircleTransition : MonoBehaviour {
	//public RectTransform circle;
	//public float speed = 5.0f;
	//public float endScale = 35.0f;

	//private Transform player;
	////private float endScale = 35.0f;

	//private void Awake() {
	//	GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
	//	if (playerObj != null) {
	//		player = playerObj.transform;
	//	}
	//}
	//public void StartTransition(string sceneName) {
	//	StartCoroutine(Transition(sceneName));
	//}

	//private IEnumerator Transition(string sceneName) {
	//	// Expand circle
	//	while (circle.localScale.x < endScale) {
	//		circle.localScale += Vector3.one * speed * Time.deltaTime;
	//		// Centre on player
	//		circle.position = Camera.main.WorldToScreenPoint(player.position);
	//		yield return null;
	//	}

	//	SceneManager.LoadScene(sceneName);
	//}

	public Transform player;
	public RectTransform circle;
	public float shrinkSpeed = 5.0f;
	public float endScale = 35.0f;

	private void Start () {
		StartCoroutine(Close());
		//StartTransition("AI_Playtest_Testing");
	}

	public void StartTransition(string sceneName) {
		StartCoroutine(Open(sceneName));
	}

	private IEnumerator Close() {
		circle.localScale = Vector3.zero;

		while (circle.localScale.x < endScale) {
			circle.localScale += Vector3.one * Time.deltaTime * shrinkSpeed;
			yield return null;
		}
	}

	private IEnumerator Open(string sceneName) {
		Vector3 screenPos = Camera.main.WorldToScreenPoint(player.position);
		circle.position = screenPos;

		circle.localScale = Vector3.one * endScale;

		while (circle.localScale.x > 0.0f) {
			circle.localScale -= Vector3.one * Time.deltaTime * shrinkSpeed;
		
			yield return null;
		}

		SceneManager.LoadScene(sceneName);
	}
}