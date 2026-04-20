using UnityEngine;

namespace Game.AI.Shield {
	[DisallowMultipleComponent]
	public sealed class ShieldAdvanceMotor : MonoBehaviour {
		[Header("References")]
		[SerializeField] private GroundEnemyMotor groundMotor;

		private void Reset() {
			if (groundMotor == null) {
				groundMotor = GetComponent<GroundEnemyMotor>();
			}
		}

		public void ResetRuntime() {
		}

		public void TickAdvance(ShieldEnemy owner, Transform target) {
			if (owner == null || target == null || groundMotor == null) {
				return;
			}

			groundMotor.Chase(target);
			groundMotor.FaceTarget(target.position);
			owner.SetShieldRaised(true);
		}

		public void Stop() {
			groundMotor?.Stop();
		}
	}
}
