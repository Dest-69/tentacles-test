using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Dreamteck.Splines;

public class TentacleController : MonoBehaviour
{
    public enum TentacleState
    {
        Spawning,
        Idle,
        Detect,
        AttackWindup,
        AttackReach,
        Pull,
        Throw,
        Retract
    }

    #region Settings & References

    [Header("References")]
    public Animator animator;
    public Transform holdPoint;
    public TentacleSplineDriver driver;
    public Rig rig;
    public Rig helperRig;
    public BlendConstraint holdPointConstraint;
    public EmotionController emotionController;

    [Header("Behavior Settings")]
    public float detectRadius = 7f;
    public float attackRadius = 4f;
    public float attackDelay = 1f;
    public float holdTime = 3f;
    public float throwForce = 500f;
    public float rotationSpeed = 5f;
    public float throwUpwardBias = 0.5f;
    [Tooltip("Local direction relative to the tentacle base to throw the player.")]
    public Vector3 throwDirectionLocal = Vector3.forward;
    [Tooltip("If set, overrides throwDirectionLocal and throws the player towards this exact world transform.")]
    public Transform throwTargetWorld;
    public float throwUnwrapDuration = 0.5f;

    [Header("Animation Control")]
    [Tooltip("The total time allocated for the attack (strike + wrap)")]
    public float attackDuration = 1.0f; 
    public AnimationCurve rigWeightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve coverageCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Header("Pull Settings")]
    [Tooltip("X-axis: 0 is Base, 1 is Tip. Y-axis: 0 is Animation, 1 is IK (Spline).")]
    public AnimationCurve pullBoneWeightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public float pullIKBlendTime = 1.0f; // Time to lerp constraint weights
    [Tooltip("The curve to animate the Hold Point Constraint weight during the Pull phase (e.g. from 1 to 0).")]
    public AnimationCurve pullHoldPointCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Procedural Spline Settings")]
    public float reachArcHeight = 2.0f;
    public float spiralRadius = 1.0f;
    public float spiralHeight = 4.0f;
    public float spiralTurns = 3.0f;
    public int spiralPointsCount = 20;

    [Header("Hold Settings")]
    public Vector3 holdPositionOffset = Vector3.zero;
    public Vector3 holdRotationOffset = Vector3.zero;
    public float holdLerpSpeed = 15f;

    #endregion

    #region Private State

    private TentacleState currentState = TentacleState.Idle;
    private TentacleManager manager;
    private PlayerController player;
    private Transform playerTransform;
    private Vector3 TargetPos => playerTransform != null ? playerTransform.TransformPoint(-holdPositionOffset) : Vector3.zero;
    
    private float lifetime;
    private float stateTimer;
    private float attackTimer;

    private SplinePoint[] proceduralPoints;
    private IRigConstraint[] rigConstraints;

    #endregion

    #region Initialization & Lifecycle

    private void Awake()
    {
        rigConstraints = rig.GetComponentsInChildren<IRigConstraint>();
    }

    public void Initialize(TentacleManager mgr, float timeToLive)
    {
        manager = mgr;
        player = GameManager.Instance.Player;
        playerTransform = player.transform;
        lifetime = timeToLive;
        
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Pull");
        animator.ResetTrigger("Throw");
        animator.ResetTrigger("Despawn");
        
        ChangeState(TentacleState.Spawning);

        if (manager != null)
        {
            manager.OnTick -= Tick;
            manager.OnTick += Tick;
        }
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.OnTick -= Tick;
        }
    }

    private void LateUpdate()
    {
        if (currentState == TentacleState.AttackReach || currentState == TentacleState.Pull || currentState == TentacleState.Throw)
        {
            GenerateAttackSpline();
            driver.EvaluateSpline();
        }
    }

    public void OnDespawn()
    {
        if (!gameObject.activeSelf) return;

        if (playerTransform != null)
        {
            if (playerTransform.parent == holdPoint)
            {
                playerTransform.SetParent(null);
                player.SetGrabbed(false);
            }
        }

        if (manager != null)
        {
            manager.Despawn(this);
        }
    }

    #endregion

    #region Spline Generation

    private float lockedHeightScale = 1f;

    private void GenerateAttackSpline()
    {
        if (playerTransform == null) return;

        int totalPoints = 2 + spiralPointsCount; 
        
        if (proceduralPoints == null || proceduralPoints.Length != totalPoints)
        {
            proceduralPoints = new SplinePoint[totalPoints];
        }

        Vector3 rootPos = transform.position;
        Vector3 targetPos = TargetPos;
        
        // Scale heights dynamically based on distance
        float distToTarget = Vector3.Distance(rootPos, targetPos);
        
        // Only update the height scale before we actually grab the player.
        // Once grabbed (Pull/Throw), we lock the scale so the spiral doesn't shrink/expand.
        if (currentState != TentacleState.Pull && currentState != TentacleState.Throw)
        {
            lockedHeightScale = Mathf.Clamp(distToTarget / attackRadius, 0.35f, 1f);
        }

        float currentSpiralHeight = spiralHeight * lockedHeightScale;
        float currentArcHeight = reachArcHeight * lockedHeightScale;
        
        Quaternion spiralRotation = transform.rotation;
        
        // Calculate the root's angle relative to the target
        Vector3 localRootPos = Quaternion.Inverse(spiralRotation) * (rootPos - targetPos);
        float rootAngle = Mathf.Atan2(localRootPos.z, localRootPos.x);
        
        // Since the spiral winds clockwise (decreasing angle), the perfect tangent entry 
        // is exactly 90 degrees (-PI/2) from the root's angle. This completely prevents kinks!
        float angleOffset = rootAngle - Mathf.PI / 2f; 

        // Calculate the dynamically aligned entry point (Start at the BOTTOM)
        Vector3 localSpiralStart = new Vector3(Mathf.Cos(angleOffset) * spiralRadius, -currentSpiralHeight / 2f, Mathf.Sin(angleOffset) * spiralRadius);
        Vector3 safeEntry = targetPos + (spiralRotation * localSpiralStart);

        // Midpoint for the rising arc (no horizontal bowing needed since it's a perfect tangent)
        Vector3 midPos = (rootPos + safeEntry) * 0.5f;
        midPos.y += currentArcHeight; // Add height for the arc

        proceduralPoints[0] = new SplinePoint(rootPos);
        proceduralPoints[0].normal = -transform.forward; 
        proceduralPoints[0].size = 1f;
        proceduralPoints[0].color = Color.white;

        // Calculate a perfectly outward normal for midPos
        Vector3 midNormal = (midPos - targetPos).normalized;
        midNormal.y = 0;
        if (midNormal.sqrMagnitude < 0.001f) midNormal = -transform.forward;

        proceduralPoints[1] = new SplinePoint(midPos);
        proceduralPoints[1].normal = midNormal.normalized; 
        proceduralPoints[1].size = 1f;
        proceduralPoints[1].color = Color.white;

        // --- 2. Spiral Points ---
        for (int i = 0; i < spiralPointsCount; i++)
        {
            float t = (float)i / (spiralPointsCount - 1);
            float angle = angleOffset - t * spiralTurns * Mathf.PI * 2f;

            float x = Mathf.Cos(angle) * spiralRadius;
            float z = Mathf.Sin(angle) * spiralRadius;
            // Climb upwards from bottom to top
            float y = -(currentSpiralHeight / 2f) + (t * currentSpiralHeight);

            Vector3 localSpiralPos = new Vector3(x, y, z);
            Vector3 worldSpiralPos = targetPos + (spiralRotation * localSpiralPos);
            
            Vector3 outwardNormal = (worldSpiralPos - targetPos).normalized;
            outwardNormal.y = 0;
            if (outwardNormal.sqrMagnitude < 0.001f) outwardNormal = transform.forward;

            proceduralPoints[i + 2] = new SplinePoint(worldSpiralPos);
            proceduralPoints[i + 2].normal = outwardNormal;
            proceduralPoints[i + 2].size = 1f;
            proceduralPoints[i + 2].color = Color.white;
        }

        driver.spline.SetPoints(proceduralPoints, SplineComputer.Space.World);
    }

    #endregion

    #region State Logic

    public void Tick(float dt)
    {
        lifetime -= dt;

        if (lifetime <= 0 && currentState == TentacleState.Idle)
        {
            ChangeState(TentacleState.Retract);
            return;
        }

        if (currentState == TentacleState.Retract) return;

        float distToPlayerSqr = (transform.position - TargetPos).sqrMagnitude;
        float detectRadiusSqr = detectRadius * detectRadius;
        float attackRadiusSqr = attackRadius * attackRadius;
        bool isPlayerTargetable = !player.isRagdoll && !player.isGrabbed;

        switch (currentState)
        {
            case TentacleState.Spawning:
                // Wait for OnSpawned
                break;

            case TentacleState.Idle:
                if (distToPlayerSqr <= detectRadiusSqr)
                {
                    ChangeState(TentacleState.Detect);
                }
                break;

            case TentacleState.Detect:
                if (distToPlayerSqr > detectRadiusSqr)
                {
                    ChangeState(TentacleState.Idle);
                }
                else if (distToPlayerSqr <= attackRadiusSqr && isPlayerTargetable)
                {
                    ChangeState(TentacleState.AttackWindup);
                }
                else
                {
                    TrackPlayer(dt);
                }
                break;

            case TentacleState.AttackWindup:
                TrackPlayer(dt);
                
                if (distToPlayerSqr > attackRadiusSqr || !isPlayerTargetable)
                {
                    ChangeState(TentacleState.Detect);
                }
                else
                {
                    stateTimer -= dt;
                    if (stateTimer <= 0)
                    {
                        animator.SetTrigger("Attack");
                        ChangeState(TentacleState.AttackReach);
                    }
                }
                break;

            case TentacleState.AttackReach:
                TrackPlayer(dt);
                
                attackTimer += dt;
                float t = Mathf.Clamp01(attackTimer / attackDuration);
                
                rig.weight = rigWeightCurve.Evaluate(t);
                driver.coverage = coverageCurve.Evaluate(t);
                
                if (attackTimer >= attackDuration)
                {
                    // The wrap is complete
                    if (isPlayerTargetable)
                    {
                        ChangeState(TentacleState.Pull);
                    }
                    else
                    {
                        ChangeState(TentacleState.Idle);
                    }
                }
                break;

            case TentacleState.Pull:
                if (playerTransform.parent != null)
                {
                    playerTransform.localPosition = Vector3.Lerp(playerTransform.localPosition, holdPositionOffset, dt * holdLerpSpeed);
                    playerTransform.localRotation = Quaternion.Slerp(playerTransform.localRotation, Quaternion.Euler(holdRotationOffset), dt * holdLerpSpeed);
                }

                // Smoothly blend individual constraint weights based on the curve
                float blendT = Mathf.Clamp01((holdTime - stateTimer) / pullIKBlendTime);
                for (int i = 0; i < rigConstraints.Length; i++)
                {
                    float percent = (float)i / (rigConstraints.Length - 1);
                    float targetWeight = pullBoneWeightCurve.Evaluate(percent);
                    rigConstraints[i].weight = Mathf.Lerp(1f, targetWeight, blendT);
                }

                holdPointConstraint.weight = pullHoldPointCurve.Evaluate(blendT);

                stateTimer -= dt;
                if (stateTimer <= 0)
                {
                    animator.SetTrigger("Throw");
                    ChangeState(TentacleState.Throw);
                }
                break;

            case TentacleState.Throw:
                stateTimer += dt;
                float throwBlend = Mathf.Clamp01(stateTimer / throwUnwrapDuration);
                rig.weight = Mathf.Lerp(1f, 0f, throwBlend);
                break;
        }
    }

    private void ChangeState(TentacleState newState)
    {
        if (currentState == newState) return;
        
        currentState = newState;
        
        float targetHelperWeight = (currentState == TentacleState.Pull) ? pullHoldPointCurve.Evaluate(0f) : 1f;
        holdPointConstraint.weight = targetHelperWeight;
        
        switch (currentState)
        {
            case TentacleState.Spawning:
                animator.SetBool("PlayerDetect", false);
                rig.weight = 0f;
                driver.coverage = 0f;
                break;

            case TentacleState.Idle:
                animator.SetBool("PlayerDetect", false);
                rig.weight = 0f;
                driver.coverage = 0f;
                break;
                
            case TentacleState.Detect:
                animator.SetBool("PlayerDetect", true);
                rig.weight = 0f;
                driver.coverage = 0f;
                emotionController.ShowQuestion();
                break;
                
            case TentacleState.AttackWindup:
                stateTimer = attackDelay;
                rig.weight = 0f;
                driver.coverage = 0f;
                emotionController.ShowExclamation();
                break;

            case TentacleState.AttackReach:
                attackTimer = 0f;
                break;
                
            case TentacleState.Pull:
                stateTimer = holdTime;
                animator.SetTrigger("Pull");
                
                if (playerTransform != null)
                {
                    // Snap holdPoint to the player's exact world position and rotation at the moment of grab
                    holdPoint.position = playerTransform.position;
                    holdPoint.rotation = playerTransform.rotation;
                }

                player.SetGrabbed(true);
                playerTransform.SetParent(holdPoint, true);
                break;

            case TentacleState.Throw:
                stateTimer = 0f;
                break;

            case TentacleState.Retract:
                animator.SetTrigger("Despawn");
                rig.weight = 0f;
                break;
        }
    }

    private void TrackPlayer(float dt)
    {
        Vector3 dir = TargetPos - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, rotationSpeed * dt);
        }
    }

    #endregion

    #region Animation Events

    public void OnSpawned()
    {
        if (currentState == TentacleState.Spawning)
        {
            ChangeState(TentacleState.Idle);
        }
    }

    // This event is kept for compatibility if the user's animation relies on it,
    // but the state transition is now handled by the attackTimer reaching attackDuration.
    public void OnAttackReached()
    {
        // No longer strictly needed for transitioning to Grab, as Grab is merged into AttackReach.
    }

    public void OnPlayerReleased()
    {
        if (currentState != TentacleState.Throw && currentState != TentacleState.Pull) return;
        
        rig.weight = 0f; // Fully unwound

        if (playerTransform.parent != null && player.isGrabbed)
        {
            playerTransform.SetParent(null);
            player.SetGrabbed(false);
            
            Rigidbody rb = player.ramecanMixer.RootBoneRb;
            
            Vector3 worldThrowDir;
            if (throwTargetWorld != null)
            {
                worldThrowDir = throwTargetWorld.position - transform.position;
            }
            else
            {
                worldThrowDir = transform.TransformDirection(throwDirectionLocal.normalized);
            }
            
            worldThrowDir.y = 0;
            Vector3 throwDir = (worldThrowDir.normalized + Vector3.up * throwUpwardBias).normalized;
            rb.AddForce(throwDir * throwForce, ForceMode.Impulse);
        }
        
        ChangeState(TentacleState.Idle);
    }

    #endregion
}
