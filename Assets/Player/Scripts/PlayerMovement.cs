using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private bool canJump;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpStartingVelocity;
    [SerializeField] private float gravity;
    [SerializeField] private float fallSpeedMax;
    [SerializeField] private float jumpHeldTimerMax;
    [SerializeField] private float jumpPreloadTimerMax;
    [SerializeField] private float coyoteTimerMax;
    [SerializeField] private LayerMask jumpableMask;

    [Header("Air Jump")]
    [SerializeField] private bool canAirJump = true;
    [SerializeField] private int airJumpsMax = 1; //how many extra jumps you get each time you leave the ground
    [SerializeField] private float airJumpVelocity = 6f;

    [Header("Dash")]
    [SerializeField] private bool canDash = true;
    [SerializeField] private float dashSpeed = 25f; //how fast the dash starts
    [SerializeField] private float dashDeceleration = 60f; //how quickly the dash slows down (units per second, per second)
    [SerializeField] private float dashGravityResumeSpeed = 5f; //once the dash slows below this speed, gravity turns back on
    [SerializeField] private float dashCooldown = 0.4f; //seconds between dashes
    [SerializeField] private int airDashesMax = 1; //how many dashes you get each time you leave the ground

    [Header("Action Audio")]
    [SerializeField] private AudioClip dashAudioClip;
    [SerializeField] private AudioClip jumpAudioClip;
    [SerializeField] private AudioClip doubleJumpAudioClip;
    [SerializeField, Range(0f, 1f)] private float actionAudioVolume = 1f;

    private const float GroundedVelocity = -2f; //small downward velocity while grounded so the player doesn't hover
    private float jumpHeldTimer;
    private float jumpPreloadTimer;
    private float coyoteTimer;
    private float dashCooldownTimer;
    private int airJumpsRemaining;
    private int airDashesRemaining;
    private bool jumping;
    private bool dashing; //true during the fast, gravity-free part of a dash
    private bool isGrounded;
    private bool wasGroundedLastFrame;
    private Vector3 velocity;
    private Vector3 velocityInput;
    private Vector3 velocityPhysics;
    private Vector3 velocityDash;
    private CharacterController controller;
    private Controls controls;
    private PlayerCameraControl cameraControl;
    private AudioSource actionAudioSource;
    private Vector3 startPosition;
    private Quaternion startRotation;

    public Vector3 DashVelocity => velocityDash; //current dash velocity (zero when not dashing)
    public float DashStartSpeed => dashSpeed; //how fast a dash starts, useful for effects that scale with the dash



    void Start()
    {
        //get the character controller, controls, and camera components
        controller = GetComponent<CharacterController>();
        controls = GetComponent<Controls>();
        cameraControl = GetComponent<PlayerCameraControl>();
        actionAudioSource = GetComponent<AudioSource>();
        if (actionAudioSource == null)
        {
            actionAudioSource = gameObject.AddComponent<AudioSource>();
        }
        actionAudioSource.playOnAwake = false;
        actionAudioSource.spatialBlend = 0f;

        //remember where the player started so they can be sent back there
        startPosition = transform.position;
        startRotation = transform.rotation;

        //start with full air jumps and dashes
        RefillAirJumps();
        RefillAirDashes();
    }

    /// <summary>
    /// Give back all air jumps (called on landing, can also be used by pickups)
    /// </summary>
    public void RefillAirJumps() {
        airJumpsRemaining = airJumpsMax;
    }

    /// <summary>
    /// Give back all air dashes (called on landing, can also be used by pickups)
    /// </summary>
    public void RefillAirDashes() {
        airDashesRemaining = airDashesMax;
    }

    /// <summary>
    /// Teleport the player back to their starting position and clear any momentum
    /// </summary>
    public void ReturnToStart()
    {
        //the character controller overrides position changes while enabled, so turn it off while teleporting
        controller.enabled = false;
        transform.SetPositionAndRotation(startPosition, startRotation);
        controller.enabled = true;

        velocityPhysics = Vector3.zero;
        velocityDash = Vector3.zero;
        jumping = false;
        dashing = false;
        jumpHeldTimer = 0;
        jumpPreloadTimer = 0;
        coyoteTimer = 0;
        dashCooldownTimer = 0;
        RefillAirJumps();
        RefillAirDashes();
    }

    void Update()
    {
        //
        if (controls.JumpTriggered()) //if jump button is pressed
        {
            if (isGrounded) { //regular jump
                BeginJump(jumpStartingVelocity, jumpAudioClip);
            }
            else if (coyoteTimer > 0) { //coyote time jump
                BeginJump(jumpStartingVelocity, jumpAudioClip);
            }
            else if (canAirJump && airJumpsRemaining > 0) { //air jump
                BeginAirJump();
            }
            else { //if you are not grounded and didn't coyote jump, start the jump preload timer
                jumpPreloadTimer = jumpPreloadTimerMax;
            }
        }

        if (controls.DashTriggered()) { //if dash button is pressed
            TryDash();
        }

        //lower timers at the end of each frame
        jumpPreloadTimer -= Time.deltaTime;
        coyoteTimer -= Time.deltaTime;
        dashCooldownTimer -= Time.deltaTime;
    }
    
    void FixedUpdate()
    {
        // --isGrounded logic--
        isGrounded = RaycastTouchesGround();
        if (isGrounded && !wasGroundedLastFrame) {
            GroundEnter();
        }
        if (!isGrounded && wasGroundedLastFrame) {
            GroundExit();
        }
        wasGroundedLastFrame = isGrounded;
        if (isGrounded && velocityPhysics.y <= 0) {
            velocityPhysics.y = GroundedVelocity;
        }

        // --gravity logic-- only apply gravity if you are not jumping or if you are jumping but the jump button is not being held down
        if (dashing) {
            //no gravity during the fast part of a dash
        }
        else if (jumping && jumpHeldTimer < jumpHeldTimerMax) {
            if (controls.JumpHeld()) {
                jumpHeldTimer += Time.fixedDeltaTime;
            }
            else {
                jumpHeldTimer = jumpHeldTimerMax;
            }
        }
        else {
            ApplyGravity();
        }

        // --movement logic--
        Vector2 moveInput = Vector2.ClampMagnitude(controls.MoveInput(), 1f); //get move input vector and clamp to 1
        velocityInput = transform.right * moveInput.x + transform.forward * moveInput.y; //get input velocity
  
        velocityInput *= moveSpeed; //scale by move speed

        // --dash logic--
        UpdateDash();

        velocity = velocityInput + velocityPhysics + velocityDash; //combine input, physics, and dash velocity

        CollisionFlags collisions = controller.Move(velocity * Time.fixedDeltaTime); //move the player based on the combined velocity

        //if a dash hits the ceiling, stop moving upward so the player doesn't stick to it
        if ((collisions & CollisionFlags.Above) != 0 && velocityDash.y > 0) {
            velocityDash.y = 0;
        }
    }

    /// <summary>
    /// Slow the dash down each physics step and turn gravity back on once it's slow enough
    /// </summary>
    void UpdateDash() {
        velocityDash = Vector3.MoveTowards(velocityDash, Vector3.zero, dashDeceleration * Time.fixedDeltaTime); //decelerate toward zero

        if (dashing && velocityDash.magnitude <= dashGravityResumeSpeed) {
            dashing = false; //dash is slow enough, let gravity take over
        }

        //if we dash down into the ground, slide along it instead of pushing into it
        if (isGrounded && velocityDash.y < 0) {
            velocityDash.y = 0;
        }
    }

    /// <summary>
    /// Start a dash in the direction the camera is looking, if one is available
    /// </summary>
    void TryDash() {
        //if you can't dash, or the dash is on cooldown, don't do anything
        if (!canDash || dashCooldownTimer > 0) {
            return;
        }
        //in the air you need an air dash left
        if (!isGrounded && airDashesRemaining <= 0) {
            return;
        }

        //dash in the direction the camera is looking
        Vector3 dashDirection = cameraControl.CameraHolder.forward;
        if (isGrounded && dashDirection.y < 0) { //on the ground, don't dash down into the floor
            dashDirection.y = 0;
            dashDirection = dashDirection.sqrMagnitude > 0.001f ? dashDirection.normalized : transform.forward; //looking straight down falls back to body forward
        }

        velocityDash = dashDirection * dashSpeed;
        velocityPhysics.y = 0; //the dash replaces any rising or falling speed
        jumping = false; //end any held jump so it doesn't fight the dash
        jumpHeldTimer = jumpHeldTimerMax;
        dashing = true;
        dashCooldownTimer = dashCooldown;

        if (!isGrounded) {
            airDashesRemaining--;
        }
        PlayActionSound(dashAudioClip);
    }

    /// <summary>
    /// End the gravity-free part of a dash early
    /// </summary>
    /// <param name="keepHorizontal">Keep the dash's sideways momentum (true) or stop it completely (false)</param>
    void EndDash(bool keepHorizontal) {
        dashing = false;
        velocityDash.y = 0;
        if (!keepHorizontal) {
            velocityDash = Vector3.zero;
        }
    }

    void ApplyGravity(float gravityMultiplier = 1f)
    {
        velocityPhysics.y -= gravity * gravityMultiplier * Time.fixedDeltaTime; //apply gravity to the physics y velocity
        if (velocityPhysics.y < -fallSpeedMax) { //make sure fall speed never exceeds fallSpeedMax
            velocityPhysics.y = -fallSpeedMax;
        }
        if (isGrounded && velocityPhysics.y < 0) { //if grounded, keep a small downward velocity so the player stays snapped to the ground
            velocityPhysics.y = GroundedVelocity;
        }
    }

    void BeginJump(float startVelocity, AudioClip audioClip)
    {
        //if you can't jump, don't do anything
        if (!canJump) {
            return;
        }

        //jumping during a dash ends it but keeps the sideways momentum
        if (dashing) {
            EndDash(true);
        }

        //if jumping, set the physics y velocity to the jump starting velocity, reset timers, and set jumping to true
        velocityPhysics.y = startVelocity;
        jumpHeldTimer = 0;
        coyoteTimer = 0;
        jumpPreloadTimer = 0;
        jumping = true;
        PlayActionSound(audioClip);
    }
    
    /// <summary>
    /// Use up an air jump charge and jump
    /// </summary>
    void BeginAirJump()
    {
        if (!canJump) { //don't spend a charge if jumping is disabled
            return;
        }
        airJumpsRemaining--;
        BeginJump(airJumpVelocity, doubleJumpAudioClip);
    }

    void PlayActionSound(AudioClip audioClip)
    {
        if (audioClip != null)
        {
            actionAudioSource.PlayOneShot(audioClip, actionAudioVolume);
        }
    }

    void EndJump(){
        jumping = false;
    }
    
    /// <summary>
    /// Called when you first start touching the ground
    /// </summary>
    void GroundEnter() {
        //if you are jumping and you touch the ground, end the jump
        if (jumping) {
            EndJump();
        }
        //landing gives back all air jumps and air dashes
        RefillAirJumps();
        RefillAirDashes();
        //if I just landed on the platform right after pressing the jump button, let me jump
        if (jumpPreloadTimer > 0) {
            BeginJump(jumpStartingVelocity, jumpAudioClip);
        }
    }

    /// <summary>
    /// Called each frame you are touching the ground
    /// </summary>
    void GroundStay(RaycastHit hit)
    {
        //USE THIS IF YOU WANT SOMETHING TO HAPPEN EACH FRAME YOU ARE TOUCHING THE GROUND
    }

    /// <summary>
    /// Called when you first stop touching the ground
    /// </summary>
    void GroundExit() {
        if (!jumping) {
            coyoteTimer = coyoteTimerMax; //start the coyote timer if you leave the ground and are not jumping
        }
    }
    
    /// <summary>
    /// Cast raycasts from the middle of the player and from 8 corners to test if the player is on the ground
    /// </summary>
    bool RaycastTouchesGround() {
        float rayLength = controller.height * .5f + controller.skinWidth + .05f; //reach just past the bottom of the capsule

        //test if the middle of the player is touching the ground
        if (RaycastTest(transform.position, rayLength)) {
            return true;
        }

        //test if any of the 8 corners of the player is touching the ground
        float halfWidth = transform.localScale.x * .5f;
        float diagonalWidth = transform.localScale.x * .35f;
        
        //test the 4 side of the player
        if (RaycastTest(transform.position + (transform.right * halfWidth), rayLength)) {
            return true;
        }
        if (RaycastTest(transform.position + (-transform.right * halfWidth), rayLength)) {
            return true;
        }
        if (RaycastTest(transform.position + (transform.forward * halfWidth), rayLength)) {
            return true;
        }
        if (RaycastTest(transform.position + (-transform.forward * halfWidth), rayLength)) {
            return true;
        }
        //test the 4 corners of the player
        if (RaycastTest(transform.position + (transform.right * diagonalWidth) + (transform.forward * diagonalWidth), rayLength)) {
            return true;
        }
        if (RaycastTest(transform.position + (transform.right * diagonalWidth) + (-transform.forward * diagonalWidth), rayLength)) {
            return true;
        }
        if (RaycastTest(transform.position + (-transform.right * diagonalWidth) + (transform.forward * diagonalWidth), rayLength)) {
            return true;
        }
        if (RaycastTest(transform.position + (-transform.right * diagonalWidth) + (-transform.forward * diagonalWidth), rayLength)) {
            return true;
        }
        
        //call leave ground results if we were grounded and now we're not
        if (isGrounded) { 
            GroundExit();
        }

        //if we are not touching the ground, return false
        return false;
    }

    /// <summary>
    /// Test a single raycast to see if it hits the ground
    /// </summary>
    /// <param name="startingPoint"> Where the ray begins (it will cast down from here)</param>
    /// <param name="rayLength">How long the ray casts</param>
    bool RaycastTest(Vector3 startingPoint, float rayLength) {
        //cast a ray down from the starting point and check if it hits the ground
        RaycastHit hit;
        if (Physics.Raycast(startingPoint, transform.TransformDirection(Vector3.down), out hit, rayLength, jumpableMask, QueryTriggerInteraction.Ignore)) {
            //if it hits the ground, call the GroundStay function and return true
            GroundStay(hit);
            return true;
        }
        else {
            //if it doesn't hit the ground, return false
            return false;
        }
    }
}
