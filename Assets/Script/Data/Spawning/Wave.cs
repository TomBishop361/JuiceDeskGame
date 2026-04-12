using UnityEngine;

[System.Serializable]
public class Wave {
	[Tooltip("Everything that should spawn in this wave.")]
	public SpawnEntry[] entries;

	[Tooltip("Optional pause after this wave is cleared.")]
	public float delayAfterWaveCleared = 1.0f;
}