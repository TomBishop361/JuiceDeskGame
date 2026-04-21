using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.AI.Shield {
	[DisallowMultipleComponent]
	[RequireComponent(typeof(PooledObject))]
	[RequireComponent(typeof(LineRenderer))]
	public sealed class ShieldMinigunTracer : MonoBehaviour, IPoolLifecycleHandler {
		[SerializeField] private LineRenderer lineRenderer;
		[SerializeField] private float defaultWidth = 0.025f;

		private PooledObject pooledObject;
		private Coroutine lifetimeRoutine;

		//private IObjectPool<ShieldMinigunTracer> ownerPool;
		//private LineRenderer cachedLineRenderer;
		//private float despawnTime = -Mathf.Infinity;
		//private bool isActive;

		private void Awake() {
			if (lineRenderer == null) {
				lineRenderer = GetComponent<LineRenderer>();
			}

			pooledObject = GetComponent<PooledObject>();
		}


		public void Play(Vector3 start, Vector3 end, float lifetime) {
			if (lineRenderer == null) {
				return;
			}

			lineRenderer.positionCount = 2;
			lineRenderer.startWidth = defaultWidth;
			lineRenderer.endWidth = defaultWidth;
			lineRenderer.SetPosition(0, start);
			lineRenderer.SetPosition(1, end);

			if (lifetimeRoutine != null) {
				StopCoroutine(lifetimeRoutine);
			}

			lifetimeRoutine = StartCoroutine(ReturnAfterLifetime(lifetime));
		}

		private IEnumerator ReturnAfterLifetime(float lifetime) {
			yield return new WaitForSeconds(Mathf.Max(0.0f, lifetime));

			if (pooledObject != null) {
				pooledObject.ReturnToPool();
			}

			lifetimeRoutine = null;
		}

		public void OnSpawned() {
			// Nothing required here, but kept since it needs implementing by IPoolLifecycleHandler
		}

		public void OnDespawned() {
			if (lifetimeRoutine != null) {
				StopCoroutine(lifetimeRoutine);
				lifetimeRoutine = null;
			}
		}
	}
}