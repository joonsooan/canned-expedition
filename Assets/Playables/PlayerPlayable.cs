using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

public class PlayerPlayable : MonoBehaviour
{
    [Header("Animation Clips")]
    public AnimationClip idleClip;
    public AnimationClip walkNClip;
    public AnimationClip walkNLandClip;
    public AnimationClip runNClip;
    public AnimationClip runNLandClip;
    public AnimationClip runSClip;
    public AnimationClip jumpStartClip;
    public AnimationClip inAirClip;
    public AnimationClip jumpLandClip;

    [Header("Player Settings")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 5.0f;
    public float jumpForce = 5.0f;
    public float gravity = -9.81f;

    private const int numStates = 9;
    private float[] weights = new float[numStates];

    private Animator animator;
    private CharacterController controller;
    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private enum PlayerState { Idle, WalkN, WalkNLand, RunN, RunNLand, RunS, JumpStart, InAir, JumpLand }

    void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        graph = PlayableGraph.Create("PlayerPlayableGraph");
        mixer = AnimationMixerPlayable.Create(graph, numStates);

        // Create Clips
        var clipPlayableIdle = AnimationClipPlayable.Create(graph, idleClip);
        var clipPlayableWalkN = AnimationClipPlayable.Create(graph, walkNClip);
        var clipPlayableWalkNLand = AnimationClipPlayable.Create(graph, walkNLandClip);
        var clipPlayableRunN = AnimationClipPlayable.Create(graph, runNClip);
        var clipPlayableRunNLand = AnimationClipPlayable.Create(graph, runNLandClip);
        var clipPlayableRunS = AnimationClipPlayable.Create(graph, runSClip);
        var clipPlayableJumpStart = AnimationClipPlayable.Create(graph, jumpStartClip);
        var clipPlayableInAir = AnimationClipPlayable.Create(graph, inAirClip);
        var clipPlayableJumpLand = AnimationClipPlayable.Create(graph, jumpLandClip);

        // Connect Clips
        graph.Connect(clipPlayableIdle, 0, mixer, (int)PlayerState.Idle);
        graph.Connect(clipPlayableWalkN, 0, mixer, (int)PlayerState.WalkN);
        graph.Connect(clipPlayableWalkNLand, 0, mixer, (int)PlayerState.WalkNLand);
        graph.Connect(clipPlayableRunN, 0, mixer, (int)PlayerState.RunN);
        graph.Connect(clipPlayableRunNLand, 0, mixer, (int)PlayerState.RunNLand);
        graph.Connect(clipPlayableRunS, 0, mixer, (int)PlayerState.RunS);
        graph.Connect(clipPlayableJumpStart, 0, mixer, (int)PlayerState.JumpStart);
        graph.Connect(clipPlayableInAir, 0, mixer, (int)PlayerState.InAir);
        graph.Connect(clipPlayableJumpLand, 0, mixer, (int)PlayerState.JumpLand);

        var output = AnimationPlayableOutput.Create(graph, "AnimationOutput", animator);
        output.SetSourcePlayable(mixer);

        SetPlayerState(PlayerState.Idle);
        weights[0] = 1.0f;
        mixer.SetInputWeight(0, weights[0]);

        graph.Play();
    }

    void Update()
    {
        HandleInput();
        UpdateWeights();
    }

    private void SetPlayerState(PlayerState state)
    {
        int activeIndex = (int)state;

        for (int i = 0; i < numStates; i++)
        {
            weights[i] = (i == activeIndex) ? 1.0f : 0.0f;
        }
    }

    private void HandleInput()
    {
        bool isGrounded = controller.isGrounded;
        float horizontal = Input.GetAxisRaw("Horizontal");
        bool isWalking = Mathf.Abs(horizontal) > 0.1f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && isWalking;
        bool isJumping = Input.GetKeyDown(KeyCode.Space) && isGrounded;

        // Move Character
        float speed = isRunning ? runSpeed : walkSpeed;
        controller.Move(new Vector3(horizontal * speed * Time.deltaTime, 0, 0));

        // Do Jump
        if (isJumping && isGrounded)
        {
            controller.Move(new Vector3(0, jumpForce * Time.deltaTime, 0));
            SetPlayerState(PlayerState.JumpStart);
        }

        // Apply Gravity
        if (!isGrounded)
        {
            controller.Move(new Vector3(0, gravity * Time.deltaTime, 0));
        }

        if (isGrounded)
        {
            if (isWalking)
            {
                if (isRunning)
                {
                    SetPlayerState(PlayerState.RunN);
                }
                else
                {
                    SetPlayerState(PlayerState.WalkN);
                }
            }
            else
            {
                SetPlayerState(PlayerState.Idle);
            }
        }
        else if (controller.velocity.y < 0)
        {
            SetPlayerState(PlayerState.InAir);
        }
        else
        {
            SetPlayerState(PlayerState.InAir);
        }
    }

    private void UpdateWeights()
    {
        for (int i = 0; i < numStates; i++)
        {
            mixer.SetInputWeight(i, weights[i]);
        }
    }

    void OnDestroy()
    {
        graph.Destroy();
    }
}
