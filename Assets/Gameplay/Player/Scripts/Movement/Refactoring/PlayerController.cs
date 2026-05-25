using Game.AI;
using UnityEngine;

[RequireComponent(typeof(PlayerMotor))]
public class PlayerController : MonoBehaviour
{
    [Header("Shared Config")]
    [SerializeField] float grindSpeed;
    [SerializeField] float heightOffset;
    public float railBoost;
    public float railGrindTime = 1;

    [Header("Input")]
    [SerializeField]
    private InputManagerBase _inputManager;

    public PlayerMotor Motor { get; private set; }
    public IInputManager InputManager => _inputManager.InputManager;
    public PlayerNoiseEmitter NoiseEmitter { get; private set; }

    private MovementState currentState;

    //States
    public WalkState walkState { get; private set; }
    public SprintState SprintState { get; private set; }

    //Flags
    public bool JumpPressed { get; private set; }
    private void OnEnable()
    {
        InputManager.OnJumpReceived += HandleJump;
    }

    private void OnDisable()
    {
        InputManager.OnJumpReceived -= HandleJump;
    }

    private void Awake()
    {
        Motor = GetComponent<PlayerMotor>();

        //instantiate states
        walkState = new WalkState(this);
        SprintState = new SprintState(this);
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SwitchState(walkState);
    }

    private void Update()
    {
        Motor.GroundCheck();
        currentState?.UpdateState();
    }

    public void SwitchToDefualtState() => SwitchState(walkState);

    private void FixedUpdate()
    {
        currentState?.FixedUpdateState();
    }

    private void LateUpdate()
    {
        if (PauseManager.IsPaused) return;
        Motor.HandleLook(InputManager.Look);
    }

    public void SwitchState(MovementState newState)
    {
        currentState?.ExitState();
        currentState = newState;
        currentState?.EnterState();
    }


    private void HandleJump(bool value)
    {
        if (value) JumpPressed = true;
    }
    
    public void UseJumpInput()
    {
        JumpPressed = false;
    }
}
