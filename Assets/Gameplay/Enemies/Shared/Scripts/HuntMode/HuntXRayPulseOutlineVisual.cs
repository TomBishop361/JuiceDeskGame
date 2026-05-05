using UnityEngine;

// Adapter for your Custom/XRayPulseOutline shader.
//
// Add this to the enemy prefab, then assign it in EnemyHuntTarget
// or leave EnemyHuntTarget autoFindRevealVisuals enabled.
[DisallowMultipleComponent]
public sealed class HuntXRayPulseOutlineVisual : MonoBehaviour, IHuntRevealVisual {
	[Header("Renderers")]
	[Tooltip("Enemy renderers that should receive the x-ray outline material. If empty, renderers are found in children.")]
	[SerializeField] private Renderer[] targetRenderers;

	[Header("Material")]
	[Tooltip("Material using the Custom/XRayPulseOutline shader.")]
	[SerializeField] private Material outlineMaterial;

	[Tooltip("If true, the outline material is appended to each renderer at runtime.")]
	[SerializeField] private bool appendOutlineMaterialOnAwake = true;

	[Header("Shader Properties")]
	[Tooltip("Alpha property used by the Custom/XRayPulseOutline shader.")]
	[SerializeField] private string alphaPropertyName = "_Alpha";

	private MaterialPropertyBlock propertyBlock;
	private int alphaPropertyId;
	private bool revealVisible;
	private float revealOpacity;

	private void Reset() {
		CollectRenderers();
	}

	private void Awake() {
		propertyBlock = new MaterialPropertyBlock();
		alphaPropertyId = Shader.PropertyToID(alphaPropertyName);

		if (targetRenderers == null || targetRenderers.Length == 0) {
			CollectRenderers();
		}

		if (appendOutlineMaterialOnAwake) {
			AppendOutlineMaterial();
		}

		SetRevealVisible(false);
		SetRevealOpacity(0.0f);
	}

	private void OnDisable() {
		SetRevealVisible(false);
		SetRevealOpacity(0.0f);
	}

	public void SetRevealVisible(bool visible) {
		revealVisible = visible;
		ApplyOpacity();
	}

	public void SetRevealOpacity(float opacity) {
		revealOpacity = Mathf.Clamp01(opacity);
		ApplyOpacity();
	}

	private void CollectRenderers() {
		targetRenderers = GetComponentsInChildren<Renderer>(true);
	}

	private void AppendOutlineMaterial() {
		if (outlineMaterial == null || targetRenderers == null) {
			return;
		}

		for (int i = 0; i < targetRenderers.Length; i++) {
			Renderer targetRenderer = targetRenderers[i];

			if (targetRenderer == null) {
				continue;
			}

			Material[] currentMaterials = targetRenderer.sharedMaterials;

			for (int materialIndex = 0; materialIndex < currentMaterials.Length; materialIndex++) {
				if (currentMaterials[materialIndex] == outlineMaterial) {
					return;
				}
			}

			Material[] newMaterials = new Material[currentMaterials.Length + 1];

			for (int materialIndex = 0; materialIndex < currentMaterials.Length; materialIndex++) {
				newMaterials[materialIndex] = currentMaterials[materialIndex];
			}

			newMaterials[newMaterials.Length - 1] = outlineMaterial;
			targetRenderer.sharedMaterials = newMaterials;
		}
	}

	private void ApplyOpacity() {
		if (targetRenderers == null) {
			return;
		}

		float finalAlpha = revealVisible ? revealOpacity : 0.0f;

		for (int i = 0; i < targetRenderers.Length; i++) {
			Renderer targetRenderer = targetRenderers[i];

			if (targetRenderer == null) {
				continue;
			}

			Material[] materials = targetRenderer.sharedMaterials;

			for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++) {
				if (materials[materialIndex] != outlineMaterial) {
					continue;
				}

				targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
				propertyBlock.SetFloat(alphaPropertyId, finalAlpha);
				targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
			}
		}
	}
}