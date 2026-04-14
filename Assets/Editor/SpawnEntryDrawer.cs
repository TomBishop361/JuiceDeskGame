using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SpawnEntry))]
public class SpawnEntryDrawer : PropertyDrawer {
	private const float VerticalSpacing = 2f;

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		EditorGUI.BeginProperty(position, label, property);

		SerializedProperty spawnSourceProp = property.FindPropertyRelative("spawnSource");
		SerializedProperty prefabProp = property.FindPropertyRelative("prefab");
		SerializedProperty categoryProp = property.FindPropertyRelative("category");
		SerializedProperty countProp = property.FindPropertyRelative("count");
		SerializedProperty delayProp = property.FindPropertyRelative("delayBetweenSpawns");

		Rect rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

		property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, label, true);

		if (property.isExpanded) {
			EditorGUI.indentLevel++;

			rect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
			EditorGUI.PropertyField(rect, spawnSourceProp);

			rect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;

			SpawnEntry.SpawnSource spawnSource =
				(SpawnEntry.SpawnSource)spawnSourceProp.enumValueIndex;

			if (spawnSource == SpawnEntry.SpawnSource.SpecificPrefab) {
				EditorGUI.PropertyField(rect, prefabProp);
			}
			else {
				EditorGUI.PropertyField(rect, categoryProp);
			}

			rect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
			EditorGUI.PropertyField(rect, countProp);

			rect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
			EditorGUI.PropertyField(rect, delayProp);

			EditorGUI.indentLevel--;
		}

		EditorGUI.EndProperty();
	}

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
		if (!property.isExpanded)
			return EditorGUIUtility.singleLineHeight;

		// Foldout + spawnSource + prefab/category + count + delay
		int lines = 5;
		return (EditorGUIUtility.singleLineHeight * lines) + (VerticalSpacing * (lines - 1));
	}
}