#if UNITY_EDITOR
using UnityEngine;

public class EnemyDebugOverlay : MonoBehaviour {
	[Header("References")]
	[SerializeField] private EnemyTracker enemyTracker;

	[Header("Layout")]
	[SerializeField] private Vector2 position = new Vector2(15.0f, 15.0f);
	[SerializeField] private Vector2 size = new Vector2(260.0f, 160.0f);

	[Header("Behaviour")]
	[SerializeField] private bool showOverlay = true;
	[SerializeField] private KeyCode toggleKey = KeyCode.F3;

	private GUIStyle boxStyle;
	private GUIStyle labelStyle;
	private bool stylesBuilt;

	private void Update() {
		if (!Application.isPlaying) {
			return;
		}

		if (Input.GetKeyDown(toggleKey)) {
			showOverlay = !showOverlay;
		}
	}

	private void EnsureStyles() {
		if (stylesBuilt) {
			return;
		}

		boxStyle = new GUIStyle(GUI.skin.box);
		boxStyle.alignment = TextAnchor.UpperLeft;
		boxStyle.fontSize = 12;
		boxStyle.padding = new RectOffset(10, 10, 10, 10);

		labelStyle = new GUIStyle(GUI.skin.label);
		labelStyle.fontSize = 12;
		labelStyle.richText = true;

		stylesBuilt = true;
	}

	private void OnGUI() {
		if (!Application.isPlaying || !showOverlay) {
			return;
		}

		EnsureStyles();

		Rect rect = new Rect(position.x, position.y, size.x, size.y);
		GUI.Box(rect, GUIContent.none, boxStyle);

		const float leftPadding = 12f;
		const float topPadding = 8f;
		const float rightPadding = 10f;
		const float bottomPadding = 10f;

		Rect contentRect = new Rect(
			rect.x + leftPadding,
			rect.y + topPadding,
			rect.width - leftPadding - rightPadding,
			rect.height - topPadding - bottomPadding
		);

		GUILayout.BeginArea(contentRect);
		GUILayout.Label("<b>Enemy Debug</b>", labelStyle);
		GUILayout.Space(6f);

		if (enemyTracker == null) {
			GUILayout.Label("No EnemyTracker assigned.", labelStyle);
			GUILayout.EndArea();
			return;
		}

		GUILayout.Label($"<b>Total Planned:</b> {enemyTracker.TotalPlanned}", labelStyle);
		GUILayout.Label($"<b>Spawned So Far:</b> {enemyTracker.SpawnedSoFar}", labelStyle);
		GUILayout.Label($"<b>Alive Now:</b> {enemyTracker.AliveNow}", labelStyle);
		GUILayout.Label($"<b>Killed So Far:</b> {enemyTracker.KilledSoFar}", labelStyle);
		GUILayout.Label($"<b>Remaining To Kill:</b> {enemyTracker.RemainingToKill}", labelStyle);

		GUILayout.EndArea();
	}
}
#endif