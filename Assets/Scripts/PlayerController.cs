using UnityEngine;
using RagdollMecanimMixer;

public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    public Animator animator;
    public Rigidbody rb;
    public RamecanMixer ramecanMixer;
    public Rigidbody[] staticBonesWhenGrabbed; // Bones that become kinematic when grabbed

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    
    [Header("Ragdoll Settings")]
    public float recoverVelocityThreshold = 0.5f;
    public float recoverTimeRequired = 1.0f;
    
    [Header("State")]
    public bool isRagdoll = false;
    public bool isGrabbed = false;
    
    private float calmTimer = 0f;
    private Vector3 inputDirection;
    private float inputVelocity;
    private Transform mainCamera;
    private bool isInitialized;

    public void Initialize()
    {
        mainCamera = GameManager.Instance.MainCamera.transform;
        
        LocalInput.Actions.Player.Enable();

        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return;

        if (isRagdoll)
        {
            CheckRagdollRecovery();
            return;
        }

        HandleInput();
        UpdateAnimator();
    }

    
    void FixedUpdate()
    {
        if (!isInitialized || isRagdoll || isGrabbed) return;
        
        HandleMovement();
    }

    private void HandleInput()
    {
        Vector2 moveInput = LocalInput.Move;

        float h = moveInput.x;
        float v = moveInput.y;
        
        inputVelocity = Mathf.Clamp01(new Vector2(h, v).magnitude);

        if (inputVelocity > 0.1f && mainCamera != null)
        {
            Vector3 camForward = mainCamera.forward;
            camForward.y = 0;
            camForward.Normalize();
            
            Vector3 camRight = mainCamera.right;
            camRight.y = 0;
            camRight.Normalize();
            
            inputDirection = (camForward * v + camRight * h).normalized;
        }
    }

    private void HandleMovement()
    {
        if (inputVelocity > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(inputDirection, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }
    }

    private void UpdateAnimator()
    {
        float angle = 0f;
        if (inputVelocity > 0.1f)
        {
            angle = Vector3.SignedAngle(rb.transform.forward, inputDirection, Vector3.up);
        }
        
        animator.SetFloat("direction", angle / 180f);
        animator.SetFloat("velocity", inputVelocity);
    }

    private void CheckRagdollRecovery()
    {
        // Don't recover if still being held by a tentacle
        if (isGrabbed) 
        {
            calmTimer = 0f;
            return;
        }

        if (ramecanMixer.RootBoneRb.linearVelocity.magnitude < recoverVelocityThreshold)
        {
            calmTimer += Time.deltaTime;
            if (calmTimer >= recoverTimeRequired)
            {
                SetRagdoll(false);
            }
        }
        else
        {
            calmTimer = 0f;
        }
    }

    public void SetGrabbed(bool state)
    {
        isGrabbed = state;
        if (isGrabbed)
        {
            SetRagdoll(true);
            
            // Make root bones (hip, stomach, chest) kinematic so they don't freak out
            if (ramecanMixer != null && ramecanMixer.RootBoneRb != null)
            {
                ramecanMixer.RootBoneRb.isKinematic = true;
            }

            if (staticBonesWhenGrabbed != null)
            {
                foreach (var bone in staticBonesWhenGrabbed)
                {
                    if (bone != null) bone.isKinematic = true;
                }
            }
        }
        else
        {
            // Restore physics to bones when released
            if (ramecanMixer != null && ramecanMixer.RootBoneRb != null)
            {
                ramecanMixer.RootBoneRb.isKinematic = false;
            }

            if (staticBonesWhenGrabbed != null)
            {
                foreach (var bone in staticBonesWhenGrabbed)
                {
                    if (bone != null) bone.isKinematic = false;
                }
            }
        }
    }

    public void SetRagdoll(bool state)
    {
        if (isRagdoll == state) return;
        isRagdoll = state;
        calmTimer = 0f;

        if (isRagdoll)
        {
            // Disable animator & script control, enable physics
            animator.enabled = false;
            rb.isKinematic = true;
            ramecanMixer.BeginStateTransition(1); // 1 = dead/ragdoll
        }
        else
        {
            // Unparent the ragdoll container so it doesn't teleport along with the root
            Transform ragdollContainer = ramecanMixer.ragdollContainer;
            Transform originalParent = ragdollContainer.parent;
            ragdollContainer.SetParent(null, true);

            // Move root to pelvis position to avoid snapping back to old position
            Vector3 pelvisPos = ramecanMixer.RootBoneTr.position;
            
            // Simple snap to ground (raycast could be used for better precision)
            if (Physics.Raycast(pelvisPos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f))
            {
                transform.position = hit.point;
            }
            else
            {
                transform.position = new Vector3(pelvisPos.x, transform.position.y, pelvisPos.z);
            }

            animator.enabled = true;
            rb.isKinematic = false;
            ramecanMixer.BeginStateTransition(0); // 0 = default

            // Reparent back after all states are updated (worldPositionStays = true keeps their actual world positions)
            ragdollContainer.SetParent(originalParent, true);
        }
    }
}
