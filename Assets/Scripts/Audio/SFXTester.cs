using UnityEngine;

namespace Game.Audio {
	public class SFXTester : MonoBehaviour {
		[Header("Test Sounds")]
		[Tooltip("Sound played when pressing Key 1. Usually a 2D UI test sound.")]
		[SerializeField] private SFXDefinition test2DSound;
		[Tooltip("Sound played when pressing Key 2. Usually a 3D positional sound.")]
		[SerializeField] private SFXDefinition test3DSound;
		[Tooltip("Sound played when pressing Key 3. Usually an attached looping or moving sound.")]
		[SerializeField] private SFXDefinition testAttachedSound;

		[Header("3D Test")]
		[Tooltip("Where the 3D test sound should play. If empty, this object's position is used.")]
		[SerializeField] private Transform worldSoundPoint;

		private AudioSource attachedSource;

		private void Update() {
			if (Input.GetKeyDown(KeyCode.Alpha1)) {
				SFXManager.Play(test2DSound);
			}

			if (Input.GetKeyDown(KeyCode.Alpha2)) {
				Vector3 position = worldSoundPoint != null ? worldSoundPoint.position : transform.position;
				SFXManager.PlayAtPosition(test3DSound, position);
			}

			if (Input.GetKeyDown(KeyCode.Alpha3)) {
				attachedSource = SFXManager.PlayAttached(testAttachedSound, transform);
			}

			if (Input.GetKeyDown(KeyCode.Alpha4)) {
				SFXManager.Stop(attachedSource);
				attachedSource = null;
			}

			if (Input.GetKeyDown(KeyCode.Alpha5)) {
				SFXManager.SetPaused(true);
			}

			if (Input.GetKeyDown(KeyCode.Alpha6)) {
				SFXManager.SetPaused(false);
			}

			if (Input.GetKeyDown(KeyCode.Alpha7)) {
				SFXManager.StopAll();
			}
		}
	}
}