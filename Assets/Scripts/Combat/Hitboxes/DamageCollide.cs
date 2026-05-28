using UnityEngine;

public class DamageCollide : MonoBehaviour {
	[Header("Damage")]
	[Tooltip("Damage data applied when something damageable enters this trigger.")]
	[SerializeField] private AttackData attackData;

	private void Reset() {
		// Laser damage should be triggers
		Collider col = GetComponent<Collider>();

		if (col != null) {
			col.isTrigger = true;
		}
	}

	private void OnTriggerEnter(Collider other) {
		IDamageable damageable = FindDamageable(other);

		if (damageable == null) {
			return;
		}

		// Creates a local copy of attack data struct
		AttackData finalAttackData = attackData;

		// Needed for damage direction indicator
		if (finalAttackData.Attacker == null) {
			finalAttackData.Attacker = gameObject;
		}

		damageable.TakeDamage(finalAttackData);
	}

	private IDamageable FindDamageable(Collider other) {
		if (other == null) {
			return null;
		}

		// Prefer to use the Hurtbox first because PlayerDamageFeedbackController listens to Hurtbox damage events
		Hurtbox hurtbox = other.GetComponent<Hurtbox>();

		if (hurtbox == null) {
			hurtbox = other.GetComponentInParent<Hurtbox>();
		}

		if (hurtbox is IDamageable hurtboxDamageable) {
			return hurtboxDamageable;
		}

		// Fallback: for objects that implement IDamageable directly
		if (other.TryGetComponent(out IDamageable directDamageable)) {
			return directDamageable;
		}

		return other.GetComponentInParent<IDamageable>();
	}
}