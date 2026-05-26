using UnityEngine;

/// <summary>
/// Animates the shield enemy eye material by shifting the UV offset across a texture atlas.
/// This is used instead of a SpriteRenderer because the shield enemy already has a dedicated eye material slot.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class ShieldEnemyEyeMaterialAnimator : MonoBehaviour {
	[Header("Renderer")]
	[Tooltip("Renderer that contains the shield enemy eye material slot. This is usually the SkinnedMeshRenderer on the shield enemy model.")]
	[SerializeField] private Renderer targetRenderer;

	[Tooltip("The material slot index used by the eyes. Example: if the Eyes material is Element 2, set this to 2.")]
	[SerializeField] private int eyeMaterialIndex = 0;

	[Header("Sprite Sheet Layout")]
	[Tooltip("Number of columns in the eye sprite sheet.")]
	[SerializeField] private int columns = 7;

	[Tooltip("Number of rows in the eye sprite sheet.")]
	[SerializeField] private int rows = 12;

	[Tooltip("Total valid frames in the sheet. For a full 7x12 sheet, this is 84.")]
	[SerializeField] private int totalFrames = 84;

	[Header("Playback")]
	[Tooltip("How many eye frames should play per second.")]
	[SerializeField] private float framesPerSecond = 12.0f;

	[Tooltip("If true, the eye animation starts automatically when enabled.")]
	[SerializeField] private bool playOnEnable = true;

	[Tooltip("If true, the current eye animation loops.")]
	[SerializeField] private bool loop = true;

	private MaterialPropertyBlock propertyBlock;
	private int currentFrame;
	private int startFrame;
	private int endFrame;
	private float frameTimer;
	private bool isPlaying;

	private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
	private static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");

	private void Awake() {
		CacheReferences();

		startFrame = 0;
		endFrame = Mathf.Max(0, totalFrames - 1);

		ApplyFrame(0);
	}

	private void OnEnable() {
		currentFrame = startFrame;
		frameTimer = 0.0f;

		if (playOnEnable == true) {
			isPlaying = true;
		}

		ApplyFrame(currentFrame);
	}

	private void Update() {
		if (isPlaying == false) {
			return;
		}

		UpdateAnimation(Time.deltaTime);
	}

	/// <summary>
	/// Caches the renderer and creates a reusable property block.
	/// </summary>
	private void CacheReferences() {
		if (targetRenderer == null) {
			targetRenderer = GetComponent<Renderer>();
		}

		if (propertyBlock == null) {
			propertyBlock = new MaterialPropertyBlock();
		}
	}

	/// <summary>
	/// Plays the full eye sheet from frame 0 to the final valid frame.
	/// </summary>
	public void PlayFullLoop() {
		PlayRange(0, totalFrames - 1, true);
	}

	/// <summary>
	/// Plays a specific section of the eye sprite sheet.
	/// Useful for idle, attack, exposed, and death eye states.
	/// </summary>
	public void PlayRange(int newStartFrame, int newEndFrame, bool shouldLoop) {
		startFrame = Mathf.Clamp(newStartFrame, 0, Mathf.Max(0, totalFrames - 1));
		endFrame = Mathf.Clamp(newEndFrame, startFrame, Mathf.Max(0, totalFrames - 1));

		currentFrame = startFrame;
		frameTimer = 0.0f;
		loop = shouldLoop;
		isPlaying = true;

		ApplyFrame(currentFrame);
	}

	/// <summary>
	/// Stops the animation and holds the current eye frame.
	/// </summary>
	public void Stop() {
		isPlaying = false;
	}

	/// <summary>
	/// Sets one exact eye frame and stops playback.
	/// </summary>
	public void SetStaticFrame(int frameIndex) {
		currentFrame = Mathf.Clamp(frameIndex, 0, Mathf.Max(0, totalFrames - 1));
		isPlaying = false;

		ApplyFrame(currentFrame);
	}

	/// <summary>
	/// Advances the eye frame based on the configured frames per second.
	/// </summary>
	private void UpdateAnimation(float deltaTime) {
		float frameDuration = 1.0f / Mathf.Max(1.0f, framesPerSecond);
		frameTimer += deltaTime;

		if (frameTimer < frameDuration) {
			return;
		}

		frameTimer -= frameDuration;
		currentFrame++;

		if (currentFrame > endFrame) {
			if (loop == true) {
				currentFrame = startFrame;
			}
			else {
				currentFrame = endFrame;
				isPlaying = false;
			}
		}

		ApplyFrame(currentFrame);
	}

	/// <summary>
	/// Converts the current frame index into texture tiling and offset values.
	/// Frame 0 is treated as the top-left frame of the sprite sheet.
	/// </summary>
	private void ApplyFrame(int frameIndex) {
		if (targetRenderer == null) {
			return;
		}

		int safeColumns = Mathf.Max(1, columns);
		int safeRows = Mathf.Max(1, rows);

		float tileX = 1.0f / safeColumns;
		float tileY = 1.0f / safeRows;

		int column = frameIndex % safeColumns;
		int row = frameIndex / safeColumns;

		float offsetX = column * tileX;

		// Unity texture V starts at the bottom, but sprite sheets are usually read top-to-bottom.
		float offsetY = 1.0f - tileY - row * tileY;

		Vector4 textureScaleOffset = new Vector4(tileX, tileY, offsetX, offsetY);

		targetRenderer.GetPropertyBlock(propertyBlock, eyeMaterialIndex);

		// URP shaders commonly use _BaseMap_ST.
		propertyBlock.SetVector(BaseMapST, textureScaleOffset);

		// Built-in/older shaders commonly use _MainTex_ST.
		propertyBlock.SetVector(MainTexST, textureScaleOffset);

		targetRenderer.SetPropertyBlock(propertyBlock, eyeMaterialIndex);
	}
}