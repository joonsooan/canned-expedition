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

    private Animator animator;
    private CharacterController controller;
    private PlayableGraph graph;

    // Playable Mixers
    private AnimationMixerPlayable rootMixer;
    private AnimationMixerPlayable moveMixer;
    private AnimationMixerPlayable jumpMixer;
    private AnimationMixerPlayable landingMixer;

    // Playable Clips
    private AnimationClipPlayable idlePlayable;
    private AnimationClipPlayable walkNPlayable;
    private AnimationClipPlayable runNPlayable;
    private AnimationClipPlayable runSPlayable;

    private AnimationClipPlayable jumpStartPlayable;
    private AnimationClipPlayable inAirPlayable;
    private AnimationClipPlayable jumpLandPlayable;

    private AnimationClipPlayable walkNLandPlayable;
    private AnimationClipPlayable runNLandPlayable;

    // States
    private enum RootState { Move, Jump, Landing }
    private enum MoveState { Idle, WalkN, RunN, RunS }
    private enum JumpState { JumpStart, InAir, JumpLand }
    private enum LandingState { WalkNLand, RunNLand }

    private JumpState currentJumpState;
    private bool isJumping;
    private bool isLanding;
    private float verticalVelocity;

    void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        graph = PlayableGraph.Create("PlayerPlayableGraph");

        // Create Mixers
        rootMixer = AnimationMixerPlayable.Create(graph, 3);
        moveMixer = AnimationMixerPlayable.Create(graph, 4);
        jumpMixer = AnimationMixerPlayable.Create(graph, 3);
        landingMixer = AnimationMixerPlayable.Create(graph, 2);

        // Create Clips
        idlePlayable = AnimationClipPlayable.Create(graph, idleClip);
        walkNPlayable = AnimationClipPlayable.Create(graph, walkNClip);
        runNPlayable = AnimationClipPlayable.Create(graph, runNClip);
        runSPlayable = AnimationClipPlayable.Create(graph, runSClip);
        jumpStartPlayable = AnimationClipPlayable.Create(graph, jumpStartClip);
        inAirPlayable = AnimationClipPlayable.Create(graph, inAirClip);
        jumpLandPlayable = AnimationClipPlayable.Create(graph, jumpLandClip);
        walkNLandPlayable = AnimationClipPlayable.Create(graph, walkNLandClip);
        runNLandPlayable = AnimationClipPlayable.Create(graph, runNLandClip);

        // Connect Clips
        graph.Connect(idlePlayable, 0, moveMixer, (int)MoveState.Idle);
        graph.Connect(walkNPlayable, 0, moveMixer, (int)MoveState.WalkN);
        graph.Connect(runNPlayable, 0, moveMixer, (int)MoveState.RunN);
        graph.Connect(runSPlayable, 0, moveMixer, (int)MoveState.RunS);
        graph.Connect(jumpStartPlayable, 0, jumpMixer, (int)JumpState.JumpStart);
        graph.Connect(inAirPlayable, 0, jumpMixer, (int)JumpState.InAir);
        graph.Connect(jumpLandPlayable, 0, jumpMixer, (int)JumpState.JumpLand);
        graph.Connect(walkNLandPlayable, 0, landingMixer, (int)LandingState.WalkNLand);
        graph.Connect(runNLandPlayable, 0, landingMixer, (int)LandingState.RunNLand);

        // Connect Mixers to Root Mixer
        graph.Connect(moveMixer, 0, rootMixer, (int)RootState.Move);
        graph.Connect(jumpMixer, 0, rootMixer, (int)RootState.Jump);
        graph.Connect(landingMixer, 0, rootMixer, (int)RootState.Landing);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "AnimationOutput", animator);
        output.SetSourcePlayable(rootMixer);

        SetMoveState(MoveState.Idle);
        SetJumpState(JumpState.JumpStart);
        SetLandingState(LandingState.WalkNLand);
        SetRootState(RootState.Move);

        graph.Play();
    }

    void Update()
    {
        HandleMovement();
        HandleJump();

        if (isLanding)
        {
            UpdateLanding();
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        bool isWalking = Mathf.Abs(horizontal) > 0.1f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && isWalking;
        bool isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        float speed = isRunning ? runSpeed : walkSpeed;
        Vector3 move = new Vector3(horizontal * speed, verticalVelocity, 0);
        controller.Move(move * Time.deltaTime);

        // Don't update move state if jumping or landing
        if (isJumping || isLanding)
        {
            return;
        }

        // Update Move State
        if (!controller.isGrounded)
        {
            return;
        }

        if (!isWalking)
        {
            SetMoveState(MoveState.Idle);
        }
        else if (isRunning)
        {
            if (horizontal > 0f)
            {
                SetMoveState(MoveState.RunN);
            }
            else
            {
                SetMoveState(MoveState.RunS);
            }
        }
        else
        {
            SetMoveState(MoveState.WalkN);
        }
    }

    private void HandleJump()
    {
        bool isGrounded = controller.isGrounded;

        if (!isJumping && !isLanding && isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            StartJump();
            return;
        }

        if (!isJumping)
        {
            return;
        }

        UpdateJumpState(isGrounded);
    }

    private void StartJump()
    {
        isJumping = true;
        verticalVelocity = jumpForce;
        currentJumpState = JumpState.JumpStart;

        SetRootState(RootState.Jump);
        SetJumpState(JumpState.JumpStart);
    }

    private void UpdateJumpState(bool isGrounded)
    {
        switch (currentJumpState)
        {
            case JumpState.JumpStart:
                if (IsClipFinished(jumpStartPlayable))
                {
                    currentJumpState = JumpState.InAir;
                    SetJumpState(JumpState.InAir);
                }

                break;

            case JumpState.InAir:
                if (isGrounded)
                {
                    currentJumpState = JumpState.JumpLand;
                    SetJumpState(JumpState.JumpLand);
                }

                break;

            case JumpState.JumpLand:
                if (IsClipFinished(jumpLandPlayable))
                {
                    isJumping = false;
                    StartLanding();
                }

                break;
        }
    }

    private void StartLanding()
    {
        isLanding = true;
        SetRootState(RootState.Landing);
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f;

        if (isRunning)
        {
            SetLandingState(LandingState.RunNLand);
        }
        else
        {
            SetLandingState(LandingState.WalkNLand);
        }
    }

    private void UpdateLanding()
    {
        AnimationClipPlayable activePlayable;

        if (landingMixer.GetInputWeight((int)LandingState.RunNLand) > 0.5f)
        {
            activePlayable = runNLandPlayable;
        }
        else
        {
            activePlayable = walkNLandPlayable;
        }

        if (!IsClipFinished(activePlayable))
        {
            return;
        }

        isLanding = false;
        SetRootState(RootState.Move);
    }

    private void SetRootState(RootState state)
    {
        for (int i = 0; i < rootMixer.GetInputCount(); i++)
        {
            rootMixer.SetInputWeight(i, i == (int)state ? 1f : 0f);
        }
    }

    private void SetMoveState(MoveState state)
    {
        for (int i = 0; i < moveMixer.GetInputCount(); i++)
        {
            moveMixer.SetInputWeight(i, i == (int)state ? 1f : 0f);
        }
    }

    private void SetJumpState(JumpState state)
    {
        for (int i = 0; i < jumpMixer.GetInputCount(); i++)
        {
            jumpMixer.SetInputWeight(i, i == (int)state ? 1f : 0f);
        }

        ResetClipTime(state);
    }

    private void ResetClipTime(JumpState state)
    {
        switch (state)
        {
            case JumpState.JumpStart:
                jumpStartPlayable.SetTime(0);
                break;

            case JumpState.InAir:
                inAirPlayable.SetTime(0);
                break;

            case JumpState.JumpLand:
                jumpLandPlayable.SetTime(0);
                break;
        }
    }

    private void SetLandingState(LandingState state)
    {
        for (int i = 0; i < landingMixer.GetInputCount(); i++)
        {
            landingMixer.SetInputWeight(i, i == (int)state ? 1f : 0f);
        }

        ResetLandingClipTime(state);
    }

    private void ResetLandingClipTime(LandingState state)
    {
        switch (state)
        {
            case LandingState.WalkNLand:
                walkNLandPlayable.SetTime(0);
                break;

            case LandingState.RunNLand:
                runNLandPlayable.SetTime(0);
                break;
        }
    }

    private bool IsClipFinished(AnimationClipPlayable playable)
    {
        double duration = playable.GetDuration();

        if (duration <= 0)
        {
            return true;
        }

        return playable.GetTime() >= duration - 0.01f;
    }

    private void OnDestroy()
    {
        graph.Destroy();
    }
}