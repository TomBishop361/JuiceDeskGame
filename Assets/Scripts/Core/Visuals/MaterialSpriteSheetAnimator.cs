using UnityEngine;

// Animates a material texture sprite sheet by shifting the material UV offset
// Useful for stuff like enemy eyes, screens, warning panels etc
[RequireComponent(typeof(Renderer))]
public class MaterialSpriteSheetAnimator : MonoBehaviour {
	[Header("Renderer")]
	[Tooltip("Renderer that contains the material slot you want to animate.")]
	[SerializeField] private Renderer targetRenderer;
	[Tooltip("Material slot index to animate.")]
	[SerializeField] private int materialIndex = 0;

	[Header("Sprite Sheet Layout")]
	[Tooltip("Number of columns in the sprite sheet.")]
	[SerializeField] private int columns = 4;
	[Tooltip("Number of rows in the sprite sheet.")]
	[SerializeField] private int rows = 4;
	[Tooltip("Total valid frames in the sheet.")]
	[SerializeField] private int totalFrames = 16;

	[Header("Playback")]
	[Tooltip("How many frames should play per second.")]
	[SerializeField] private float framesPerSecond = 12.0f;
	[Tooltip("If true, the animation starts automatically when enabled.")]
	[SerializeField] private bool playOnEnable = true;
	[Tooltip("If true, the current animation loops.")]
	[SerializeField] private bool loop = true;

	private MaterialPropertyBlock propertyBlock;
	private int currentFrame;
	private int startFrame;
	private int endFrame;
	private float frameTimer;
	private bool isPlaying;

	private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

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

	// Caches the renderer and creates a reusable material property block
	private void CacheReferences() {
		if (targetRenderer == null) {
			targetRenderer = GetComponent<Renderer>();
		}

		if (propertyBlock == null) {
			propertyBlock = new MaterialPropertyBlock();
		}
	}

	// Plays the full sprite sheet from frame 0 to the final valid frame
	public void PlayFullLoop() {
		PlayRange(0, totalFrames - 1, true);
	}

	// Plays a specific frame range inside the sprite sheet
	public void PlayRange(int newStartFrame, int newEndFrame, bool shouldLoop) {
		startFrame = Mathf.Clamp(newStartFrame, 0, Mathf.Max(0, totalFrames - 1));
		endFrame = Mathf.Clamp(newEndFrame, startFrame, Mathf.Max(0, totalFrames - 1));

		currentFrame = startFrame;
		frameTimer = 0.0f;
		loop = shouldLoop;
		isPlaying = true;

		ApplyFrame(currentFrame);
	}

	// Stops the animation and holds the current frame
	public void Stop() {
		isPlaying = false;
	}

	// Sets one exact frame and stops playback
	public void SetStaticFrame(int frameIndex) {
		currentFrame = Mathf.Clamp(frameIndex, 0, Mathf.Max(0, totalFrames - 1));
		isPlaying = false;

		ApplyFrame(currentFrame);
	}

	// Advances the sprite sheet frame based on the configured frame rate
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

	// Converts a frame index into material texture tiling and offset values
	// Frame 0 is treated as the top-left frame of the sprite sheet
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

		float offsetY = 1.0f - tileY - row * tileY;

		Vector4 textureScaleOffset = new Vector4(tileX, tileY, offsetX, offsetY);

		targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);

		// URP shaders commonly use _BaseMap_ST.
		propertyBlock.SetVector(BaseMapST, textureScaleOffset);

		targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
	}
}