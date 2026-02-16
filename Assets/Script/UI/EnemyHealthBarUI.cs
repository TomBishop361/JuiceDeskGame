using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBarUI : MonoBehaviour {
	[SerializeField] private Health health;
	[SerializeField] private Image image;
	[SerializeField] private Vector3 uiOffset = new Vector3(0, 2.0f, 0);

	private Camera mainCamera;

	[SerializeField] private Color fullHealthColor = Color.green;
	[SerializeField] private Color lowHealthColor = Color.red;

	private void Awake() {
		if (gameObject.tag != "Player") {
			mainCamera = Camera.main;
		}

		health.OnHealthChanged += UpdateHealthBar;
		health.OnDeath += HideHealthBar;
	}

	private void LateUpdate() {
		transform.position = health.transform.position + uiOffset;
		if (gameObject.tag != "Player") {
			transform.forward = mainCamera.transform.forward;
		}
	}

	private void UpdateHealthBar(float currentHealth, float maxHealth) {
		float healthPercent = currentHealth / maxHealth;

		image.fillAmount = healthPercent;
		image.color = Color.Lerp(lowHealthColor, fullHealthColor, healthPercent);
	}

	private void HideHealthBar() {
		gameObject.SetActive(false);
	}
}