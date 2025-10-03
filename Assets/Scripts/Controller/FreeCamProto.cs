using UnityEngine;
using UnityEngine.InputSystem;

/*
 * Just a quick and dirty free cam script for prototyping.
 */
public class FreeCamProto : MonoBehaviour
{
	[Header("Movement")]
	[SerializeField] private float _MoveSpeed = 5f;
	[SerializeField] private float _FastMultiplier = 2f;
	[SerializeField] private float _Acceleration = 20f;
	[SerializeField] private float _Damping = 10f;
	
	[Header("Look")]
	[SerializeField] private float _LookSensitivity = 1f;
	[SerializeField] private float _MaxLookX = 90f;
	[SerializeField] private float _MinLookX = -90f;
	
	private Vector2 _MoveInput;
	private Vector2 _LookInput;
	private bool _FastMove;
	
	private float _RotationX;
	private float _RotationY;
	private Vector3 _CurrentVelocity;

    // Update is called once per frame
    void Update()
    {
        HandleMovement();
        HandleLook();
    }

    private void HandleLook()
    {
	    _RotationX -= _LookInput.y * _LookSensitivity;
	    _RotationY += _LookInput.x * _LookSensitivity;
	    _RotationX = Mathf.Clamp(_RotationX, _MinLookX, _MaxLookX);
	    
	    transform.rotation = Quaternion.Euler(_RotationX, _RotationY, 0);
    }

    private void HandleMovement()
    {
	    Vector3 target = (transform.forward * _MoveInput.y + transform.right * _MoveInput.x);
	    if (target.sqrMagnitude > 1f) target.Normalize();

	    float speed = _MoveSpeed * (_FastMove ? _FastMultiplier : 1f);
	    target *= speed;

	    _CurrentVelocity = Vector3.Lerp(_CurrentVelocity, target, _Acceleration * Time.deltaTime);

	    if (_MoveInput == Vector2.zero)
		    _CurrentVelocity = Vector3.Lerp(_CurrentVelocity, Vector3.zero, _Damping * Time.deltaTime);

	    transform.position += _CurrentVelocity * Time.deltaTime;
    }
    
    public void OnMove(InputAction.CallbackContext ctx) =>  _MoveInput = ctx.ReadValue<Vector2>();

    public void OnLook(InputAction.CallbackContext ctx) => _LookInput = ctx.ReadValue<Vector2>();

    public void OnSprint(InputAction.CallbackContext ctx) => _FastMove = ctx.ReadValueAsButton();
}
