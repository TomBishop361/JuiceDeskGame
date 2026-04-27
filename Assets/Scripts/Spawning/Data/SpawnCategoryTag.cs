using UnityEngine;

public class SpawnCategoryTag : MonoBehaviour {
	[SerializeField] private SpawnCategory category = SpawnCategory.Any;
	public SpawnCategory Category => category;
}