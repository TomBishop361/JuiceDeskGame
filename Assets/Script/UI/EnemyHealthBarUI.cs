using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

public class EnemyHealthBarUI : MonoBehaviour {
	[Header("References")]
	[SerializeField] private Health health;
	[SerializeField] private Slider healthSlider;
	[SerializeField] private Image fillImage; // image inside slider to fill
	[SerializeField] private TMPro.TextMeshProUGUI hpText;

	[Header("World Space")]
	[SerializeField] private bool followTarget = true;
	[SerializeField] private Vector3 uiOffset = new Vector3(0, 2.0f, 0);

	private Camera mainCamera;

	[Header("Colors")]
	[SerializeField] private Color fullHealthColor = Color.green;
	[SerializeField] private Color lowHealthColor = Color.red;

	[Header("Segments")]
	[SerializeField] private RectTransform segmentsContainer;
	[SerializeField] private GameObject segmentDividerPrefab;

	private void Awake() {
		// TODO: ADD BACK LATER WHEN PLAYER DOES NOT USE THIS SCRIPT FOR THEIR HP BAR
		//if (gameObject.tag != "Player") {
		//	mainCamera = Camera.main;
		//}

		mainCamera = Camera.main;

		// Initialise slider values
		healthSlider.minValue = 0.0f;
		healthSlider.maxValue = 1.0f;
		healthSlider.interactable = false;

		health.OnHealthChanged += UpdateHealthBar;
		health.OnDeath += HideHealthBar;
	}
	private void Start() {
		float maxHealth = health.MaxHealth;
		float currentHealth = health.CurrentHealth;
		BuildSegments((int)maxHealth);
		UpdateHealthBar(currentHealth, maxHealth);

		//currentHealth = maxHealth;// new
		//healthSlider.maxValue = health.MaxHealth; // new
		//healthSlider.value = health.CurrentHealth; // new
	}

	private void LateUpdate() {
		if (followTarget == false) {
			return;
		}

		transform.position = health.transform.position + uiOffset;
		transform.forward = mainCamera.transform.forward;

		// TODO: ADD BACK LATER WHEN PLAYER DOES NOT USE THIS SCRIPT FOR THEIR HP BAR
		//if (gameObject.tag != "Player") {
		//	transform.forward = mainCamera.transform.forward;
		//}
	}

	private void UpdateHealthBar(float currentHealth, float maxHealth) {
		float healthPercent = currentHealth / maxHealth;

		healthSlider.value = healthPercent;

		if (fillImage != null) {
			fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor, healthPercent);
		}
		if (hpText != null) {
			//string currentHPText = currentHealth.ToString("F2");
			//hpText.text = $"{currentHealth.ToString("F2")} / {(int)maxHealth}";
			hpText.text = $"{Mathf.Max(1, Mathf.CeilToInt(currentHealth))} / {(int)maxHealth}";
		}
		//image.fillAmount = healthPercent;
		//image.color = Color.Lerp(lowHealthColor, fullHealthColor, healthPercent);
	}

	private void BuildSegments(int maxHealth) {
		if (segmentsContainer == null || segmentDividerPrefab == null || maxHealth <= 1) {
			return;
		}
		
		foreach (Transform child in segmentsContainer) {
			Destroy(child.gameObject);
		}

		float width = segmentsContainer.rect.width;

		for (int i = 1; i < maxHealth; i++) {
			GameObject divider = Instantiate(segmentDividerPrefab, segmentsContainer);
			RectTransform rt = divider.GetComponent<RectTransform>();

			float x = (width * i / maxHealth);

			rt.anchorMin = new Vector2(0f, 0f);
			rt.anchorMax = new Vector2(0f, 1f);
			rt.pivot = new Vector2(0.5f, 0.5f);
			rt.anchoredPosition = new Vector2(x, 0f);
		}
	}

	private void HideHealthBar() {
		gameObject.SetActive(false);
	}
}