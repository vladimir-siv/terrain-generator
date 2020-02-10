using UnityEngine;

public class CreatorMovement : MonoBehaviour
{
	[SerializeField] private float Speed = 1.0f;
	[SerializeField] private float RotationalSpeed = 1.0f;

	private void FixedUpdate()
	{
		var direct = 0.0f;
		var strafe = 0.0f;
		var fly = 0.0f;

		if (Input.GetKey(KeyCode.W)) direct += 1.0f;
		if (Input.GetKey(KeyCode.S)) direct -= 1.0f;
		if (Input.GetKey(KeyCode.A)) strafe -= 1.0f;
		if (Input.GetKey(KeyCode.D)) strafe += 1.0f;
		if (Input.GetKey(KeyCode.Mouse3)) fly -= 1.0f;
		if (Input.GetKey(KeyCode.Mouse4)) fly += 1.0f;

		if (Input.GetMouseButton(2))
		{
			var rotSpeed = RotationalSpeed * Time.fixedDeltaTime;
			transform.Rotate(-Input.GetAxis("Mouse Y") * Vector3.right * rotSpeed);
			transform.Rotate(Input.GetAxis("Mouse X") * Vector3.up * rotSpeed, Space.World);
		}

		transform.position += (transform.forward * direct + transform.right * strafe + transform.up * fly).normalized * Speed * Time.fixedDeltaTime;
	}
}
