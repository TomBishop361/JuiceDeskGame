using UnityEngine;

// Handles alarm start SFX, looping siren audio, stop SFX, volume fading, and pitch urgency
[DisallowMultipleComponent]
public sealed class AlarmAudioController : MonoBehaviour {
	[Header("References")]
	[Tooltip("Central alarm controller used as the source for level/encounter alarm behaviour.")]
	[SerializeField] private LevelAlarmController alarmController;
	[Tooltip("AudioSource used for the looping siren.")]
	[SerializeField] private AudioSource loopSource;
	[Tooltip("AudioSource used for start/stop one-shot SFX. If empty, Loop Source is used instead.")]
	[SerializeField] private AudioSource oneShotSource;

	[Header("Clips")]
	[Tooltip("Optional one-shot played when the alarm starts.")]
	[SerializeField] private AudioClip startClip;
	[Tooltip("Looping siren clip played while the alarm is active.")]
	[SerializeField] private AudioClip loopClip;
	[Tooltip("Optional one-shot played when the alarm stops.")]
	[SerializeField] private AudioClip stopClip;

	[Header("Volume")]
	[Tooltip("Target volume for the looping siren at full fade weight.")]
	[SerializeField][Range(0.0f, 1.0f)] private float loopVolume = 0.85f;
	[Tooltip("Volume used when playing the start and stop one-shot clips.")]
	[SerializeField][Range(0.0f, 1.0f)] private float oneShotVolume = 1.0f;
	[Tooltip("Seconds used to fade the looping siren in.")]
	[SerializeField] private float fadeInDuration = 0.35f;
	[Tooltip("Seconds used to fade the looping siren out.")]
	[SerializeField] private float fadeOutDuration = 0.75f;

	[Header("Pitch Urgency")]
	[Tooltip("If true, siren pitch increases as alarm intensity rises.")]
	[SerializeField] private bool usePitchEscalation = true;
	[Tooltip("Loop pitch at low alarm intensity.")]
	[SerializeField] private float minPitch = 0.95f;
	[Tooltip("Loop pitch at maximum alarm intensity.")]
	[SerializeField] private float maxPitch = 1.15f;
	[Tooltip("If true, a small wobble is added to the siren pitch. Makes the alarm sound more sci-fi.")]
	[SerializeField] private bool usePitchWobble = true;
	[Tooltip("Pitch wobble strength.")]
	[SerializeField] private float pitchWobbleAmount = 0.025f;
	[Tooltip("Pitch wobble speed.")]
	[SerializeField] private float pitchWobbleSpeed = 4.0f;

	private bool alarmActive = false;
	private float fadeWeight = 0.0f;
	private float pitchTimer = 0.0f;

	private void Reset() {
		AutoWireReferences();
	}

	private void Awake() {
		AutoWireReferences();
		PrepareLoopSource();
	}

	private void OnEnable() {
		SubscribeToAlarm();
		SyncWithAlarmState();
	}

	private void OnDisable() {
		UnsubscribeFromAlarm();
	}

	private void Update() {
		// Handles smooth fade and pitch changes over time
		UpdateFade();
		UpdateLoopAudio();
	}

	private void AutoWireReferences() {
		if (alarmController == null) {
			alarmController = FindFirstObjectByType<LevelAlarmController>();
		}

		if (loopSource == null) {
			loopSource = GetComponent<AudioSource>();
		}

		if (oneShotSource == null) {
			oneShotSource = loopSource;
		}
	}

	private void PrepareLoopSource() {
		if (loopSource == null) {
			Debug.LogWarning($"{name}: AlarmAudioController has no Loop Source assigned.", this);
			return;
		}

		loopSource.loop = true;
		loopSource.playOnAwake = false;

		// Start silent
		// The volume is faded in when the alarm starts
		loopSource.volume = 0.0f;

		if (loopClip != null) {
			loopSource.clip = loopClip;
		}
	}

	private void SubscribeToAlarm() {
		if (alarmController == null) {
			return;
		}

		alarmController.AlarmStarted -= HandleAlarmStarted;
		alarmController.AlarmStopped -= HandleAlarmStopped;

		alarmController.AlarmStarted += HandleAlarmStarted;
		alarmController.AlarmStopped += HandleAlarmStopped;
	}

	private void UnsubscribeFromAlarm() {
		if (alarmController == null) {
			return;
		}

		alarmController.AlarmStarted -= HandleAlarmStarted;
		alarmController.AlarmStopped -= HandleAlarmStopped;
	}

