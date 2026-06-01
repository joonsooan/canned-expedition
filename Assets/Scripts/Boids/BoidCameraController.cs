using UnityEngine;
using Cinemachine;

public class BoidCameraController : MonoBehaviour
{
    [Header("Cinemachine References")]
    public CinemachineVirtualCamera boidVirtualCamera;

    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0f, 2f, -5f);
    public float smoothSpeed = 10f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 positionVelocity;
    private Boid targetBoid;
    private bool isFollowing = false;
    private CinemachineTransposer transposer;

    void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (boidVirtualCamera != null)
        {
            transposer = boidVirtualCamera.GetCinemachineComponent<CinemachineTransposer>();
            boidVirtualCamera.enabled = false;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            SelectRandomBoid();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ReturnToInitialPosition();
        }
    }

    void LateUpdate()
    {
        if (isFollowing)
        {
            if (targetBoid != null)
            {
                if (transposer != null)
                {
                    transposer.m_FollowOffset = offset;
                }
            }
            else
            {
                SelectRandomBoid();
                if (targetBoid == null)
                {
                    ReturnToInitialPosition();
                }
            }
        }
        else
        {
            float rotationDampFactor = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            float positionSmoothTime = 1f / smoothSpeed;

            transform.position = Vector3.SmoothDamp(transform.position, initialPosition, ref positionVelocity, positionSmoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, initialRotation, rotationDampFactor);
        }
    }

    private void SelectRandomBoid()
    {
        var allBoids = Boid.ActiveBoids;

        if (allBoids.Count == 1)
        {
            targetBoid = allBoids[0];
        }
        else
        {
            Boid nextBoid = targetBoid;
            int maxAttempts = 10;
            int attempts = 0;

            while (nextBoid == targetBoid && attempts < maxAttempts)
            {
                int randomIndex = Random.Range(0, allBoids.Count);
                nextBoid = allBoids[randomIndex];
                attempts++;
            }

            targetBoid = nextBoid;
        }

        boidVirtualCamera.Follow = targetBoid.transform;
        boidVirtualCamera.LookAt = targetBoid.transform;
        boidVirtualCamera.enabled = true;
        isFollowing = true;
    }

    private void ReturnToInitialPosition()
    {
        isFollowing = false;
        targetBoid = null;

        if (boidVirtualCamera != null)
        {
            boidVirtualCamera.Follow = null;
            boidVirtualCamera.LookAt = null;
            boidVirtualCamera.enabled = false;
        }
    }
}
