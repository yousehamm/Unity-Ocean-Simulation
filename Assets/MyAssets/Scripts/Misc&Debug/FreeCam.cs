using UnityEngine;

public class FreeCam : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float movementMultiplier = 10f;
    public float lookSpeed = 2f;
    private float rotationX, rotationY;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        //Do mouse rotation with right mouse button
        if (Input.GetMouseButton(1))
        {
            rotationX -= Input.GetAxis("Mouse Y") * lookSpeed;
            rotationY += Input.GetAxis("Mouse X") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -90f, 90f);
            transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);
        }

        //Do Keyboard movement for input
        Vector3 move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

        if (Input.GetKey(KeyCode.E))
        {
            move.y = 1f;
        }

        if (Input.GetKey(KeyCode.Q))
        {
            move.y = -1f;
        }

        //Apply speed multiplier
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * movementMultiplier : moveSpeed;

        transform.Translate(move * currentSpeed * Time.deltaTime, Space.Self);
    }
}