	private void SyncWithAlarmState() {
		alarmActive = alarmController != null && alarmController.IsAlarmActive;

		// If the alarm is already active, start fully faded in
		// If it is inactive, start silent
		fadeWeight = alarmActive ? 1.0f : 0.0f;

		if (alarmActive) {
			EnsureLoopPlaying();
		}
	}

	private void HandleAlarmStarted() {
		// Avoid instantly forcing max volume
		// UpdateFade() will fade the loop in using fadeInDuration
		alarmActive = true;

		// Optional start beep/stinger
		PlayOneShot(startClip);

		// Start the looping siren immediately at low volume, then fade it in
		EnsureLoopPlaying();
	}

	private void HandleAlarmStopped() {
		// Avoid instantly stopping the loop
		// UpdateFade() will fade the loop out using fadeOutDuration
		alarmActive = false;

		// Optional shutdown beep/stinger
		PlayOneShot(stopClip);
	}

	private void UpdateFade() {
		// Fade weight smoothly moves between 0 and 1
		// 0 = silent, 1 = full loop volume
		float targetWeight = alarmActive ? 1.0f : 0.0f;
		float fadeDuration = alarmActive ? fadeInDuration : fadeOutDuration;
		float fadeRate = fadeDuration <= 0.0f ? float.PositiveInfinity : 1.0f / fadeDuration;

		fadeWeight = Mathf.MoveTowards(fadeWeight, targetWeight, fadeRate * Time.deltaTime);
	}

	private void UpdateLoopAudio() {
		if (loopSource == null) {
			return;
		}

		// If the alarm is active, make sure the siren is playing
		if (alarmActive) {
			EnsureLoopPlaying();
		}

		// Fade volume smoothly instead of snapping the siren on/off
		loopSource.volume = loopVolume * fadeWeight;

		// Pitch can change while playing to make the alarm feel more urgent
		loopSource.pitch = CalculatePitch();

		// Stop the looping source after fade-out so it does not keep running silently forever
		if (alarmActive == false && fadeWeight <= 0.001f && loopSource.isPlaying) {
			loopSource.Stop();
		}
	}

	private void EnsureLoopPlaying() {
		if (loopSource == null) {
			return;
		}

		if (loopClip != null && loopSource.clip != loopClip) {
			loopSource.clip = loopClip;
		}

		if (loopSource.clip == null) {
			Debug.LogWarning($"{name}: AlarmAudioController cannot play because no loop clip is assigned.", this);
			return;
		}

		if (loopSource.isPlaying == false) {
			loopSource.loop = true;
			loopSource.Play();
		}
	}

	private void PlayOneShot(AudioClip clip) {
		if (clip == null || oneShotSource == null) {
			return;
		}

		// PlayOneShot is used so the start/stop SFX can overlap the looping siren if needed
		oneShotSource.PlayOneShot(clip, oneShotVolume);
	}

	private float CalculatePitch() {
		// Alarm intensity comes from LevelAlarmController
		// Higher intensity means fewer enemies remain, so the siren becomes more urgent
		float intensity = alarmController != null ? alarmController.AlarmIntensity : 1.0f;

		// Escalation raises pitch from minPitch to maxPitch as intensity increases
		float pitch = usePitchEscalation ? Mathf.Lerp(minPitch, maxPitch, intensity) : minPitch;

		if (usePitchWobble) {
			// Adds subtle pitch movement so the siren feels less flat/static
			pitchTimer += Time.deltaTime * Mathf.Max(0.01f, pitchWobbleSpeed);
			pitch += Mathf.Sin(pitchTimer * Mathf.PI * 2.0f) * pitchWobbleAmount;
		}

		return Mathf.Max(0.01f, pitch);
	}

	private void OnValidate() {
		loopVolume = Mathf.Clamp01(loopVolume);
		oneShotVolume = Mathf.Clamp01(oneShotVolume);
		fadeInDuration = Mathf.Max(0.0f, fadeInDuration);
		fadeOutDuration = Mathf.Max(0.0f, fadeOutDuration);
		minPitch = Mathf.Max(0.01f, minPitch);
		maxPitch = Mathf.Max(0.01f, maxPitch);
		pitchWobbleAmount = Mathf.Max(0.0f, pitchWobbleAmount);
		pitchWobbleSpeed = Mathf.Max(0.01f, pitchWobbleSpeed);
	}
}