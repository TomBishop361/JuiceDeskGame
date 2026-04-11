using UnityEngine;

public class EnemyCombat : MonoBehaviour {
	private EnemyTracker tracker;

	void Start() {
		tracker = FindObjectOfType<EnemyTracker>();
	}

	public void TrackDeath() {
		tracker.EnemyDied();
		//Destroy(gameObject);
	}
}