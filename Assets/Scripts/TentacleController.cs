using System;
using UnityEngine;

public class TentacleController : MonoBehaviour
{
    public enum TentacleState
    {
        Idle,
        Detect,
        AttackWindup,
        AttackReach,
        Grab,
        Throw,
        Retract
    }

    [Header("References")]
    public Animator animator;
    public Transform holdPoint;
    public Transform attackPoint;
    public Transform[] tentacleNodes; // 0 is tip, 5 is base

    [Header("Settings")]
    public float detectRadius = 7f;
    public float attackRadius = 4f;
    public float attackDelay = 1f;
    public float holdTime = 3f;
    public float throwForce = 500f;
    public float rotationSpeed = 5f;
    
    [Header("IK Settings")]
    public Transform[] ikNodes; // Nodes to offset, from base-most to tip-most

    [Header("Hold Offsets")]
    public Vector3 holdPositionOffset = Vector3.zero;
    public Vector3 holdRotationOffset = Vector3.zero;

    private TentacleState currentState = TentacleState.Idle;
    private TentacleManager manager;
    private PlayerController player;
    private Transform playerTransform;
    
    private float lifetime;
    private float stateTimer;
    private Vector3 currentIKOffset = Vector3.zero;

    public void Initialize(TentacleManager mgr, float timeToLive)
    {
        manager = mgr;
        player = GameManager.Instance.Player;
        playerTransform = player.transform;
        lifetime = timeToLive;
        
        currentState = TentacleState.Idle;
        animator.SetBool("PlayerDetect", false);
        currentIKOffset = Vector3.zero;

        manager.OnTick += Tick;
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.OnTick -= Tick;
        }
    }

    private void Tick(float dt)
    {
        lifetime -= dt;

        if (lifetime <= 0 && currentState == TentacleState.Idle)
        {
            ChangeState(TentacleState.Retract);
            return;
        }

        if (currentState == TentacleState.Retract) return;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        bool isPlayerTargetable = !player.isRagdoll && !player.isGrabbed;

        switch (currentState)
        {
            case TentacleState.Idle:
                if (distToPlayer <= detectRadius)
                {
                    ChangeState(TentacleState.Detect);
                }
                break;

            case TentacleState.Detect:
                if (distToPlayer > detectRadius)
                {
                    ChangeState(TentacleState.Idle);
                }
                else if (distToPlayer <= attackRadius && isPlayerTargetable)
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
                
                if (distToPlayer > attackRadius || !isPlayerTargetable)
                {
                    ChangeState(TentacleState.Detect); // Cancel attack
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
                // Waiting for OnGrabbed animation event
                break;

            case TentacleState.Grab:
                stateTimer -= dt;
                if (stateTimer <= 0)
                {
                    animator.SetTrigger("Throw");
                    ChangeState(TentacleState.Throw);
                }
                break;

            case TentacleState.Throw:
                // Waiting for OnReleased animation event
                break;
        }
    }

    private void LateUpdate()
    {
        if (currentState == TentacleState.AttackReach || currentState == TentacleState.Grab)
        {
            // Calculate target offset to reach the player exactly
            Vector3 targetPos = playerTransform.position;
            Transform tip = attackPoint != null ? attackPoint : ikNodes[ikNodes.Length - 1];
            Vector3 targetOffset = targetPos - tip.position;
            
            // Smoothly lerp current IK offset to target offset
            currentIKOffset = Vector3.Lerp(currentIKOffset, targetOffset, Time.deltaTime * 10f);
        }
        else
        {
            // Smoothly return to normal animation
            currentIKOffset = Vector3.Lerp(currentIKOffset, Vector3.zero, Time.deltaTime * 5f);
        }

        // Apply uniform offset to IK nodes
        if (currentIKOffset.sqrMagnitude > 0.001f && ikNodes.Length > 0)
        {
            for (int i = 0; i < ikNodes.Length; i++)
            {
                // Fraction goes from >0 to 1.0 (for the last node/tip)
                float fraction = (float)(i + 1) / ikNodes.Length;
                
                // ПРОБЛЕМА ЗДЕСЬ: Animator перезаписывает это каждую секунду, 
                // и Dreamteck Splines может не успевать считывать этот сдвиг из-за Script Execution Order
                ikNodes[i].position += currentIKOffset * fraction;
            }
        }
    }

    private void TrackPlayer(float dt)
    {
        Vector3 dir = playerTransform.position - transform.position;
        dir.y = 0; // Only rotate on Y axis
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, rotationSpeed * dt);
        }
    }

    private void ChangeState(TentacleState newState)
    {
        currentState = newState;
        
        switch (currentState)
        {
            case TentacleState.Idle:
                animator.SetBool("PlayerDetect", false);
                break;
                
            case TentacleState.Detect:
                animator.SetBool("PlayerDetect", true);
                break;
                
            case TentacleState.AttackWindup:
                stateTimer = attackDelay;
                break;
                
            case TentacleState.Grab:
                stateTimer = holdTime;
                break;

            case TentacleState.Retract:
                animator.SetTrigger("Despawn");
                break;
        }
    }

    // --- Animation Events ---

    public void OnPlayerGrabbed()
    {
        if (currentState != TentacleState.AttackReach) return;
        
        if (!player.isRagdoll)
        {
            ChangeState(TentacleState.Grab);
            
            player.SetGrabbed(true);
            playerTransform.SetParent(holdPoint);
            playerTransform.localPosition = holdPositionOffset;
            playerTransform.localEulerAngles = holdRotationOffset;
        }
        else
        {
            // Player already grabbed or escaped, cancel
            ChangeState(TentacleState.Idle);
        }
    }

    public void OnPlayerReleased()
    {
        if (currentState != TentacleState.Throw) return;
        
        if (playerTransform.parent == holdPoint)
        {
            playerTransform.SetParent(null);
            player.SetGrabbed(false);
            
            Rigidbody rb = player.ramecanMixer.RootBoneRb;
            // Calculate throw direction from tentacle base to the player (with a slight upward angle)
            Vector3 dirFromBase = playerTransform.position - transform.position;
            dirFromBase.y = 0; // Ignore height difference for the base direction
            Vector3 throwDir = (dirFromBase.normalized + Vector3.up * 0.5f).normalized;
            rb.AddForce(throwDir * throwForce, ForceMode.Impulse);
        }
        
        ChangeState(TentacleState.Idle);
    }

    public void OnDespawn()
    {
        if (!gameObject.activeSelf) return;

        if (playerTransform != null && playerTransform.parent == holdPoint)
        {
            playerTransform.SetParent(null);
            player.SetGrabbed(false);
        }

        if (manager != null)
        {
            manager.Despawn(this);
        }
    }
}
