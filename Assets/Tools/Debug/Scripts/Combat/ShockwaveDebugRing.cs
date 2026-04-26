using UnityEngine;

public class ShockwaveDebugRing : MonoBehaviour {
	private float expandDuration;
	private float holdDuration;
	private float maxRadius;
	private float time;
	private float yThickness;

	public void Init(float ringMaxRadius, float ringExpandDuration, float ringHoldDuration, float ringYThickness) {
		maxRadius = ringMaxRadius;
		expandDuration = Mathf.Max(0.01f, ringExpandDuration);
		holdDuration = Mathf.Max(0f, ringHoldDuration);
		yThickness = Mathf.Max(0.001f, ringYThickness);

		time = 0.0f;
		transform.localScale = new Vector3(0.0f, ringYThickness, 0.0f);
	}

	private void Update() {
		time += Time.deltaTime;

		// Expand - smoothly
		float tExpand = Mathf.Clamp01(time / expandDuration);
		tExpand = Mathf.SmoothStep(0f, 1f, tExpand);
		float diameter = maxRadius * 2f * tExpand;
		transform.localScale = new Vector3(diameter, yThickness, diameter);

		// Destroy after expand + hold
		if (time >= expandDuration + holdDuration) {
			Destroy(gameObject);
		}
			

		//time += Time.deltaTime;
		//float t = Mathf.Clamp01(time / duration);

		//// Expanding visual ring for shockwave
		//float diameter = maxRadius * 2.0f * t;
		//transform.localScale = new Vector3(diameter, transform.localScale.y, diameter);

		//if (time >= duration) {
		//	Destroy(gameObject);
		//}
	}
}
