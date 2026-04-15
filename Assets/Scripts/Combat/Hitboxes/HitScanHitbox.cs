using UnityEngine;

// Shared HitScan Hitbox for Player, Enemies and Boss (if boss uses hitscan at all) 
// Determines where a hitscan hits

// NOTE: Apply to the object root - Entity shooting hitscan - cast ray - supplies HitScanHitbox with the hit and attackdata - HitScanHitbox deals with whether the hit should be notified to handle damage being applied or not 
public class HitScanHitbox : MonoBehaviour {

	// Called by HitScan.cs when raycast detects a valid hit
	public void ApplyHitScanHit(RaycastHit hit, AttackData attackData) {

		//if (hit.collider.TryGetComponent(out IDamageable damageableInterface) == false) {
		//	return;
		//}

		IDamageable damageableInterface = hit.collider.GetComponentInParent<IDamageable>();
		if (damageableInterface == null) {
			Debug.LogError("IDamageable: not found in parent of: " + hit.collider.gameObject.name);
			return;
		}

		// Damage handling dealt with inside target HurtBox.cs script
		damageableInterface.TakeDamage(attackData);
	}
}