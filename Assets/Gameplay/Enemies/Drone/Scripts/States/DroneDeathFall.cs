using System.Collections;
using UnityEngine;

namespace Game.AI.Drone {
    public sealed class DroneDeathFall : MonoBehaviour {
		[Header("Death Settings")]
		[SerializeField] private float deathFallSpeed = 6.0f;
		[SerializeField] private float deathDuration = 0.45f;

		[Header("VFX")]
		[SerializeField] private ParticleSystem deathFX;

		private Coroutine fallRoutine;

		// Resets death fall module state for pooling or respawn
		public void ResetRuntime() {
			if (fallRoutine != null) {
				StopCoroutine(fallRoutine);
				fallRoutine = null;
			}
		}

		// Start the death fall transition for the drone
		public void Play(DroneEnemy owner, EnemyDeathHandler deathHandler, /*Animator animator,*/ Transform sourceTarget) {
			//if (animator != null) {
			//	animator.SetTrigger("Die");
			//}

			// TODO: death FX
			if (deathFX != null) {
				Instantiate(deathFX, transform.position, Quaternion.identity);
			}

			if (fallRoutine != null) {
				StopCoroutine(fallRoutine);
			}

			// Delay despawn so the death can actually be seen
			fallRoutine = StartCoroutine(FallRoutine(owner, deathHandler));
		}


		// Perform fall + rotation on death
		// TODO: ATTEMPT ORIGINAL WAY + OTHER WAYS AS WELL
		private IEnumerator FallRoutine(DroneEnemy owner, EnemyDeathHandler deathHandler) {
			// Safety guard
			if (deathDuration <= 0.0f) {
				if (deathHandler != null) {
					deathHandler.ReturnToPoolNow();
				}
				yield break;
			}

			float timer = 0.0f;
			Vector3 startPos = transform.position;

			// Random sideways drift + downwards
			Vector3 randomDir = new Vector3(
				Random.Range(-0.8f, 0.8f), 
				Random.Range(-1.2f, -0.8f), 
				Random.Range(-0.8f, 0.8f)
			).normalized;

			Vector3 endPos = startPos + randomDir * (deathFallSpeed * deathDuration);

			// Random spin speed
			Vector3 spinSpeed = new Vector3(
				Random.Range(180.0f, 540.0f),
				Random.Range(180.0f, 540.0f),
				Random.Range(180.0f, 540.0f)
			);

			while (timer < deathDuration) {

				timer += Time.deltaTime;
				float t = timer / deathDuration;

				// Fall
				transform.position = Vector3.Lerp(startPos, endPos, t);

				// Spin while falling
				transform.Rotate(spinSpeed * Time.deltaTime, Space.Self);

				yield return null;
			}

			// Snap to exact final position
			transform.position = endPos;

			if (deathHandler != null) {
				deathHandler.ReturnToPoolNow();
			}
		}
	}
}