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
    public float rotateSpeed = 10.0f;
    public float transitionSpeed = 10.0f;
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

    private RootState currentRootState;
    private MoveState currentMoveState;
    private JumpState currentJumpState;
    private LandingState currentLandingState;

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

        rootMixer.SetInputWeight((int)RootState.Move, 1f);
        moveMixer.SetInputWeight((int)MoveState.Idle, 1f);

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

        UpdateMixerWeights();
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(horizontal, 0, vertical).normalized;

        bool isMoving = inputDir.sqrMagnitude > 0.01f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && isMoving;
        bool isGrounded = controller.isGrounded;

        if (isMoving)
        {
            Quaternion targetRotation = Quaternion.LookRotation(inputDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
        }

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        float speed = isRunning ? runSpeed : walkSpeed;
        Vector3 velocity = inputDir * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

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

        if (!isMoving)
        {
            SetMoveState(MoveState.Idle);
        }
        else if (isRunning)
        {
            SetMoveState(MoveState.RunN);
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

        SetRootState(RootState.Jump);
        SetJumpState(JumpState.JumpStart);
    }

    private void UpdateJumpState(bool isGrounded)
    {
        switch (currentJumpState)
        {
            case JumpState.JumpStart:
                if (IsClipFinished(jumpStartPlayable) || !isGrounded)
                {
                    SetJumpState(JumpState.InAir);
                }

                break;

            case JumpState.InAir:
                if (isGrounded && verticalVelocity <= 0f)
                {
                    SetJumpState(JumpState.JumpLand);
                }

                break;

            case JumpState.JumpLand:
                if (IsClipFinished(jumpLandPlayable) || jumpLandPlayable.GetTime() >= 1f)
                {
                    isJumping = false;
                    isLanding = false;
                    SetRootState(RootState.Move);
                }

                break;
        }
    }

    private void StartLanding()
    {
        isLanding = true;
        SetRootState(RootState.Landing);
    }

    private void UpdateLanding()
    {
        isLanding = false;
        SetRootState(RootState.Move);
    }

    private void SetRootState(RootState state)
    {
        currentRootState = state;
    }

    private void SetMoveState(MoveState state)
    {
        currentMoveState = state;
    }

    private void SetJumpState(JumpState state)
    {
        currentJumpState = state;
        ResetClipTime(state);
    }

    private void SetLandingState(LandingState state)
    {
        currentLandingState = state;
        ResetLandingClipTime(state);
    }

    private void UpdateMixerWeights()
    {
        float blendStep = Time.deltaTime * transitionSpeed;

        for (int i = 0; i < rootMixer.GetInputCount(); i++)
        {
            float targetWeight = (i == (int)currentRootState) ? 1f : 0f;
            float currentWeight = rootMixer.GetInputWeight(i);
            rootMixer.SetInputWeight(i, Mathf.MoveTowards(currentWeight, targetWeight, blendStep));
        }

        for (int i = 0; i < moveMixer.GetInputCount(); i++)
        {
            float targetWeight = (i == (int)currentMoveState) ? 1f : 0f;
            float currentWeight = moveMixer.GetInputWeight(i);
            moveMixer.SetInputWeight(i, Mathf.MoveTowards(currentWeight, targetWeight, blendStep));
        }

        for (int i = 0; i < jumpMixer.GetInputCount(); i++)
        {
            float targetWeight = (i == (int)currentJumpState) ? 1f : 0f;
            float currentWeight = jumpMixer.GetInputWeight(i);
            jumpMixer.SetInputWeight(i, Mathf.MoveTowards(currentWeight, targetWeight, blendStep));
        }

        for (int i = 0; i < landingMixer.GetInputCount(); i++)
        {
            float targetWeight = (i == (int)currentLandingState) ? 1f : 0f;
            float currentWeight = landingMixer.GetInputWeight(i);
            landingMixer.SetInputWeight(i, Mathf.MoveTowards(currentWeight, targetWeight, blendStep));
        }
    }

    private void ResetClipTime(JumpState state)
    {
        AnimationClipPlayable playable = default;

        switch (state)
        {
            case JumpState.JumpStart: playable = jumpStartPlayable; break;
            case JumpState.InAir: playable = inAirPlayable; break;
            case JumpState.JumpLand: playable = jumpLandPlayable; break;
        }

        if (playable.IsValid())
        {
            playable.SetTime(0);
            playable.SetDone(false);
            playable.Play();
        }
    }

    private void ResetLandingClipTime(LandingState state)
    {
        AnimationClipPlayable playable = default;

        switch (state)
        {
            case LandingState.WalkNLand: playable = walkNLandPlayable; break;
            case LandingState.RunNLand: playable = runNLandPlayable; break;
        }

        if (playable.IsValid())
        {
            playable.SetTime(0);
            playable.SetDone(false);
            playable.Play();
        }
    }

    private bool IsClipFinished(AnimationClipPlayable playable)
    {
        if (!playable.IsValid()) return true;

        double duration = playable.GetDuration();

        if (duration <= 0)
        {
            return true;
        }

        return playable.GetTime() >= duration - 0.01f;
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }
}